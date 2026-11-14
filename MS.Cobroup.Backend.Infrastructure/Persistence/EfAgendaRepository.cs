using Microsoft.EntityFrameworkCore;
using MS.Cobroup.Backend.Domain.Agenda;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;
using MS.Cobroup.Backend.Domain.Loans;

namespace MS.Cobroup.Backend.Infrastructure.Persistence;

public sealed class EfAgendaRepository(OperationsDbContext dbContext) : IAgendaRepository
{
    public async Task<IReadOnlyCollection<AgendaCustomerData>> GetAgendaCustomersAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly targetDate,
        CancellationToken cancellationToken)
    {
        var targetRoutes = accessibleRouteIds;
        if (routeId.HasValue)
        {
            if (!accessibleRouteIds.Contains(routeId.Value))
            {
                return [];
            }
            targetRoutes = [routeId.Value];
        }

        if (targetRoutes.Count == 0)
        {
            return [];
        }

        // Query active customers in routes
        var customers = await dbContext.Customers
            .AsNoTracking()
            .Include(c => c.Locations)
            .Include(c => c.Phones)
            .Where(c => c.TenantId == tenantId && c.Status == CustomerStatus.Active && targetRoutes.Contains(c.RouteId))
            .OrderBy(c => c.LastNames)
            .ThenBy(c => c.FirstNames)
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return [];
        }

        var customerIds = customers.Select(c => c.Id).ToList();

        // Query active loans for these customers
        var loans = await dbContext.Loans
            .Include(l => l.Installments)
            .Where(l => l.TenantId == tenantId && l.Status == LoanStatus.Active && customerIds.Contains(l.CustomerId))
            .ToListAsync(cancellationToken);

        var loansByCustomer = loans
            .GroupBy(l => l.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Query payments registered on targetDate converting to local tenant timezone (defaulting to America/Bogota)
        var tz = GetTenantTimeZone();
        var startLocal = targetDate.ToDateTime(TimeOnly.MinValue);
        var endLocal = targetDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var fromUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, tz), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, tz), TimeSpan.Zero);

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && customerIds.Contains(p.CustomerId) && p.RegisteredAtUtc >= fromUtc && p.RegisteredAtUtc < toUtc)
            .ToListAsync(cancellationToken);

        var paymentsByCustomer = payments
            .GroupBy(p => p.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var nonPaymentRecords = await dbContext.CustomerNonPaymentRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && targetRoutes.Contains(r.RouteId) && r.BusinessDate == targetDate)
            .ToListAsync(cancellationToken);

        var nonPaymentsByCustomer = nonPaymentRecords
            .GroupBy(r => r.CustomerId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<CustomerNonPaymentRecord>)g.ToList());

        var result = new List<AgendaCustomerData>(customers.Count);

        foreach (var c in customers)
        {
            var primaryLoc = c.Locations.FirstOrDefault(l => l.IsPrimary) ?? c.Locations.FirstOrDefault();
            AgendaLocationData? locationData = primaryLoc is not null
                ? new AgendaLocationData(primaryLoc.Latitude, primaryLoc.Longitude, primaryLoc.Address, primaryLoc.Alias)
                : null;

            var primaryPhone = c.Phones.FirstOrDefault(p => p.IsPrimary)?.Number ?? c.Phones.FirstOrDefault()?.Number;

            var customerLoans = loansByCustomer.TryGetValue(c.Id, out var cLoans)
                ? cLoans.Select(l => new AgendaLoanData(
                    l.Id,
                    l.Terms.TotalAmount,
                    l.Terms.PaidAmount,
                    l.Terms.BalanceAmount,
                    l.Terms.InstallmentCount,
                    l.Installments.OrderBy(i => i.Number).Select(i => new AgendaInstallmentData(
                        i.Id,
                        i.Number,
                        i.DueDate,
                        i.ExpectedAmount,
                        i.PaidAmount,
                        i.Status)).ToList(),
                    l.Terms.PaymentFrequency)).ToList()
                : [];

            var customerPayments = paymentsByCustomer.TryGetValue(c.Id, out var cPayments)
                ? cPayments.Select(p => new AgendaPaymentData(
                    p.Id,
                    p.LoanId,
                    p.Amount,
                    p.RegisteredAtUtc,
                    p.PaymentMethodName)).ToList()
                : [];

            var customerNonPayments = nonPaymentsByCustomer.TryGetValue(c.Id, out var cNp)
                ? cNp
                : [];

            result.Add(new AgendaCustomerData(
                c.Id,
                $"{c.FirstNames} {c.LastNames}".Trim(),
                c.BusinessOrWorkName,
                primaryPhone,
                c.RouteId,
                locationData,
                customerLoans,
                customerPayments,
                customerNonPayments));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<RoutePortfolioData>> GetRoutePortfolioDataAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly targetDate,
        CancellationToken cancellationToken)
    {
        var targetRoutes = accessibleRouteIds;
        if (routeId.HasValue)
        {
            if (!accessibleRouteIds.Contains(routeId.Value))
            {
                return [];
            }
            targetRoutes = [routeId.Value];
        }

        if (targetRoutes.Count == 0)
        {
            return [];
        }

        var loans = await dbContext.Loans
            .Include(l => l.Installments)
            .Where(l => l.TenantId == tenantId && l.Status == LoanStatus.Active && targetRoutes.Contains(l.RouteId))
            .ToListAsync(cancellationToken);

        var activeCustomersCountByRoute = await dbContext.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Status == CustomerStatus.Active && targetRoutes.Contains(c.RouteId))
            .GroupBy(c => c.RouteId)
            .Select(g => new { RouteId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RouteId, g => g.Count, cancellationToken);

        var loansByRoute = loans
            .GroupBy(l => l.RouteId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<RoutePortfolioData>();

        foreach (var rId in targetRoutes)
        {
            loansByRoute.TryGetValue(rId, out var rLoans);
            rLoans ??= [];

            activeCustomersCountByRoute.TryGetValue(rId, out var custCount);

            var totalPlaced = rLoans.Sum(l => l.Terms.TotalAmount);
            var totalCollected = rLoans.Sum(l => l.Terms.PaidAmount);
            var totalBalance = rLoans.Sum(l => l.Terms.BalanceAmount);

            decimal overdueBalance = 0m;

            var bucket0 = 0m; var count0 = 0;
            var bucket1To7 = 0m; var count1To7 = 0;
            var bucket8To15 = 0m; var count8To15 = 0;
            var bucket16To30 = 0m; var count16To30 = 0;
            var bucketOver30 = 0m; var countOver30 = 0;

            foreach (var loan in rLoans)
            {
                var overdueInstallments = loan.Installments
                    .Where(i => i.DueDate < targetDate && i.PaidAmount < i.ExpectedAmount)
                    .ToList();

                if (overdueInstallments.Count > 0)
                {
                    var loanOverdueAmount = overdueInstallments.Sum(i => i.ExpectedAmount - i.PaidAmount);
                    overdueBalance += loanOverdueAmount;

                    var minDueDate = overdueInstallments.Min(i => i.DueDate);
                    var daysOverdue = targetDate.DayNumber - minDueDate.DayNumber;

                    if (daysOverdue <= 7)
                    {
                        bucket1To7 += loanOverdueAmount;
                        count1To7++;
                    }
                    else if (daysOverdue <= 15)
                    {
                        bucket8To15 += loanOverdueAmount;
                        count8To15++;
                    }
                    else if (daysOverdue <= 30)
                    {
                        bucket16To30 += loanOverdueAmount;
                        count16To30++;
                    }
                    else
                    {
                        bucketOver30 += loanOverdueAmount;
                        countOver30++;
                    }
                }
                else
                {
                    bucket0 += loan.Terms.BalanceAmount;
                    count0++;
                }
            }

            var parRate = totalBalance > 0m
                ? Math.Round(overdueBalance / totalBalance * 100m, 2)
                : 0m;

            var buckets = new List<AgingBucketData>
            {
                new("current", "Al día (0 días)", 0, 0, bucket0, count0),
                new("1_7_days", "Mora temprana (1-7 días)", 1, 7, bucket1To7, count1To7),
                new("8_15_days", "Mora moderada (8-15 días)", 8, 15, bucket8To15, count8To15),
                new("16_30_days", "Mora alta (16-30 días)", 16, 30, bucket16To30, count16To30),
                new("over_30_days", "Mora crítica (>30 días)", 31, null, bucketOver30, countOver30)
            };

            result.Add(new RoutePortfolioData(
                rId,
                rLoans.Count,
                custCount,
                totalPlaced,
                totalCollected,
                totalBalance,
                overdueBalance,
                parRate,
                buckets));
        }

        return result;
    }

    private static TimeZoneInfo GetTenantTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
        }
        catch
        {
            return TimeZoneInfo.Utc;
        }
    }

    public async Task<IReadOnlyCollection<NonPaymentReason>> GetNonPaymentReasonsAsync(
        Guid tenantId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.NonPaymentReasons
            .Where(r => r.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        var reasons = await query
            .OrderBy(r => r.DisplayOrder)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        if (reasons.Count == 0)
        {
            // Seed default reasons for this tenant
            var now = DateTimeOffset.UtcNow;
            var defaults = new List<NonPaymentReason>
            {
                new(Guid.NewGuid(), tenantId, "Cliente no se encuentra / ausente", "El cliente no estaba en su domicilio o lugar de trabajo.", true, 1, now),
                new(Guid.NewGuid(), tenantId, "Sin dinero en el momento", "El cliente no cuenta con el dinero hoy.", true, 2, now),
                new(Guid.NewGuid(), tenantId, "Problemas de salud o calamidad", "Situación de salud, emergencia o calamidad familiar.", true, 3, now),
                new(Guid.NewGuid(), tenantId, "Fecha de pago reprogramada", "Se acordó una nueva fecha con el supervisor/cobrador.", true, 4, now),
                new(Guid.NewGuid(), tenantId, "Rechaza realizar el pago", "El cliente se niega a realizar el pago pactado.", true, 5, now),
                new(Guid.NewGuid(), tenantId, "Otro motivo", "Motivo no especificado en las categorías anteriores.", true, 6, now),
            };

            await dbContext.NonPaymentReasons.AddRangeAsync(defaults, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return defaults;
        }

        return reasons;
    }

    public async Task<NonPaymentReason?> GetNonPaymentReasonByIdAsync(
        Guid tenantId,
        Guid reasonId,
        CancellationToken cancellationToken)
    {
        return await dbContext.NonPaymentReasons
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == reasonId, cancellationToken);
    }

    public async Task SaveNonPaymentReasonAsync(
        NonPaymentReason reason,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.NonPaymentReasons
            .FirstOrDefaultAsync(r => r.TenantId == reason.TenantId && r.Id == reason.Id, cancellationToken);

        if (existing is null)
        {
            await dbContext.NonPaymentReasons.AddAsync(reason, cancellationToken);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(reason);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteNonPaymentReasonAsync(
        Guid tenantId,
        Guid reasonId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.NonPaymentReasons
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == reasonId, cancellationToken);

        if (existing is not null)
        {
            dbContext.NonPaymentReasons.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<CustomerNonPaymentRecord>> GetNonPaymentRecordsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var targetRoutes = accessibleRouteIds;
        if (routeId.HasValue)
        {
            if (!accessibleRouteIds.Contains(routeId.Value))
            {
                return [];
            }
            targetRoutes = [routeId.Value];
        }

        return await dbContext.CustomerNonPaymentRecords
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && targetRoutes.Contains(r.RouteId) && r.BusinessDate == businessDate)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveNonPaymentRecordAsync(
        CustomerNonPaymentRecord record,
        CancellationToken cancellationToken)
    {
        // Remove existing record for same loan/customer on same date if present (upsert)
        var query = dbContext.CustomerNonPaymentRecords
            .Where(r => r.TenantId == record.TenantId && r.RouteId == record.RouteId && r.BusinessDate == record.BusinessDate && r.CustomerId == record.CustomerId);

        if (record.LoanId.HasValue)
        {
            query = query.Where(r => r.LoanId == record.LoanId.Value);
        }
        else
        {
            query = query.Where(r => r.LoanId == null);
        }

        var existing = await query.ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            dbContext.CustomerNonPaymentRecords.RemoveRange(existing);
        }

        await dbContext.CustomerNonPaymentRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteNonPaymentRecordAsync(
        Guid tenantId,
        Guid routeId,
        Guid customerId,
        Guid? loanId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CustomerNonPaymentRecords
            .Where(r => r.TenantId == tenantId && r.RouteId == routeId && r.CustomerId == customerId && r.BusinessDate == businessDate);

        if (loanId.HasValue)
        {
            query = query.Where(r => r.LoanId == loanId.Value || r.LoanId == null);
        }

        var existing = await query.ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            dbContext.CustomerNonPaymentRecords.RemoveRange(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<DateOnly?> GetActiveCashBoxBusinessDateAsync(
        Guid tenantId,
        Guid routeId,
        CancellationToken cancellationToken)
    {
        var activeBox = await dbContext.RouteDayClosings
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.RouteId == routeId && !b.IsClosed)
            .OrderByDescending(b => b.BusinessDate)
            .FirstOrDefaultAsync(cancellationToken);
        return activeBox?.BusinessDate;
    }
}
