namespace Hakathon.Server.Features.Support;

internal static class SupportLogFixtures
{
    internal static SupportServiceLog[] Create(string company, string caseId, SupportCharge[] charges)
    {
        var account = $"{company}-{caseId}";
        SupportServiceLog Entry(int number, string at, string service, string eventName, string resource, string detail) =>
            new($"{account}-log-{number}", DateTimeOffset.Parse(at, System.Globalization.CultureInfo.InvariantCulture), service, eventName, resource, detail);

        return caseId switch
        {
            "duplicate_charge" =>
            [
                Entry(1, "2026-10-31T08:00:00Z", "Платежі", "payment_captured", charges[0].Id, "Підтверджено списання 399 грн за жовтень, підписка Premium."),
                Entry(2, "2026-10-31T08:00:12Z", "Платежі", "payment_captured", charges[1].Id, "Повторно списано 399 грн за ту саму підписку й розрахунковий період."),
                Entry(3, "2026-10-31T08:00:13Z", "Звірка платежів", "duplicate_payment_detected", account, "Виявлено два різні успішні платежі за один період. Повернень ще немає.")
            ],
            "service_inactive" =>
            [
                Entry(1, "2026-10-31T09:00:00Z", "Платежі", "payment_captured", charges[0].Id, "Платіж 399 грн успішний; оплату підтверджено."),
                Entry(2, "2026-10-31T09:00:01Z", "Підписки", "subscription_active", account, "Підписку оформлено, оплачений період чинний."),
                Entry(3, "2026-10-31T09:00:02Z", "Активація", "activation_failed", account, "Активація не завершилася: тайм-аут черги. Послуга залишилася неактивною.")
            ],
            "no_access" =>
            [
                Entry(1, "2026-10-31T10:00:00Z", "Підписки", "entitlement_valid", account, "Право на послугу чинне, оплачений період не завершився."),
                Entry(2, "2026-10-31T10:01:00Z", "Контроль доступу", "access_denied", account, "Запит до послуги відхилено: права доступу не синхронізовано."),
                Entry(3, "2026-10-31T10:01:01Z", "Безпека", "security_check_passed", account, "Безпекових блокувань немає. Дозволено повторну синхронізацію прав.")
            ],
            "login_failed" =>
            [
                Entry(1, "2026-10-31T11:00:00Z", "Безпека", "account_unblocked", account, "Обліковий запис активний, блокувань і обмежень входу немає."),
                Entry(2, "2026-10-31T11:03:00Z", "Авторизація", "login_succeeded", account, "Після часу заявленого блокування зафіксовано успішний вхід."),
                Entry(3, "2026-10-31T11:03:02Z", "Контроль доступу", "access_check_succeeded", account, "Сеанс дійсний; доступ до послуги успішно перевірено.")
            ],
            "cancel_subscription" =>
            [
                Entry(1, "2026-10-01T08:00:00Z", "Платежі", "payment_captured", charges[0].Id, "Останнє списання 399 грн було до скасування, за поточний оплачений період."),
                Entry(2, "2026-10-20T12:00:00Z", "Підписки", "subscription_cancelled", account, "Скасування виконано. Автоматичне продовження вимкнено."),
                Entry(3, "2026-10-31T08:00:00Z", "Платежі", "renewal_skipped", account, "Наступне списання пропущено через скасування. Нових платежів після скасування немає.")
            ],
            _ => []
        };
    }
}
