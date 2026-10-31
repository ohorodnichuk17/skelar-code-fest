namespace Hakathon.Server.Features.Support;

internal static class SupportLogVerification
{
    internal static SupportVerification UnmatchedComplaint() => new("unknown",
        "Журнали вибраного прикладу не підтверджують це звернення. Потрібні відповідні докази та перевірка фахівця.",
        [], ["Уточнити обставини саме цього звернення.", "Отримати відповідні журнали без розширення доступу до даних.", "Передати підтверджене рішення фахівцю для підготовки інструкції."]);

    internal static SupportVerification Assess(string caseId, SupportStore.CaseState state)
    {
        var logs = state.Logs;
        bool Has(string eventName) => logs.Any(log => log.Event == eventName);
        bool Paid(string id) => logs.Any(log => log.Event == "payment_captured" && log.ResourceId == id);
        var confirmed = caseId switch
        {
            "duplicate_charge" => state.Charges.Length == 2 && state.Charges.All(charge => Paid(charge.Id)) &&
                state.Charges[0].Id != state.Charges[1].Id && state.Charges[0].Amount > 0 &&
                state.Charges[0].Amount == state.Charges[1].Amount &&
                !string.IsNullOrWhiteSpace(state.Charges[0].BillingPeriod) && state.Charges[0].BillingPeriod == state.Charges[1].BillingPeriod &&
                !string.IsNullOrWhiteSpace(state.Charges[0].ProductId) && state.Charges[0].ProductId == state.Charges[1].ProductId && Has("duplicate_payment_detected"),
            "service_inactive" => state.Charges.Any(charge => Paid(charge.Id)) && Has("subscription_active") && Has("activation_failed"),
            "no_access" => Has("entitlement_valid") && Has("access_denied") && Has("security_check_passed"),
            _ => false
        };
        var cancelled = logs.Where(log => log.Event == "subscription_cancelled").OrderByDescending(log => log.At).FirstOrDefault();
        var contradicted = caseId switch
        {
            "login_failed" => Has("account_unblocked") && Has("login_succeeded") && Has("access_check_succeeded") && !state.SecurityBlocked,
            "cancel_subscription" => cancelled is not null && Has("renewal_skipped") &&
                !logs.Any(log => log.Event == "payment_captured" && log.At > cancelled.At),
            _ => false
        };
        var status = confirmed ? "confirmed" : contradicted ? "not_confirmed" : "unknown";
        var summary = (caseId, status) switch
        {
            ("duplicate_charge", "confirmed") => "Журнали підтверджують два успішні списання за одну послугу й один період.",
            ("service_inactive", "confirmed") => "Оплату підтверджено; журнал активації зафіксував збій після платежу.",
            ("no_access", "confirmed") => "Чинна підписка підтверджена; доступ відхилено через розсинхронізацію прав, блокувань немає.",
            ("login_failed", "not_confirmed") => "Заявлене блокування не підтверджено: є успішний вхід і перевірка доступу; обліковий запис не заблокований.",
            ("cancel_subscription", "not_confirmed") => "Списання після скасування не підтверджено: останній платіж був раніше, наступне списання пропущено.",
            _ => "Наявних журналів недостатньо для підтвердження звернення; потрібна перевірка фахівцем."
        };
        string[] path = (caseId, status) switch
        {
            ("duplicate_charge", "confirmed") => ["Звірити ідентифікатори, суми, послугу та період обох платежів.", "Повернути лише друге списання; перше залишити оплаченим.", "Зафіксувати повернення та повідомити клієнту результат."],
            ("service_inactive", "confirmed") => ["Звірити успішний платіж із чинною підпискою.", "Повторити активацію оплаченої послуги після збою черги.", "Перевірити активний доступ і повідомити клієнту результат."],
            ("no_access", "confirmed") => ["Перевірити чинність підписки та відсутність безпекового блокування.", "Повторно синхронізувати права доступу; не змінювати оплату.", "Перевірити відновлений доступ і повідомити клієнту результат."],
            ("login_failed", "not_confirmed") => ["Зіставити час скарги з успішним входом і станом облікового запису.", "Не запускати розблокування чи скидання пароля без підтвердженої потреби.", "Попросити час помилки та текст повідомлення, якщо проблема повторюється."],
            ("cancel_subscription", "not_confirmed") => ["Зіставити дату останнього платежу з датою скасування.", "Не створювати повернення та не скасовувати підписку повторно.", "Пояснити хронологію; попросити ідентифікатор іншого списання, якщо воно є."],
            _ => ["Уточнити обставини звернення.", "Отримати дозволені докази та підтверджене рішення фахівця.", "Підготувати інструкцію для ручного погодження."]
        };
        return new(status, summary, logs.ToArray(), path);
    }
}
