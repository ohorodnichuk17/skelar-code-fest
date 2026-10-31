namespace Hakathon.Server.Features.Support;

public static class SupportActionRules
{
    public static bool CanRefundDuplicate(IReadOnlyList<SupportCharge> charges) =>
        charges.Count == 2 && charges.All(c => c.Status == "paid") &&
        charges[0].Id != charges[1].Id && charges[0].Amount > 0 && charges[0].Amount == charges[1].Amount &&
        !string.IsNullOrWhiteSpace(charges[0].BillingPeriod) && charges[0].BillingPeriod == charges[1].BillingPeriod &&
        !string.IsNullOrWhiteSpace(charges[0].ProductId) && charges[0].ProductId == charges[1].ProductId;
}
