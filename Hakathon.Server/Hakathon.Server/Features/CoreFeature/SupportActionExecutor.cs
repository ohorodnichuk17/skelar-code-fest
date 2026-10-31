using Hakathon.Server.Database;

namespace Hakathon.Server.Features.CoreFeature;

public static class SupportActionExecutor
{
    public static string? GetExecutionBlockReason(ProposedActionDto action, IReadOnlyList<Charge> customerCharges)
    {
        switch (action.Type)
        {
            case ActionTypes.Refund:
                if (action.ChargeId is null)
                {
                    return "Повернення можливе лише для одного конкретного списання.";
                }
                var charge = customerCharges.SingleOrDefault(c => c.Id == action.ChargeId);
                if (charge is null)
                {
                    return $"Списання {action.ChargeId} не належить цьому клієнту.";
                }
                if (charge.Status == ChargeStatus.Refunded)
                {
                    return $"Списання {charge.Id} вже повернуто.";
                }
                return action.Amount == charge.Amount
                    ? null
                    : $"Сума не збігається зі списанням {charge.Id} (₴{charge.Amount}).";
            case ActionTypes.CancelSubscription:
            case ActionTypes.ResetPassword:
                return null;
            case ActionTypes.ChangePlan:
                return "Новий план не визначено — змініть план вручну.";
            default:
                return "Немає дії для виконання.";
        }
    }

    public static ActionResultDto Execute(ProposedActionDto action, Customer customer, IReadOnlyList<Charge> customerCharges)
    {
        if (GetExecutionBlockReason(action, customerCharges) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        switch (action.Type)
        {
            case ActionTypes.Refund:
                var charge = customerCharges.Single(c => c.Id == action.ChargeId);
                charge.Status = ChargeStatus.Refunded;
                charge.RefundedAt = DateTimeOffset.UtcNow;
                return new ActionResultDto(true, $"Повернено ₴{charge.Amount} за {charge.Id}");
            case ActionTypes.CancelSubscription:
                customer.Plan = $"Скасовано · було {customer.Plan}";
                return new ActionResultDto(true, "Підписку скасовано, автосписання вимкнено");
            default:
                return new ActionResultDto(true, $"Посилання для скидання пароля надіслано на {customer.Email}");
        }
    }
}

public static class BillingMapper
{
    public static BillingStateDto ToDto(Customer customer, IEnumerable<Charge> charges) =>
        new(customer.Name, customer.Plan, charges
            .OrderBy(c => c.ChargedAt)
            .Select(c => new ChargeDto(c.Id, c.Amount, c.Currency, c.ChargedAt, c.Description, c.Status.ToString().ToLowerInvariant()))
            .ToList());
}
