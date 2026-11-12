using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Core.Routes;
using MS.Cobroup.Backend.Domain.Agenda;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;

namespace MS.Cobroup.Backend.Core.Agenda;

public sealed class AgendaService(
    IAgendaRepository agendaRepository,
    IAdministrativeRouteDirectory routeDirectory)
{
    public async Task<AgendaResult<AgendaResponse>> GetAgendaAsync(
        GetAgendaQuery query,
        CancellationToken cancellationToken)
    {
        var routesResult = await routeDirectory.ListAccessibleAsync(query.AccessToken, cancellationToken);
        if (routesResult.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapRouteFailure<AgendaResponse>(routesResult.Outcome);
        }

        var accessibleRouteIds = routesResult.Routes.Select(r => r.Id).ToList();
        if (query.RouteId.HasValue && !accessibleRouteIds.Contains(query.RouteId.Value))
        {
            return AgendaResult<AgendaResponse>.Forbidden("La ruta solicitada no se encuentra dentro del alcance de operación.");
        }

        var targetDate = query.TargetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rawCustomers = await agendaRepository.GetAgendaCustomersAsync(
            query.Actor.TenantId,
            accessibleRouteIds,
            query.RouteId,
            targetDate,
            cancellationToken);

        var items = ProcessAgendaItems(rawCustomers, targetDate, query.OriginLatitude, query.OriginLongitude);
        var summary = BuildSummary(items, targetDate, query.RouteId);

        return AgendaResult<AgendaResponse>.Success(new AgendaResponse(summary, items));
    }

    public async Task<AgendaResult<IReadOnlyCollection<CustomerMapLocationDto>>> GetMapLocationsAsync(
        GetMapLocationsQuery query,
        CancellationToken cancellationToken)
    {
        var routesResult = await routeDirectory.ListAccessibleAsync(query.AccessToken, cancellationToken);
        if (routesResult.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapRouteFailure<IReadOnlyCollection<CustomerMapLocationDto>>(routesResult.Outcome);
        }

        var accessibleRouteIds = routesResult.Routes.Select(r => r.Id).ToList();
        if (query.RouteId.HasValue && !accessibleRouteIds.Contains(query.RouteId.Value))
        {
            return AgendaResult<IReadOnlyCollection<CustomerMapLocationDto>>.Forbidden("La ruta solicitada no se encuentra dentro del alcance de operación.");
        }

        var targetDate = query.TargetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rawCustomers = await agendaRepository.GetAgendaCustomersAsync(
            query.Actor.TenantId,
            accessibleRouteIds,
            query.RouteId,
            targetDate,
            cancellationToken);

        var items = ProcessAgendaItems(rawCustomers, targetDate, null, null);
        var mapLocations = items
            .Where(i => i.Latitude.HasValue && i.Longitude.HasValue)
            .GroupBy(i => i.CustomerId)
            .Select(g =>
            {
                var first = g.First();
                var worstStatus = g.Any(i => i.Status == AgendaStatus.Overdue) ? AgendaStatus.Overdue
                    : g.Any(i => i.Status == AgendaStatus.DueToday) ? AgendaStatus.DueToday
                    : g.Any(i => i.Status == AgendaStatus.Collected) ? AgendaStatus.Collected
                    : AgendaStatus.UpToDate;
                var markerColor = worstStatus switch
                {
                    AgendaStatus.Overdue => "#DC2626",
                    AgendaStatus.DueToday => "#F59E0B",
                    AgendaStatus.Collected => "#10B981",
                    _ => "#3B82F6"
                };
                return new CustomerMapLocationDto(
                    first.CustomerId,
                    first.CustomerName,
                    first.BusinessOrWorkName,
                    first.PrimaryPhone,
                    first.RouteId,
                    first.Latitude!.Value,
                    first.Longitude!.Value,
                    first.Address,
                    first.Alias,
                    first.ActiveLoanId,
                    g.Sum(i => i.LoanBalance),
                    g.Sum(i => i.OverdueAmount),
                    g.Sum(i => i.DueTodayAmount),
                    worstStatus,
                    markerColor);
            })
            .ToList();

        return AgendaResult<IReadOnlyCollection<CustomerMapLocationDto>>.Success(mapLocations);
    }

    public async Task<AgendaResult<AgendaResponse>> OptimizeSequenceAsync(
        OptimizeSequenceCommand command,
        CancellationToken cancellationToken)
    {
        var input = command.Input;
        var routesResult = await routeDirectory.ListAccessibleAsync(command.AccessToken, cancellationToken);
        if (routesResult.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapRouteFailure<AgendaResponse>(routesResult.Outcome);
        }

        var accessibleRouteIds = routesResult.Routes.Select(r => r.Id).ToList();
        if (input.RouteId.HasValue && !accessibleRouteIds.Contains(input.RouteId.Value))
        {
            return AgendaResult<AgendaResponse>.Forbidden("La ruta solicitada no se encuentra dentro del alcance de operación.");
        }

        var targetDate = input.TargetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var rawCustomers = await agendaRepository.GetAgendaCustomersAsync(
            command.Actor.TenantId,
            accessibleRouteIds,
            input.RouteId,
            targetDate,
            cancellationToken);

        var allItems = ProcessAgendaItems(rawCustomers, targetDate, input.OriginLatitude, input.OriginLongitude);
        var sorted = OptimizeByNearestNeighbor(allItems, input.OriginLatitude, input.OriginLongitude);
        var summary = BuildSummary(sorted, targetDate, input.RouteId);

        return AgendaResult<AgendaResponse>.Success(new AgendaResponse(summary, sorted));
    }

    public async Task<AgendaResult<PortfolioAnalyticsResponse>> GetRouteAnalyticsAsync(
        GetRouteAnalyticsQuery query,
        CancellationToken cancellationToken)
    {
        var routesResult = await routeDirectory.ListAccessibleAsync(query.AccessToken, cancellationToken);
        if (routesResult.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapRouteFailure<PortfolioAnalyticsResponse>(routesResult.Outcome);
        }

        var accessibleRouteIds = routesResult.Routes.Select(r => r.Id).ToList();
        if (query.RouteId.HasValue && !accessibleRouteIds.Contains(query.RouteId.Value))
        {
            return AgendaResult<PortfolioAnalyticsResponse>.Forbidden("La ruta solicitada no se encuentra dentro del alcance de operación.");
        }

        var targetDate = query.TargetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var routeData = await agendaRepository.GetRoutePortfolioDataAsync(
            query.Actor.TenantId,
            accessibleRouteIds,
            query.RouteId,
            targetDate,
            cancellationToken);

        var totalLoans = routeData.Sum(r => r.ActiveLoansCount);
        var totalCustomers = routeData.Sum(r => r.ActiveCustomersCount);
        var totalPlaced = routeData.Sum(r => r.TotalPlacedAmount);
        var totalCollected = routeData.Sum(r => r.TotalCollectedAmount);
        var totalBalance = routeData.Sum(r => r.TotalBalanceAmount);
        var totalOverdue = routeData.Sum(r => r.OverdueBalanceAmount);

        var parRate = totalBalance > 0m
            ? Math.Round(totalOverdue / totalBalance * 100m, 2)
            : 0m;

        // Consolidate aging buckets
        var bucketMap = new Dictionary<string, (string Label, int MinDays, int? MaxDays, decimal Amount, int Count)>
        {
            ["current"] = ("Al día (0 días)", 0, 0, 0m, 0),
            ["1_7_days"] = ("Mora temprana (1-7 días)", 1, 7, 0m, 0),
            ["8_15_days"] = ("Mora moderada (8-15 días)", 8, 15, 0m, 0),
            ["16_30_days"] = ("Mora alta (16-30 días)", 16, 30, 0m, 0),
            ["over_30_days"] = ("Mora crítica (>30 días)", 31, null, 0m, 0)
        };

        foreach (var r in routeData)
        {
            foreach (var b in r.AgingBuckets)
            {
                if (bucketMap.TryGetValue(b.BucketKey, out var current))
                {
                    bucketMap[b.BucketKey] = (current.Label, current.MinDays, current.MaxDays, current.Amount + b.TotalAmount, current.Count + b.LoanCount);
                }
            }
        }

        var consolidatedBuckets = bucketMap.Select(kv => new AgingBucketData(
            kv.Key,
            kv.Value.Label,
            kv.Value.MinDays,
            kv.Value.MaxDays,
            kv.Value.Amount,
            kv.Value.Count)).ToList();

        // Calculate agenda collection efficiency for targetDate
        var rawCustomers = await agendaRepository.GetAgendaCustomersAsync(
            query.Actor.TenantId,
            accessibleRouteIds,
            query.RouteId,
            targetDate,
            cancellationToken);

        var items = ProcessAgendaItems(rawCustomers, targetDate, null, null);
        var totalDueToday = items.Sum(i => i.DueTodayAmount);
        var totalCollectedToday = items.Sum(i => i.CollectedTodayAmount);
        var duePlusOverdue = totalDueToday + totalOverdue;
        var efficiencyRate = duePlusOverdue > 0m
            ? Math.Round(totalCollectedToday / duePlusOverdue * 100m, 2)
            : 100m;

        var response = new PortfolioAnalyticsResponse(
            targetDate,
            totalLoans,
            totalCustomers,
            totalPlaced,
            totalCollected,
            totalBalance,
            totalOverdue,
            parRate,
            efficiencyRate,
            consolidatedBuckets,
            routeData);

        return AgendaResult<PortfolioAnalyticsResponse>.Success(response);
    }

    private static List<AgendaItemDto> ProcessAgendaItems(
        IReadOnlyCollection<AgendaCustomerData> rawCustomers,
        DateOnly targetDate,
        decimal? originLat,
        decimal? originLon)
    {
        var items = new List<AgendaItemDto>(rawCustomers.Count);

        foreach (var c in rawCustomers)
        {
            decimal? distanceKm = null;
            if (originLat.HasValue && originLon.HasValue && c.Location is not null)
            {
                distanceKm = HaversineGeoCalculator.CalculateDistanceKm(
                    originLat.Value,
                    originLon.Value,
                    c.Location.Latitude,
                    c.Location.Longitude);
            }

            if (c.ActiveLoans.Count == 0)
            {
                var collectedToday = c.PaymentsOnDate.Sum(p => p.Amount);
                var status = collectedToday > 0m ? AgendaStatus.Collected : AgendaStatus.UpToDate;
                var statusLabel = collectedToday > 0m ? "Cobrado hoy" : "Al día";
                var markerColor = collectedToday > 0m ? "#10B981" : "#3B82F6";

                items.Add(new AgendaItemDto(
                    c.CustomerId,
                    c.CustomerName,
                    c.BusinessOrWorkName,
                    c.PrimaryPhone,
                    c.RouteId,
                    c.Location?.Latitude,
                    c.Location?.Longitude,
                    c.Location?.Address,
                    c.Location?.Alias,
                    null,
                    0m,
                    0m,
                    0,
                    0m,
                    collectedToday,
                    status,
                    statusLabel,
                    markerColor,
                    distanceKm,
                    0m,
                    0));
                continue;
            }

            foreach (var loan in c.ActiveLoans)
            {
                decimal overdueAmount = 0m;
                int daysOverdue = 0;
                decimal dueTodayAmount = 0m;
                int overdueCount = 0;

                var overdueInstallments = loan.Installments
                    .Where(i => i.DueDate < targetDate && i.PaidAmount < i.ExpectedAmount)
                    .ToList();

                if (overdueInstallments.Count > 0)
                {
                    overdueAmount = overdueInstallments.Sum(i => i.ExpectedAmount - i.PaidAmount);
                    overdueCount = overdueInstallments.Count;
                    var minDue = overdueInstallments.Min(i => i.DueDate);
                    var days = targetDate.DayNumber - minDue.DayNumber;
                    if (days > daysOverdue)
                    {
                        daysOverdue = days;
                    }
                }

                var todayInstallments = loan.Installments
                    .Where(i => i.DueDate == targetDate && i.PaidAmount < i.ExpectedAmount)
                    .ToList();

                if (todayInstallments.Count > 0)
                {
                    dueTodayAmount = todayInstallments.Sum(i => i.ExpectedAmount - i.PaidAmount);
                }

                var regularInstallment = todayInstallments.FirstOrDefault()
                    ?? overdueInstallments.FirstOrDefault()
                    ?? loan.Installments.OrderBy(i => i.Number).FirstOrDefault();

                decimal installmentAmount = regularInstallment?.ExpectedAmount
                    ?? (loan.InstallmentCount > 0 ? loan.TotalAmount / loan.InstallmentCount : 0m);

                var collectedForThisLoan = c.PaymentsOnDate.Where(p => p.LoanId == loan.LoanId).Sum(p => p.Amount);

                AgendaStatus status;
                string statusLabel;
                string markerColor;

                if (overdueAmount > 0m)
                {
                    status = AgendaStatus.Overdue;
                    statusLabel = $"En mora ({daysOverdue}d)";
                    markerColor = "#DC2626"; // danger red
                }
                else if (dueTodayAmount > 0m)
                {
                    status = AgendaStatus.DueToday;
                    statusLabel = "Cobro hoy";
                    markerColor = "#F59E0B"; // warning yellow/amber
                }
                else if (collectedForThisLoan > 0m)
                {
                    status = AgendaStatus.Collected;
                    statusLabel = "Cobrado hoy";
                    markerColor = "#10B981"; // success green
                }
                else
                {
                    status = AgendaStatus.UpToDate;
                    statusLabel = "Al día";
                    markerColor = "#3B82F6"; // info blue
                }

                items.Add(new AgendaItemDto(
                    c.CustomerId,
                    c.CustomerName,
                    c.BusinessOrWorkName,
                    c.PrimaryPhone,
                    c.RouteId,
                    c.Location?.Latitude,
                    c.Location?.Longitude,
                    c.Location?.Address,
                    c.Location?.Alias,
                    loan.LoanId,
                    loan.BalanceAmount,
                    overdueAmount,
                    daysOverdue,
                    dueTodayAmount,
                    collectedForThisLoan,
                    status,
                    statusLabel,
                    markerColor,
                    distanceKm,
                    installmentAmount,
                    overdueCount));
            }
        }

        return items;
    }

    private static List<AgendaItemDto> OptimizeByNearestNeighbor(
        List<AgendaItemDto> items,
        decimal originLat,
        decimal originLon)
    {
        var withCoords = items.Where(i => i.Latitude.HasValue && i.Longitude.HasValue).ToList();
        var withoutCoords = items.Where(i => !i.Latitude.HasValue || !i.Longitude.HasValue).ToList();

        var result = new List<AgendaItemDto>(items.Count);
        var currentLat = originLat;
        var currentLon = originLon;

        while (withCoords.Count > 0)
        {
            var nearestIndex = 0;
            var minDistance = decimal.MaxValue;

            for (var i = 0; i < withCoords.Count; i++)
            {
                var candidate = withCoords[i];
                var dist = HaversineGeoCalculator.CalculateDistanceKm(
                    currentLat,
                    currentLon,
                    candidate.Latitude!.Value,
                    candidate.Longitude!.Value);

                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestIndex = i;
                }
            }

            var chosen = withCoords[nearestIndex];
            withCoords.RemoveAt(nearestIndex);

            result.Add(chosen with { DistanceKm = minDistance });
            currentLat = chosen.Latitude!.Value;
            currentLon = chosen.Longitude!.Value;
        }

        result.AddRange(withoutCoords);
        return result;
    }

    private static AgendaSummaryDto BuildSummary(IReadOnlyCollection<AgendaItemDto> items, DateOnly targetDate, Guid? routeId)
    {
        var totalCustomers = items.Count;
        var totalDueToday = items.Sum(i => i.DueTodayAmount);
        var totalOverdue = items.Sum(i => i.OverdueAmount);
        var totalCollectedToday = items.Sum(i => i.CollectedTodayAmount);

        var overdueCount = items.Count(i => i.Status is AgendaStatus.Overdue);
        var dueTodayCount = items.Count(i => i.Status is AgendaStatus.DueToday);
        var collectedCount = items.Count(i => i.Status is AgendaStatus.Collected);
        var upToDateCount = items.Count(i => i.Status is AgendaStatus.UpToDate);

        return new AgendaSummaryDto(
            targetDate,
            routeId,
            totalCustomers,
            totalDueToday,
            totalOverdue,
            totalCollectedToday,
            overdueCount,
            dueTodayCount,
            collectedCount,
            upToDateCount);
    }

    private static AgendaResult<T> MapRouteFailure<T>(RouteDirectoryOutcome outcome) =>
        outcome switch
        {
            RouteDirectoryOutcome.Unauthorized => AgendaResult<T>.Unauthorized(),
            RouteDirectoryOutcome.Forbidden => AgendaResult<T>.Forbidden(),
            _ => AgendaResult<T>.Unavailable()
        };
}
