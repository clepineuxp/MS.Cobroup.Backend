using System.Text.Json;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;

namespace MS.Cobroup.Backend.Core.Customers;

public sealed class CustomerService(
    ICustomerRepository repository,
    IAdministrativeRouteDirectory routeDirectory)
{
    public async Task<CustomerSearchResult> SearchAsync(
        SearchCustomersQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            return CustomerSearchResult.Invalid("Page must be positive and pageSize must be between 1 and 100.");
        }

        var routes = await GetRoutesAsync(query.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapSearchFailure(routes.Outcome);
        }

        var routeIds = routes.Routes.Select(route => route.Id).ToHashSet();
        if (query.RouteId.HasValue && !routeIds.Contains(query.RouteId.Value))
        {
            return CustomerSearchResult.Forbidden();
        }

        var page = await repository.SearchAsync(
            query.Actor.TenantId,
            routeIds,
            query.RouteId,
            query.Search,
            query.Page,
            query.PageSize,
            cancellationToken);
        return CustomerSearchResult.Success(page);
    }

    public async Task<CustomerResult> GetAsync(
        GetCustomerQuery query,
        CancellationToken cancellationToken)
    {
        var routes = await GetRoutesAsync(query.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var customer = await repository.FindAsync(query.Actor.TenantId, query.CustomerId, cancellationToken);
        if (customer is null || routes.Routes.All(route => route.Id != customer.RouteId))
        {
            return CustomerResult.NotFound();
        }

        return CustomerResult.Success(ToDetails(customer));
    }

    public async Task<CustomerResult> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var routes = await GetRoutesAsync(command.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var route = routes.Routes.SingleOrDefault(item => item.Id == command.RouteId);
        if (route is null)
        {
            return CustomerResult.Forbidden();
        }

        Customer customer;
        try
        {
            customer = Customer.Create(
                command.Actor.TenantId,
                command.RouteId,
                command.FirstNames,
                command.LastNames,
                command.BusinessOrWorkName,
                command.Observations,
                command.Phones.Select(item => new CustomerPhoneValue(item.Number, item.IsPrimary)).ToArray(),
                command.Locations.Select(item => new CustomerLocationValue(
                    item.Latitude,
                    item.Longitude,
                    item.Alias,
                    item.Address,
                    item.IsPrimary,
                    item.Source)).ToArray(),
                command.Actor.UserId,
                DateTimeOffset.UtcNow,
                command.Identification);
        }
        catch (ArgumentException exception)
        {
            return CustomerResult.Invalid(exception.Message);
        }

        var auditRecord = CustomerAuditRecord.Create(
            customer.TenantId,
            customer.Id,
            command.Actor.UserId,
            CustomerAuditAction.Created,
            SerializeAuditDetails(null, customer),
            customer.CreatedAtUtc);
        var outcome = await repository.TryAddAsync(
            customer,
            auditRecord,
            route.MaxActiveCustomers,
            cancellationToken);
        return outcome switch
        {
            CustomerSaveOutcome.Saved => CustomerResult.Created(ToDetails(customer)),
            CustomerSaveOutcome.CapacityReached => CustomerResult.CapacityReached(),
            _ => CustomerResult.Conflict("The customer could not be created due to a concurrent change.")
        };
    }

    public async Task<CustomerResult> UpdateAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken)
    {
        var routes = await GetRoutesAsync(command.AccessToken, cancellationToken);
        if (routes.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapFailure(routes.Outcome);
        }

        var customer = await repository.FindAsync(command.Actor.TenantId, command.CustomerId, cancellationToken);
        if (customer is null || routes.Routes.All(route => route.Id != customer.RouteId))
        {
            return CustomerResult.NotFound();
        }

        var targetRoute = routes.Routes.SingleOrDefault(item => item.Id == command.RouteId);
        if (targetRoute is null)
        {
            return CustomerResult.Forbidden();
        }

        var routeChanged = customer.RouteId != command.RouteId;
        var statusChanged = customer.Status != command.Status;
        if ((routeChanged || statusChanged) && command.Actor.Role is not UserRole.Admin)
        {
            return CustomerResult.Forbidden();
        }

        var previousRouteId = customer.RouteId;
        var previousStatus = customer.Status;
        var previousSnapshot = CustomerAuditSnapshot.From(customer);
        var updatedAtUtc = DateTimeOffset.UtcNow;
        try
        {
            customer.Update(
                command.RouteId,
                command.FirstNames,
                command.LastNames,
                command.BusinessOrWorkName,
                command.Observations,
                command.Status,
                command.Phones.Select(item => new CustomerPhoneValue(item.Number, item.IsPrimary)).ToArray(),
                command.Locations.Select(item => new CustomerLocationValue(
                    item.Latitude,
                    item.Longitude,
                    item.Alias,
                    item.Address,
                    item.IsPrimary,
                    item.Source)).ToArray(),
                command.Actor.UserId,
                updatedAtUtc,
                command.Identification);
        }
        catch (ArgumentException exception)
        {
            return CustomerResult.Invalid(exception.Message);
        }

        var auditAction = (routeChanged, statusChanged) switch
        {
            (true, true) => CustomerAuditAction.TransferredAndStatusChanged,
            (true, false) => CustomerAuditAction.Transferred,
            (false, true) => CustomerAuditAction.StatusChanged,
            _ => CustomerAuditAction.Updated
        };
        var auditRecord = CustomerAuditRecord.Create(
            customer.TenantId,
            customer.Id,
            command.Actor.UserId,
            auditAction,
            SerializeAuditDetails(previousSnapshot, customer),
            updatedAtUtc);
        var outcome = await repository.TryUpdateAsync(
            customer,
            auditRecord,
            previousRouteId,
            previousStatus,
            targetRoute.MaxActiveCustomers,
            cancellationToken);
        return outcome switch
        {
            CustomerSaveOutcome.Saved => CustomerResult.Success(ToDetails(customer)),
            CustomerSaveOutcome.CapacityReached => CustomerResult.CapacityReached(),
            _ => CustomerResult.Conflict("The customer could not be updated due to a concurrent change.")
        };
    }

    private Task<RouteDirectoryResult> GetRoutesAsync(string accessToken, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(accessToken)
            ? Task.FromResult(RouteDirectoryResult.Unauthorized())
            : routeDirectory.ListAccessibleAsync(accessToken, cancellationToken);

    private static CustomerResult MapFailure(RouteDirectoryOutcome outcome) => outcome switch
    {
        RouteDirectoryOutcome.Unauthorized => CustomerResult.Unauthorized(),
        RouteDirectoryOutcome.Forbidden => CustomerResult.Forbidden(),
        _ => CustomerResult.Unavailable()
    };

    private static CustomerSearchResult MapSearchFailure(RouteDirectoryOutcome outcome) => outcome switch
    {
        RouteDirectoryOutcome.Unauthorized => CustomerSearchResult.Unauthorized(),
        RouteDirectoryOutcome.Forbidden => CustomerSearchResult.Forbidden(),
        _ => CustomerSearchResult.Unavailable()
    };

    private static CustomerDetails ToDetails(Customer customer) => new(
        customer.Id,
        customer.RouteId,
        customer.FirstNames,
        customer.LastNames,
        customer.BusinessOrWorkName,
        customer.Observations,
        customer.Status,
        customer.Phones
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CustomerPhoneDetails(item.Id, item.Number, item.IsPrimary))
            .ToArray(),
        customer.Locations
            .OrderByDescending(item => item.IsPrimary)
            .Select(item => new CustomerLocationDetails(
                item.Id,
                item.Latitude,
                item.Longitude,
                item.Alias,
                item.Address,
                item.IsPrimary,
                item.Source))
            .ToArray(),
        customer.CreatedAtUtc,
        customer.UpdatedAtUtc,
        customer.Identification);

    private static string SerializeAuditDetails(CustomerAuditSnapshot? before, Customer after) =>
        JsonSerializer.Serialize(new CustomerAuditChange(before, CustomerAuditSnapshot.From(after)));

    private sealed record CustomerAuditChange(CustomerAuditSnapshot? Before, CustomerAuditSnapshot After);

    private sealed record CustomerAuditSnapshot(
        Guid RouteId,
        CustomerStatus Status,
        string FirstNames,
        string LastNames,
        string? BusinessOrWorkName,
        int PhoneCount,
        int LocationCount,
        string Identification)
    {
        public static CustomerAuditSnapshot From(Customer customer) => new(
            customer.RouteId,
            customer.Status,
            customer.FirstNames,
            customer.LastNames,
            customer.BusinessOrWorkName,
            customer.Phones.Count,
            customer.Locations.Count,
            customer.Identification);
    }
}
