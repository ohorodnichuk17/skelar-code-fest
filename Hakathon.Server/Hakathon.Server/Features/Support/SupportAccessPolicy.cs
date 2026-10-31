namespace Hakathon.Server.Features.Support;

public static class SupportAccessPolicy
{
    private static readonly string[] Fields =
        ["charges", "paymentConfirmed", "subscription", "entitlementValid", "securityBlocked", "access", "taxId", "phone", "email", "resetToken", "caseLogs"];

    public static bool IsAllowed(string companyId, string country, string action, string field)
    {
        if (!((companyId == "fitpulse" && country == "UA") || (companyId == "studydesk" && country == "DE")))
        {
            return false;
        }
        if (field == "caseLogs")
        {
            return action is "refund_duplicate" or "activate_service" or "restore_access" or "cancel_subscription" or "request_password_reset";
        }
        return action switch
        {
            "refund_duplicate" => field == "charges",
            "activate_service" => field is "paymentConfirmed" or "subscription",
            "restore_access" => field is "entitlementValid" or "securityBlocked" or "access",
            "cancel_subscription" => field == "subscription",
            "request_password_reset" => field == "securityBlocked" ||
                (companyId == "fitpulse" ? field == "email" : field == "resetToken"),
            _ => false
        };
    }

    internal static SupportAccess[] Describe(SupportCompany company, string action) => Fields.Select(field =>
    {
        var allowed = IsAllowed(company.Id, company.Country, action, field);
        return new SupportAccess(field, field, allowed, allowed
            ? "Дозволено профілем компанії для цієї дії." : "Поле не дозволене профілем компанії для цієї дії.");
    }).ToArray();
}
