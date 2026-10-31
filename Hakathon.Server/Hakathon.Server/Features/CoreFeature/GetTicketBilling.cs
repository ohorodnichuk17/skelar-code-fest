using Carter;
using Hakathon.Server.Database;
using Hakathon.Server.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hakathon.Server.Features.CoreFeature;

public sealed record GetTicketBillingQuery(string TicketId) : IRequest<BillingStateDto>;

public sealed class GetTicketBillingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tickets/{ticketId}/billing",
            async (string ticketId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new GetTicketBillingQuery(ticketId), cancellationToken)));
    }
}

public sealed class GetTicketBillingHandler(DatabaseContext db) : IRequestHandler<GetTicketBillingQuery, BillingStateDto>
{
    public async Task<BillingStateDto> Handle(GetTicketBillingQuery request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException($"Ticket {request.TicketId} not found");
        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == ticket.CustomerId, cancellationToken);
        var charges = await db.Charges.AsNoTracking()
            .Where(c => c.CustomerId == customer.Id)
            .ToListAsync(cancellationToken);

        return BillingMapper.ToDto(customer, charges);
    }
}
