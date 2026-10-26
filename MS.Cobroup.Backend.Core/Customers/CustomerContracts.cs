using MS.Cobroup.Backend.Domain.Authorization;
using MS.Cobroup.Backend.Domain.Customers;
using MS.Cobroup.Backend.Domain.Interfaces.Repositories;

namespace MS.Cobroup.Backend.Core.Customers;

public sealed record CustomerActor(Guid UserId, Guid TenantId, UserRole Role, bool CanReopenDay = false);

public sealed record CustomerPhoneInput(string Number, bool IsPrimary);

public sealed record CustomerLocationInput(
    decimal Latitude,
    decimal Longitude,
    string? Alias,
    string? Address,
    bool IsPrimary,
    LocationSource Source);

public sealed record CreateCustomerCommand(
    CustomerActor Actor,
    string AccessToken,
    Guid RouteId,
    string FirstNames,
    string LastNames,
    string? BusinessOrWorkName,
    string? Observations,
    IReadOnlyCollection<CustomerPhoneInput> Phones,
    IReadOnlyCollection<CustomerLocationInput> Locations,
    string Identification);

public sealed record UpdateCustomerCommand(
    CustomerActor Actor,
    string AccessToken,
    Guid CustomerId,
    Guid RouteId,
    string FirstNames,
    string LastNames,
    string? BusinessOrWorkName,
    string? Observations,
    CustomerStatus Status,
    IReadOnlyCollection<CustomerPhoneInput> Phones,
    IReadOnlyCollection<CustomerLocationInput> Locations,
    string Identification);

public sealed record SearchCustomersQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid? RouteId,
    string? Search,
    int Page,
    int PageSize);

public sealed record GetCustomerQuery(
    CustomerActor Actor,
    string AccessToken,
    Guid CustomerId);

public sealed record CustomerPhoneDetails(Guid Id, string Number, bool IsPrimary);

public sealed record CustomerLocationDetails(
    Guid Id,
    decimal Latitude,
    decimal Longitude,
    string? Alias,
    string? Address,
    bool IsPrimary,
    LocationSource Source);

public sealed record CustomerDetails(
    Guid Id,
    Guid RouteId,
    string FirstNames,
    string LastNames,
    string? BusinessOrWorkName,
    string? Observations,
    CustomerStatus Status,
    IReadOnlyCollection<CustomerPhoneDetails> Phones,
    IReadOnlyCollection<CustomerLocationDetails> Locations,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Identification);

public enum CustomerOutcome
{
    Success,
    Created,
    Invalid,
    NotFound,
    Forbidden,
    Unauthorized,
    Conflict,
    CapacityReached,
    Unavailable
}

public sealed record CustomerResult(
    CustomerOutcome Outcome,
    CustomerDetails? Customer,
    string? Error)
{
    public static CustomerResult Success(CustomerDetails customer) => new(CustomerOutcome.Success, customer, null);

    public static CustomerResult Created(CustomerDetails customer) => new(CustomerOutcome.Created, customer, null);

    public static CustomerResult Invalid(string error) => new(CustomerOutcome.Invalid, null, error);

    public static CustomerResult NotFound() => new(CustomerOutcome.NotFound, null, null);

    public static CustomerResult Forbidden() => new(CustomerOutcome.Forbidden, null, null);

    public static CustomerResult Unauthorized() => new(CustomerOutcome.Unauthorized, null, null);

    public static CustomerResult Conflict(string error) => new(CustomerOutcome.Conflict, null, error);

    public static CustomerResult CapacityReached() =>
        new(CustomerOutcome.CapacityReached, null, "The route has reached its active customer capacity.");

    public static CustomerResult Unavailable() => new(CustomerOutcome.Unavailable, null, null);
}

public sealed record CustomerSearchResult(
    CustomerOutcome Outcome,
    CustomerSearchPage? Page,
    string? Error)
{
    public static CustomerSearchResult Success(CustomerSearchPage page) => new(CustomerOutcome.Success, page, null);

    public static CustomerSearchResult Invalid(string error) => new(CustomerOutcome.Invalid, null, error);

    public static CustomerSearchResult Forbidden() => new(CustomerOutcome.Forbidden, null, null);

    public static CustomerSearchResult Unauthorized() => new(CustomerOutcome.Unauthorized, null, null);

    public static CustomerSearchResult Unavailable() => new(CustomerOutcome.Unavailable, null, null);
}
