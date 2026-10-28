using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MS.Cobroup.Backend.Api.Security;
using MS.Cobroup.Backend.Core.Customers;
using MS.Cobroup.Backend.Domain.Customers;

namespace MS.Cobroup.Backend.Api.Controllers;

[ApiController]
[Route("api/operations/v1/customers")]
[Authorize(Policy = "tenant-user")]
[MachineAuthorized]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? routeId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await customers.SearchAsync(
            new SearchCustomersQuery(
                actor,
                CustomerActorReader.ReadBearerToken(Request),
                routeId,
                search,
                page,
                pageSize),
            cancellationToken);
        return result.Outcome switch
        {
            CustomerOutcome.Success => Ok(result.Page),
            CustomerOutcome.Invalid => BadRequest(new { message = result.Error }),
            CustomerOutcome.Unauthorized => Unauthorized(),
            CustomerOutcome.Forbidden => Forbid(),
            CustomerOutcome.Unavailable => Problem(
                "The route authorization directory is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("Unexpected customer search result.")
        };
    }

    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> Get(Guid customerId, CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await customers.GetAsync(
            new GetCustomerQuery(actor, CustomerActorReader.ReadBearerToken(Request), customerId),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CustomerWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await customers.CreateAsync(
            new CreateCustomerCommand(
                actor,
                CustomerActorReader.ReadBearerToken(Request),
                request.RouteId,
                request.FirstNames,
                request.LastNames,
                request.BusinessOrWorkName,
                request.Observations,
                request.Phones?.Select(item => new CustomerPhoneInput(item.Number, item.IsPrimary)).ToArray() ?? [],
                request.Locations?.Select(item => new CustomerLocationInput(
                    item.Latitude,
                    item.Longitude,
                    item.Alias,
                    item.Address,
                    item.IsPrimary,
                    item.Source)).ToArray() ?? [],
                request.Identification),
            cancellationToken);
        return result.Outcome is CustomerOutcome.Created
            ? Created($"/api/operations/v1/customers/{result.Customer!.Id}", result.Customer)
            : ToActionResult(result);
    }

    [HttpPut("{customerId:guid}")]
    public async Task<IActionResult> Update(
        Guid customerId,
        CustomerWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (!CustomerActorReader.TryRead(User, out var actor))
        {
            return Unauthorized();
        }

        var result = await customers.UpdateAsync(
            new UpdateCustomerCommand(
                actor,
                CustomerActorReader.ReadBearerToken(Request),
                customerId,
                request.RouteId,
                request.FirstNames,
                request.LastNames,
                request.BusinessOrWorkName,
                request.Observations,
                request.Status,
                request.Phones?.Select(item => new CustomerPhoneInput(item.Number, item.IsPrimary)).ToArray() ?? [],
                request.Locations?.Select(item => new CustomerLocationInput(
                    item.Latitude,
                    item.Longitude,
                    item.Alias,
                    item.Address,
                    item.IsPrimary,
                    item.Source)).ToArray() ?? [],
                request.Identification),
            cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(CustomerResult result) => result.Outcome switch
    {
        CustomerOutcome.Success => Ok(result.Customer),
        CustomerOutcome.Invalid => BadRequest(new { message = result.Error }),
        CustomerOutcome.NotFound => NotFound(),
        CustomerOutcome.Unauthorized => Unauthorized(),
        CustomerOutcome.Forbidden => Forbid(),
        CustomerOutcome.Conflict => Conflict(new { message = result.Error }),
        CustomerOutcome.CapacityReached => Conflict(new { code = "route_capacity_reached", message = result.Error }),
        CustomerOutcome.Unavailable => Problem(
            "The route authorization directory is temporarily unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Problem("Unexpected customer operation result.")
    };
}

public sealed record CustomerWriteRequest(
    Guid RouteId,
    string FirstNames,
    string LastNames,
    string? BusinessOrWorkName,
    string? Observations,
    CustomerStatus Status,
    IReadOnlyCollection<CustomerPhoneRequest>? Phones,
    IReadOnlyCollection<CustomerLocationRequest>? Locations,
    string Identification);

public sealed record CustomerPhoneRequest(string Number, bool IsPrimary);

public sealed record CustomerLocationRequest(
    decimal Latitude,
    decimal Longitude,
    string? Alias,
    string? Address,
    bool IsPrimary,
    LocationSource Source);
