namespace Hakathon.Server.Features.Support;

public sealed record SupportCase(string Id, string Title, string Message);
public sealed record SupportCompany(string Id, string Name, string Country, string ProfileLabel, SupportCase[] Cases);
public sealed record SupportInstruction(string Id, string CompanyId, string Title, string Summary, string[] Steps,
    string[] RequiredFields, string Action, string Status, int Version, string Source);
public sealed record SupportCatalog(SupportCompany[] Companies, SupportInstruction[] Instructions);
public sealed record ResolveSupportRequest(string CompanyId, string CaseId, string Message);
public sealed record DraftSupportRequest(string CompanyId, string CaseId, string Message, string Resolution);
public sealed record ApproveSupportRequest(string CompanyId);
public sealed record SupportAccess(string Field, string Label, bool Allowed, string Reason);
public sealed record SupportTimeline(string Step, string Status, string Detail);
public sealed record SupportServiceLog(string Id, DateTimeOffset At, string Service, string Event, string ResourceId, string Detail);
public sealed record SupportVerification(string Status, string Summary, SupportServiceLog[] Logs, string[] ResolutionPath);
public sealed record SupportCharge(string Id, decimal Amount, string Status, string BillingPeriod = "", string ProductId = "");
public sealed record SupportState(string Subscription, string Access, SupportCharge[] Charges);
public sealed record SupportResolution(string Id, string CompanyId, string CaseId, SupportInstruction? Instruction,
    string Status, string Source, string Summary, string Reply, SupportAccess[] DataAccess,
    SupportTimeline[] Timeline, SupportState State, string? DraftId, SupportVerification? Verification = null, string? ModelFailure = null);

public sealed class SupportException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
