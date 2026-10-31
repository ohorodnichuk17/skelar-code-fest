using Carter;
using Hakathon.Server.Database;
using Hakathon.Server.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hakathon.Server.Features.CoreFeature;

public sealed record ApprovePendingActionCommand(string ActionId, bool Approve) : IRequest<ApprovePendingActionResponse>;

public sealed class ApprovePendingActionEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/actions/{actionId}/approve",
            async (string actionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ApprovePendingActionCommand(actionId, Approve: true), cancellationToken)));

        app.MapPost("/api/actions/{actionId}/reject",
            async (string actionId, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ApprovePendingActionCommand(actionId, Approve: false), cancellationToken)));
    }
}

public sealed class ApprovePendingActionHandler(DatabaseContext db) : IRequestHandler<ApprovePendingActionCommand, ApprovePendingActionResponse>
{
    public async Task<ApprovePendingActionResponse> Handle(ApprovePendingActionCommand request, CancellationToken cancellationToken)
    {
        var pending = await db.PendingActions.SingleOrDefaultAsync(a => a.Id == request.ActionId, cancellationToken)
            ?? throw new NotFoundException($"Action {request.ActionId} not found");
        if (pending.Status != PendingActionStatus.Pending)
        {
            throw new ConflictException($"Дію {pending.Id} вже розглянуто");
        }

        var ticket = await db.Tickets.SingleAsync(t => t.Id == pending.TicketId, cancellationToken);
        var customer = await db.Customers.SingleAsync(c => c.Id == ticket.CustomerId, cancellationToken);
        var charges = await db.Charges.Where(c => c.CustomerId == customer.Id).ToListAsync(cancellationToken);
        var proposed = new ProposedActionDto(pending.Type, pending.ChargeId, pending.Amount);

        ActionResultDto result;
        string reply;
        if (request.Approve)
        {
            // Стан білінгу міг змінитися після тріажу — інваріанти перевіряємо ще раз.
            if (SupportActionExecutor.GetExecutionBlockReason(proposed, charges) is { } reason)
            {
                throw new ConflictException(reason);
            }
            result = SupportActionExecutor.Execute(proposed, customer, charges);
            pending.Status = PendingActionStatus.Approved;
            reply = $"Готово! {result.Summary}. Дякуємо за терпіння.";
        }
        else
        {
            result = new ActionResultDto(false, "Агент відхилив дію — білінг не змінено");
            pending.Status = PendingActionStatus.Rejected;
            reply = "Ми перевірили ваш запит і не можемо виконати його в такому вигляді. Фахівець зв'яжеться з вами з деталями.";
        }

        pending.ReviewedAt = DateTimeOffset.UtcNow;
        var status = request.Approve ? "approved" : "rejected";
        var reviewEvent = new TimelineEventDto("review", status, result.Summary);
        db.AuditEvents.Add(new AuditEvent
        {
            TicketId = ticket.Id,
            At = pending.ReviewedAt.Value,
            Step = reviewEvent.Step,
            Detail = $"{status} by agent: {pending.Type} ({pending.Rule}) · {result.Summary}"
        });
        await db.SaveChangesAsync(cancellationToken);

        return new ApprovePendingActionResponse(pending.Id, status, result, BillingMapper.ToDto(customer, charges), reviewEvent, reply);
    }
}
