using Hakathon.Server.Database;
using Hakathon.Server.Exceptions;
using Hakathon.Server.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Hakathon.Server.Features.CoreFeature;

public sealed class ResolveTicketHandler(DatabaseContext db, TicketTriageService ticketTriageService)
    : IRequestHandler<ResolveTicketCommand, ResolveTicketResponse>
{
    public async Task<ResolveTicketResponse> Handle(ResolveTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == request.TicketId, cancellationToken)
            ?? throw new NotFoundException($"Ticket {request.TicketId} not found");
        var customer = await db.Customers.SingleAsync(c => c.Id == ticket.CustomerId, cancellationToken);
        var charges = await db.Charges
            .Where(c => c.CustomerId == customer.Id)
            .OrderBy(c => c.ChargedAt)
            .ToListAsync(cancellationToken);

        var timeline = new List<TimelineEventDto>
        {
            new("message", "received", $"Тікет {ticket.Id} · {customer.Name}")
        };

        var triage = await ticketTriageService.AnalyzeAsync(ticket.Id, customer, charges, request.Message, cancellationToken);
        timeline.Add(new("triage", triage.Source == TriageSources.Gemma ? "ok" : "fallback", triage.Detail));

        var decision = ActionPolicy.Assess(triage.Triage, triage.Source, charges);
        timeline.Add(new("policy", decision.Status, decision.Rule));

        var proposed = triage.Triage.ProposedAction;
        var action = decision.Status switch
        {
            PolicyDecisionStatuses.Auto => SupportActionExecutor.Execute(proposed, customer, charges),
            PolicyDecisionStatuses.NoAction => new ActionResultDto(false, "Дія не потрібна"),
            _ => new ActionResultDto(false, "Не виконано — чекає на рішення людини")
        };
        timeline.Add(new("action", action.Executed ? "done" : "skipped", action.Summary));

        var pending = CreatePendingAction(ticket.Id, decision, proposed, charges);

        var now = DateTimeOffset.UtcNow;
        db.AuditEvents.AddRange(timeline.Select(e => new AuditEvent
        {
            TicketId = ticket.Id,
            At = now,
            Step = e.Step,
            Detail = $"{e.Status}: {e.Detail}"
        }));
        await db.SaveChangesAsync(cancellationToken);
        timeline.Add(new("audit", "recorded", $"{timeline.Count} подій записано · {decision.Rule}"));

        return new ResolveTicketResponse(
            ticket.Id,
            triage.Source,
            triage.Triage,
            decision,
            action,
            BillingMapper.ToDto(customer, charges),
            timeline,
            ReplyFor(decision, action, triage.Reply),
            pending);
    }

    private PendingActionDto? CreatePendingAction(
        string ticketId, PolicyDecisionDto decision, ProposedActionDto proposed, IReadOnlyList<Charge> charges)
    {
        if (decision.Status is PolicyDecisionStatuses.Auto or PolicyDecisionStatuses.NoAction || proposed.Type == ActionTypes.None)
        {
            return null;
        }

        var entity = new PendingAction
        {
            Id = $"A-{Guid.NewGuid():N}"[..10],
            TicketId = ticketId,
            Type = proposed.Type,
            ChargeId = proposed.ChargeId,
            Amount = proposed.Amount,
            Rule = decision.Rule,
            Status = PendingActionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.PendingActions.Add(entity);

        var cannotApprove = SupportActionExecutor.GetExecutionBlockReason(proposed, charges);
        return new PendingActionDto(entity.Id, cannotApprove is null, cannotApprove);
    }

    private static string ReplyFor(PolicyDecisionDto decision, ActionResultDto action, string modelReply) =>
            action.Executed || decision.Status == PolicyDecisionStatuses.NoAction
            ? modelReply
            : "Дякуємо за звернення! Ми передали його фахівцю — він перевірить деталі й відповість найближчим часом.";
}
