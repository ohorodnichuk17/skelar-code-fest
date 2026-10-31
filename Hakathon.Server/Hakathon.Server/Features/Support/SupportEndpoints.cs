using Carter;

namespace Hakathon.Server.Features.Support;

public sealed class SupportEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/support").WithTags("Support");
        group.MapGet("/catalog", (SupportWorkflow workflow, CancellationToken ct) => Run(() => workflow.CatalogAsync(ct)));
        group.MapPost("/resolve", (ResolveSupportRequest body, SupportWorkflow workflow, CancellationToken ct) => Run(() => workflow.ResolveAsync(body, ct)));
        group.MapPost("/instructions/drafts", (DraftSupportRequest body, SupportWorkflow workflow, CancellationToken ct) => Run(() => workflow.DraftAsync(body, ct)));
        group.MapPost("/instructions/{id}/approve", (string id, ApproveSupportRequest body, SupportWorkflow workflow, CancellationToken ct) => Run(() => workflow.ApproveInstructionAsync(id, body.CompanyId, ct)));
        group.MapPost("/resolutions/{id}/approve", (string id, ApproveSupportRequest body, SupportWorkflow workflow, CancellationToken ct) => Run(() => workflow.ApproveResolutionAsync(id, body.CompanyId, ct)));
    }

    private static async Task<IResult> Run<T>(Func<Task<T>> action)
    {
        try { return Results.Ok(await action()); }
        catch (SupportException ex) { return Results.Problem(statusCode: ex.StatusCode, title: ex.Message); }
    }
}
