using MS.Cobroup.Backend.Domain.Customers;

namespace MS.Cobroup.Backend.Domain.Interfaces.Repositories;

public interface ICustomerRepository
{
    Task<CustomerSearchPage> SearchAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accessibleRouteIds,
        Guid? routeId,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Customer?> FindAsync(Guid tenantId, Guid customerId, CancellationToken cancellationToken);

    Task<CustomerSaveOutcome> TryAddAsync(
        Customer customer,
        int? maxActiveCustomers,
        CancellationToken cancellationToken);

    Task<CustomerSaveOutcome> TryUpdateAsync(
        Customer customer,
        Guid previousRouteId,
        CustomerStatus previousStatus,
        int? maxActiveCustomers,
        CancellationToken cancellationToken);
}

public enum CustomerSaveOutcome
{
    Saved,
    CapacityReached,
    Conflict
}

public sealed record CustomerSearchPage(
    IReadOnlyCollection<CustomerSearchItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CustomerSearchItem(
    Guid Id,
    Guid RouteId,
    string FirstNames,
    string LastNames,
    string? BusinessOrWorkName,
    string PrimaryPhone,
    decimal PrimaryLatitude,
    decimal PrimaryLongitude,
    CustomerStatus Status,
    DateTimeOffset UpdatedAtUtc);
