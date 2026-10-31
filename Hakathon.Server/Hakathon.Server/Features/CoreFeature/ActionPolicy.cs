using Hakathon.Server.Database;

namespace Hakathon.Server.Features.CoreFeature;

public static class ActionPolicy
{
    public const decimal AutoRefundLimit = 500;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromHours(24);

    public static PolicyDecisionDto Assess(TriageDto triage, string source, IReadOnlyList<Charge> customerCharges)
    {
        var action = triage.ProposedAction;

        if (triage.ManipulationAttempt)
        {
            return Blocked("manipulation_detected", "Запит намагається обійти правила або охоплює кілька платежів.");
        }

        if (action.Type == ActionTypes.None)
        {
            return source == TriageSources.Fallback
                ? NeedsApproval("provider_unavailable", "AI недоступний — звернення передано людині.")
                : new PolicyDecisionDto(PolicyDecisionStatuses.NoAction, "nothing_to_execute", "Дія не потрібна — достатньо відповіді.");
        }

        if (source == TriageSources.Fallback)
        {
            return NeedsApproval("provider_unavailable", "Тріаж отримано з кешу, а не від Gemma — автоматичне виконання заборонене.");
        }

        if (action.Type != ActionTypes.Refund)
        {
            return NeedsApproval("manual_only_in_mvp", "Цю дію в MVP виконує лише людина.");
        }

        if (action.ChargeId is null)
        {
            return Blocked("no_specific_charge", "Повернення можливе лише для одного конкретного списання.");
        }

        var charge = customerCharges.SingleOrDefault(c => c.Id == action.ChargeId);
        if (charge is null)
        {
            return Blocked("charge_ownership", $"Списання {action.ChargeId} не належить цьому клієнту.");
        }

        if (charge.Status == ChargeStatus.Refunded)
        {
            return Blocked("already_refunded", $"Списання {charge.Id} вже повернуто.");
        }

        if (action.Amount != charge.Amount)
        {
            return Blocked("amount_mismatch", $"Сума {action.Amount} не збігається зі списанням {charge.Id} (₴{charge.Amount}).");
        }

        if (charge.Amount > AutoRefundLimit)
        {
            return NeedsApproval("refund_limit", $"₴{charge.Amount} перевищує ліміт автоповернення ₴{AutoRefundLimit}.");
        }

        var twin = customerCharges.FirstOrDefault(c =>
            c.Id != charge.Id &&
            c.Amount == charge.Amount &&
            c.Description == charge.Description &&
            (c.ChargedAt - charge.ChargedAt).Duration() <= DuplicateWindow);
        if (twin is null)
        {
            return NeedsApproval("not_a_duplicate", $"Для {charge.Id} не знайдено дубля — потрібне рішення людини.");
        }

        if (twin.Status == ChargeStatus.Refunded)
        {
            return Blocked("already_refunded", $"Дубль {twin.Id} уже повернуто — друге повернення заборонене.");
        }

        return new PolicyDecisionDto(PolicyDecisionStatuses.Auto, "duplicate_charge_refund",
            $"{charge.Id} дублює {twin.Id}: та сама сума, той самий період, ₴{charge.Amount} ≤ ліміту ₴{AutoRefundLimit}.");
    }

    private static PolicyDecisionDto NeedsApproval(string rule, string reason) => new(PolicyDecisionStatuses.NeedsApproval, rule, reason);

    private static PolicyDecisionDto Blocked(string rule, string reason) => new(PolicyDecisionStatuses.Blocked, rule, reason);
}
