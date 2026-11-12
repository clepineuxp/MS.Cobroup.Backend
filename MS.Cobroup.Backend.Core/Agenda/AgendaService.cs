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
                var customerNonPayment = c.NonPaymentRecords?.FirstOrDefault(r => r.BusinessDate == targetDate);

                AgendaStatus status;
                string statusLabel;
                string markerColor;

                if (collectedToday > 0m)
                {
                    status = AgendaStatus.Collected;
                    statusLabel = "Cobrado hoy";
                    markerColor = "#10B981";
                }
                else if (customerNonPayment != null)
                {
                    status = AgendaStatus.WillNotPayToday;
                    statusLabel = "No pagará hoy";
                    markerColor = "#EC4899";
                }
                else
                {
                    status = AgendaStatus.UpToDate;
                    statusLabel = "Al día";
                    markerColor = "#3B82F6";
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
                    0,
                    null,
                    customerNonPayment?.ReasonId,
                    customerNonPayment?.ReasonName,
                    customerNonPayment?.Observations));
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
                var nonPaymentRecord = c.NonPaymentRecords?.FirstOrDefault(r =>
                    r.BusinessDate == targetDate && (r.LoanId == loan.LoanId || r.LoanId == null));

                AgendaStatus status;
                string statusLabel;
                string markerColor;

                if (collectedForThisLoan > 0m)
                {
                    // Rule 4.1 & 4.2: Any payment today moves customer to "Pagos" (Collected)
                    status = AgendaStatus.Collected;
                    statusLabel = "Cobrado hoy";
                    markerColor = "#10B981"; // success green
                }
                else if (nonPaymentRecord != null)
                {
                    // Rule 4.3: Customer marked as will not pay today
                    status = AgendaStatus.WillNotPayToday;
                    statusLabel = "No pagará hoy";
                    markerColor = "#EC4899"; // pink
                }
                else if (overdueAmount > 0m)
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
                    overdueCount,
                    loan.PaymentFrequency.ToString(),
                    nonPaymentRecord?.ReasonId,
                    nonPaymentRecord?.ReasonName,
                    nonPaymentRecord?.Observations));
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
        var willNotPayCount = items.Count(i => i.Status is AgendaStatus.WillNotPayToday);

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
            upToDateCount,
            willNotPayCount);
    }

    public async Task<AgendaResult<IReadOnlyCollection<NonPaymentReasonDto>>> GetNonPaymentReasonsAsync(
        CustomerActor actor,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var reasons = await agendaRepository.GetNonPaymentReasonsAsync(actor.TenantId, includeInactive, cancellationToken);
        var dtos = reasons.Select(r => new NonPaymentReasonDto(r.Id, r.Name, r.Description, r.IsActive, r.DisplayOrder)).ToList();
        return AgendaResult<IReadOnlyCollection<NonPaymentReasonDto>>.Success(dtos);
    }

    public async Task<AgendaResult<NonPaymentReasonDto>> CreateNonPaymentReasonAsync(
        CreateNonPaymentReasonCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Input.Name))
        {
            return AgendaResult<NonPaymentReasonDto>.Invalid("El nombre del motivo es requerido.");
        }

        var reason = new NonPaymentReason(
            Guid.NewGuid(),
            command.Actor.TenantId,
            command.Input.Name,
            command.Input.Description,
            isActive: true,
            displayOrder: command.Input.DisplayOrder,
            DateTimeOffset.UtcNow);

        await agendaRepository.SaveNonPaymentReasonAsync(reason, cancellationToken);
        var dto = new NonPaymentReasonDto(reason.Id, reason.Name, reason.Description, reason.IsActive, reason.DisplayOrder);
        return AgendaResult<NonPaymentReasonDto>.Success(dto);
    }

    public async Task<AgendaResult<NonPaymentReasonDto>> ToggleNonPaymentReasonStatusAsync(
        ToggleNonPaymentReasonStatusCommand command,
        CancellationToken cancellationToken)
    {
        var reason = await agendaRepository.GetNonPaymentReasonByIdAsync(command.Actor.TenantId, command.ReasonId, cancellationToken);
        if (reason is null)
        {
            return AgendaResult<NonPaymentReasonDto>.Invalid("El motivo de no pago no existe.");
        }

        if (command.IsActive)
        {
            reason.Activate();
        }
        else
        {
            reason.Deactivate();
        }

        await agendaRepository.SaveNonPaymentReasonAsync(reason, cancellationToken);
        var dto = new NonPaymentReasonDto(reason.Id, reason.Name, reason.Description, reason.IsActive, reason.DisplayOrder);
        return AgendaResult<NonPaymentReasonDto>.Success(dto);
    }

    public async Task<AgendaResult<bool>> RegisterWillNotPayAsync(
        RegisterWillNotPayCommand command,
        CancellationToken cancellationToken)
    {
        var routesResult = await routeDirectory.ListAccessibleAsync(command.AccessToken, cancellationToken);
        if (routesResult.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapRouteFailure<bool>(routesResult.Outcome);
        }

        var accessibleRouteIds = routesResult.Routes.Select(r => r.Id).ToList();
        if (!accessibleRouteIds.Contains(command.Input.RouteId))
        {
            return AgendaResult<bool>.Forbidden("La ruta solicitada no se encuentra dentro del alcance de operación.");
        }

        string reasonName = command.Input.ReasonName?.Trim() ?? string.Empty;
        if (command.Input.ReasonId.HasValue)
        {
            var reason = await agendaRepository.GetNonPaymentReasonByIdAsync(command.Actor.TenantId, command.Input.ReasonId.Value, cancellationToken);
            if (reason is not null)
            {
                reasonName = reason.Name;
            }
        }

        if (string.IsNullOrWhiteSpace(reasonName))
        {
            reasonName = "No especificado";
        }

        var record = new CustomerNonPaymentRecord(
            Guid.NewGuid(),
            command.Actor.TenantId,
            command.Input.RouteId,
            command.Input.CustomerId,
            command.Input.LoanId,
            command.Input.BusinessDate,
            command.Input.ReasonId,
            reasonName,
            command.Input.Observations,
            command.Actor.UserId,
            DateTimeOffset.UtcNow);

        await agendaRepository.SaveNonPaymentRecordAsync(record, cancellationToken);
        return AgendaResult<bool>.Success(true);
    }

    public async Task<AgendaResult<bool>> CancelWillNotPayAsync(
        CancelWillNotPayCommand command,
        CancellationToken cancellationToken)
    {
        var routesResult = await routeDirectory.ListAccessibleAsync(command.AccessToken, cancellationToken);
        if (routesResult.Outcome is not RouteDirectoryOutcome.Success)
        {
            return MapRouteFailure<bool>(routesResult.Outcome);
        }

        var accessibleRouteIds = routesResult.Routes.Select(r => r.Id).ToList();
        if (!accessibleRouteIds.Contains(command.Input.RouteId))
        {
            return AgendaResult<bool>.Forbidden("La ruta solicitada no se encuentra dentro del alcance de operación.");
        }

        await agendaRepository.DeleteNonPaymentRecordAsync(
            command.Actor.TenantId,
            command.Input.RouteId,
            command.Input.CustomerId,
            command.Input.LoanId,
            command.Input.BusinessDate,
            cancellationToken);

        return AgendaResult<bool>.Success(true);
    }

    private static AgendaResult<T> MapRouteFailure<T>(RouteDirectoryOutcome outcome) =>
        outcome switch
        {
            RouteDirectoryOutcome.Unauthorized => AgendaResult<T>.Unauthorized(),
            RouteDirectoryOutcome.Forbidden => AgendaResult<T>.Forbidden(),
            _ => AgendaResult<T>.Unavailable()
        };
}
