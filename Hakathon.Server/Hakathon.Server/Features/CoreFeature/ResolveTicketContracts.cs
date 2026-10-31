using MediatR;

namespace Hakathon.Server.Features.CoreFeature;

public sealed record ResolveTicketRequest(string Message);

public sealed record ResolveTicketCommand(string TicketId, string Message) : IRequest<ResolveTicketResponse>;

public sealed record ResolveTicketResponse(
    string TicketId,
    string Source,
    TriageDto Triage,
    PolicyDecisionDto Decision,
    ActionResultDto Action,
    BillingStateDto Billing,
    IReadOnlyList<TimelineEventDto> Timeline,
    string Reply,
    PendingActionDto? PendingAction = null);

// Дія, яка чекає на людину. CanApprove = false, якщо її неможливо виконати навіть з погодженням (напр. «поверни все»).
public sealed record PendingActionDto(string Id, bool CanApprove, string? CannotApproveReason);

public sealed record ApprovePendingActionResponse(
    string ActionId,
    string Status,
    ActionResultDto Action,
    BillingStateDto Billing,
    TimelineEventDto Event,
    string Reply);

public sealed record TriageDto(
    string TrueGoal,
    string Intent,
    string LiteralTone,
    string ActualTone,
    bool Sarcasm,
    string Urgency,
    IReadOnlyList<string> Facts,
    ProposedActionDto ProposedAction,
    string Risk,
    bool ManipulationAttempt);

public sealed record ProposedActionDto(string Type, string? ChargeId, decimal? Amount);

public sealed record PolicyDecisionDto(string Status, string Rule, string Reason);

public sealed record ActionResultDto(bool Executed, string Summary);

public sealed record BillingStateDto(string CustomerName, string Plan, IReadOnlyList<ChargeDto> Charges);

public sealed record ChargeDto(
    string Id,
    decimal Amount,
    string Currency,
    DateTimeOffset ChargedAt,
    string Description,
    string Status);

public sealed record TimelineEventDto(string Step, string Status, string Detail);

public static class TriageSources
{
    public const string Gemma = "gemma";
    public const string Fallback = "fallback";
}

public static class ActionTypes
{
    public const string None = "none";
    public const string Refund = "refund";
    public const string CancelSubscription = "cancel_subscription";
    public const string ChangePlan = "change_plan";
    public const string ResetPassword = "reset_password";
}

public static class PolicyDecisionStatuses
{
    public const string Auto = "auto";
    public const string NeedsApproval = "needs_approval";
    public const string Blocked = "blocked";
    public const string NoAction = "no_action";
}
