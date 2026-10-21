using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using Npgsql;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class EfCustomerRepository(OperationsDbContext dbContext) : ICustomerRepository
{
    public async Task<CustomerSearchPage> SearchAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (accessibleRouteIds.Count is 0)
        {
            return new CustomerSearchPage([], page, pageSize, 0);
        }

        var query = dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.TenantId == tenantId && accessibleRouteIds.Contains(customer.RouteId));
        if (routeId.HasValue)
        {
            query = query.Where(customer => customer.RouteId == routeId.Value);
        }

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            var pattern = $"%{normalizedSearch}%";
            query = query.Where(customer =>
                EF.Functions.ILike(customer.FirstNames, pattern)
                || EF.Functions.ILike(customer.LastNames, pattern)
                || (customer.BusinessOrWorkName != null && EF.Functions.ILike(customer.BusinessOrWorkName, pattern))
                || customer.Phones.Any(phone => EF.Functions.ILike(phone.Number, pattern)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(customer => customer.LastNames)
            .ThenBy(customer => customer.FirstNames)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(customer => new CustomerSearchItem(
                customer.Id,
                customer.RouteId,
                customer.FirstNames,
                customer.LastNames,
                customer.BusinessOrWorkName,
                customer.Phones.Where(phone => phone.IsPrimary).Select(phone => phone.Number).Single(),
                customer.Locations.Where(location => location.IsPrimary).Select(location => location.Latitude).Single(),
                customer.Locations.Where(location => location.IsPrimary).Select(location => location.Longitude).Single(),
                customer.Status,
                customer.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

        return new CustomerSearchPage(items, page, pageSize, totalCount);
    }

    public Task<Customer?> FindAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers
            .Include(customer => customer.Phones)
            .Include(customer => customer.Locations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                customer => customer.TenantId == tenantId && customer.Id == customerId,
                cancellationToken);

    public async Task<CustomerSaveOutcome> TryAddAsync(
        Customer customer,
        CustomerAuditRecord auditRecord,
        int? maxActiveCustomers,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockRoutesAsync([customer.RouteId], cancellationToken);
            if (customer.Status is CustomerStatus.Active
                && await HasReachedCapacityAsync(
                    customer.TenantId,
                    customer.RouteId,
                    maxActiveCustomers,
                    null,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return CustomerSaveOutcome.CapacityReached;
            }

            dbContext.Customers.Add(customer);
            dbContext.CustomerAuditRecords.Add(auditRecord);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CustomerSaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CustomerSaveOutcome.Conflict;
        }
    }

    public async Task<CustomerSaveOutcome> TryUpdateAsync(
        Customer customer,
        CustomerAuditRecord auditRecord,
        Guid previousRouteId,
        CustomerStatus previousStatus,
        int? maxActiveCustomers,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockRoutesAsync([previousRouteId, customer.RouteId], cancellationToken);
            var consumesNewCapacity = customer.Status is CustomerStatus.Active
                && (previousStatus is not CustomerStatus.Active || previousRouteId != customer.RouteId);
            if (consumesNewCapacity
                && await HasReachedCapacityAsync(
                    customer.TenantId,
                    customer.RouteId,
                    maxActiveCustomers,
                    customer.Id,
                    cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                await dbContext.Entry(customer).ReloadAsync(cancellationToken);
                return CustomerSaveOutcome.CapacityReached;
            }

            var replacementPhones = customer.Phones.ToArray();
            var replacementLocations = customer.Locations.ToArray();
            foreach (var entry in dbContext.ChangeTracker
                         .Entries<CustomerPhone>()
                         .Where(entry => entry.Entity.CustomerId == customer.Id)
                         .ToArray())
            {
                entry.State = EntityState.Detached;
            }

            foreach (var entry in dbContext.ChangeTracker
                         .Entries<CustomerLocation>()
                         .Where(entry => entry.Entity.CustomerId == customer.Id)
                         .ToArray())
            {
                entry.State = EntityState.Detached;
            }

            await dbContext.CustomerPhones
                .Where(phone => phone.CustomerId == customer.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.CustomerLocations
                .Where(location => location.CustomerId == customer.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.CustomerPhones.AddRange(replacementPhones);
            dbContext.CustomerLocations.AddRange(replacementLocations);
            dbContext.CustomerAuditRecords.Add(auditRecord);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return CustomerSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CustomerSaveOutcome.Conflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CustomerSaveOutcome.Conflict;
        }
    }

    private async Task<bool> HasReachedCapacityAsync(
        Guid tenantId,
        Guid routeId,
        int? maximum,
        Guid? excludedCustomerId,
        CancellationToken cancellationToken)
    {
        if (!maximum.HasValue)
        {
            return false;
        }

        var activeCount = await dbContext.Customers.CountAsync(
            customer => customer.TenantId == tenantId
                && customer.RouteId == routeId
                && customer.Status == CustomerStatus.Active
                && (!excludedCustomerId.HasValue || customer.Id != excludedCustomerId.Value),
            cancellationToken);
        return activeCount >= maximum.Value;
    }

    private async Task LockRoutesAsync(
        IReadOnlyCollection<Guid> routeIds,
        CancellationToken cancellationToken)
    {
        foreach (var routeId in routeIds.Distinct().Order())
        {
            var key = BitConverter.ToInt64(routeId.ToByteArray(), 0);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({key})",
                cancellationToken);
        }
    }
}
