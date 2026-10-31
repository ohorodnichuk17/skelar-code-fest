using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Hakathon.Server.Features.Support;

public sealed class SupportStore
{
    internal readonly SemaphoreSlim Gate = new(1, 1);
    internal readonly Dictionary<string, Tenant> Tenants = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, SupportResolution> Resolutions = new(StringComparer.Ordinal);
    internal readonly Dictionary<string, (string Company, string Case, string Message)> Bindings = new(StringComparer.Ordinal);
    internal static readonly string[] Restricted =
        ["FITPULSE-TAX-SECRET-7319", "+380671112233", "STUDYDESK-TAX-SECRET-8426", "+4915112345678",
            "fitpulse-private-reset-token", "studydesk-private@example.test", "studydesk-demo-reset-token"];

    public SupportStore()
    {
        foreach (var (id, name, country) in new[] { ("fitpulse", "FitPulse", "UA"), ("studydesk", "StudyDesk", "DE") })
        {
            var cases = new[]
            {
                new SupportCase("duplicate_charge", "Подвійне списання", "З мене двічі списали оплату за цей місяць."),
                new SupportCase("service_inactive", "Послуга неактивна", "Я оплатив послугу, але вона досі неактивна."),
                new SupportCase("no_access", "Немає доступу", "Маю чинну підписку, але не можу отримати доступ."),
                new SupportCase("login_failed", "Заявлене блокування входу", "Мій обліковий запис заблокований, не можу увійти до послуги."),
                new SupportCase("cancel_subscription", "Списання після скасування", "Я скасував підписку, але після скасування ви знову списали оплату."),
                new SupportCase("other", "Новий випадок", "Потрібно перенести мій прогрес до іншої групи.")
            };
            var tenant = new Tenant(new SupportCompany(id, name, country, "Демонстраційний профіль доступу", cases));
            foreach (var item in cases)
            {
                tenant.States[item.Id] = new CaseState(id, item.Id);
                if (item.Id == "other") { continue; }
                var (action, fields) = item.Id switch
                {
                    "duplicate_charge" => ("refund_duplicate", new[] { "charges", "caseLogs" }),
                    "service_inactive" => ("activate_service", new[] { "paymentConfirmed", "subscription", "caseLogs" }),
                    "no_access" => ("restore_access", new[] { "entitlementValid", "securityBlocked", "access", "caseLogs" }),
                    "login_failed" => ("request_password_reset", new[] { "securityBlocked", id == "fitpulse" ? "email" : "resetToken", "caseLogs" }),
                    _ => ("cancel_subscription", new[] { "subscription", "caseLogs" })
                };
                tenant.Instructions.Add(new SupportInstruction($"{id}-{item.Id}", id, item.Title,
                    "Перевірити умови та виконати лише дозволену демонстраційну дію.",
                    Steps(item.Id),
                    fields, action, "active", 1, "seed"));
            }
            Tenants.Add(id, tenant);
        }
    }

    private static string[] Steps(string caseId) => caseId switch
    {
        "duplicate_charge" => ["Знайти два оплачені списання за один розрахунковий період.", "Звірити однакову суму та переконатися, що повернення ще не було.", "Повернути лише друге дубльоване списання; перше залишити оплаченим."],
        "service_inactive" => ["Перевірити підтвердження оплати на сервері.", "Переконатися, що підписка чинна, а доступ ще неактивний.", "Активувати доступ лише для оплаченого облікового запису."],
        "no_access" => ["Перевірити чинне право доступу та відсутність безпекового блокування.", "Переконатися, що доступ справді неактивний.", "Відновити доступ без зміни оплати або підписки."],
        "login_failed" => ["Зіставити заявлене блокування з журналом авторизації та перевіркою доступу.", "Якщо є успішний вхід і блокувань немає, не запускати операцій відновлення.", "Попросити час і текст помилки для повторної перевірки; не стверджувати, що клієнт обманює."],
        _ => ["Зіставити час останнього платежу та скасування підписки.", "Перевірити журнал автопродовження: чи був новий платіж після скасування.", "Якщо нового списання немає, пояснити хронологію; не повертати попередній платіж автоматично."]
    };

    // Redact all tenant sentinels, including values echoed by the provider or embedded in user text.
    internal static string Sanitize(string value)
    {
        var result = value.Normalize(NormalizationForm.FormKC);
        foreach (var secret in Restricted)
        {
            result = result.Replace(secret, "[приховано]", StringComparison.OrdinalIgnoreCase);
        }
        result = Regex.Replace(result, @"(?<!\w)\+?\d[\d\s().-]{8,}\d(?!\w)", "[приховано]", RegexOptions.CultureInvariant);
        result = Regex.Replace(result, @"[\w.!#$%&'*+/=?^`{|}~-]+@[\w.-]+\.[a-zA-Z]{2,}", "[приховано]", RegexOptions.CultureInvariant);
        return result;
    }

    internal static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant(), @"\s+", " ");
    internal static string MessageKey(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(value))));

    internal sealed class Tenant(SupportCompany company)
    {
        internal SupportCompany Company = company;
        internal readonly List<SupportInstruction> Instructions = [];
        internal readonly Dictionary<string, CaseState> States = [];
    }

    internal sealed class CaseState(string company, string caseId)
    {
        internal string Subscription = caseId == "cancel_subscription" ? "cancelled" : "active";
        internal string Access = caseId is "no_access" or "service_inactive" ? "inactive" : "active";
        internal bool PaymentConfirmed = true;
        internal bool EntitlementValid = true;
        internal bool SecurityBlocked = false;
        internal bool ResetRequested;
        internal SupportServiceLog[] Logs = SupportLogFixtures.Create(company, caseId, CreateCharges(company, caseId));
        internal Dictionary<string, object> Fields(string company) => new()
        {
            ["charges"] = Charges,
            ["paymentConfirmed"] = PaymentConfirmed,
            ["subscription"] = Subscription,
            ["entitlementValid"] = EntitlementValid,
            ["securityBlocked"] = SecurityBlocked,
            ["access"] = Access,
            ["caseLogs"] = Logs,
            ["taxId"] = company == "fitpulse" ? Restricted[0] : Restricted[2],
            ["phone"] = company == "fitpulse" ? Restricted[1] : Restricted[3],
            ["email"] = company == "fitpulse" ? "fitpulse-demo@example.test" : Restricted[5],
            ["resetToken"] = company == "fitpulse" ? Restricted[4] : "studydesk-demo-reset-token"
        };
        internal SupportCharge[] Charges = CreateCharges(company, caseId);
        private static SupportCharge[] CreateCharges(string company, string caseId) => caseId == "duplicate_charge"
            ? [new($"{company}-{caseId}-1", 399, "paid", "2026-10", $"{company}-subscription"), new($"{company}-{caseId}-2", 399, "paid", "2026-10", $"{company}-subscription")]
            : [new($"{company}-{caseId}-1", 399, "paid", "2026-10", $"{company}-subscription")];
        internal SupportState Snapshot() => new(Subscription, Access, Charges.ToArray());
    }
}
