using System.Diagnostics;
using System.Text.Json;
using Hakathon.Server.Database;
using Hakathon.Server.Features.CoreFeature;
using Hakathon.Server.Options;
using Microsoft.Extensions.Options;

namespace Hakathon.Server.Services;

public sealed record TicketTriageResult(TriageDto Triage, string Reply, string Source, string Detail);

public sealed class TicketTriageService(
    GemmaService gemma,
    FallbackTriageStore fallback,
    IOptions<GemmaOption> options,
    ILogger<TicketTriageService> logger)
{
    public async Task<TicketTriageResult> AnalyzeAsync(
        string ticketId, Customer customer, IReadOnlyList<Charge> charges, string message, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.AI_TimeoutSeconds));
        var stopwatch = Stopwatch.StartNew();

        var prompt = TicketTriagePrompt.Build(customer, charges, message);
        var reason = "timeout";

        for (var attempt = 1; attempt <= 2 && !timeout.IsCancellationRequested; attempt++)
        {
            try
            {
                var text = await gemma.GenerateJsonAsync(prompt, timeout.Token);
                var parsed = JsonSerializer.Deserialize<ModelTriage>(ModelJson.ExtractObject(text), ModelJson.Options);
                if (parsed?.ToDto() is { } triage)
                {
                    fallback.Remember(ticketId, message, triage, parsed.Reply!);
                    return new TicketTriageResult(triage, parsed.Reply!, TriageSources.Gemma, $"Gemma 4 · {stopwatch.ElapsedMilliseconds} мс");
                }
                reason = "invalid_json";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                reason = "timeout";
            }
            catch (GemmaProviderException ex)
            {
                reason = ex.Reason;
            }
            catch (HttpRequestException)
            {
                reason = "network";
            }
            catch (JsonException)
            {
                reason = "invalid_json";
            }
        }

        logger.LogWarning("Gemma triage failed for {TicketId}: {Reason}, using fallback", ticketId, reason);
        var (cached, reply) = fallback.Find(ticketId, message);
        return new TicketTriageResult(cached, reply, TriageSources.Fallback, $"Gemma недоступна ({reason}) — кешований тріаж");
    }

    private sealed record ModelProposedAction(string? Type, string? ChargeId, decimal? Amount);

    private sealed record ModelTriage(
        string? TrueGoal,
        string? Intent,
        string? LiteralTone,
        string? ActualTone,
        bool? Sarcasm,
        string? Urgency,
        List<string>? Facts,
        ModelProposedAction? ProposedAction,
        string? Risk,
        bool? ManipulationAttempt,
        string? Reply)
    {
        private static readonly string[] KnownActions =
            [ActionTypes.None, ActionTypes.Refund, ActionTypes.CancelSubscription, ActionTypes.ChangePlan, ActionTypes.ResetPassword];

        public TriageDto? ToDto()
        {
            var actionType = ProposedAction?.Type?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(TrueGoal) || string.IsNullOrWhiteSpace(Intent) ||
                string.IsNullOrWhiteSpace(ActualTone) || string.IsNullOrWhiteSpace(Reply) ||
                Sarcasm is null || actionType is null || !KnownActions.Contains(actionType))
            {
                return null;
            }

            return new TriageDto(
                TrueGoal.Trim(),
                Intent.Trim(),
                LiteralTone?.Trim().ToLowerInvariant() ?? "neutral",
                ActualTone.Trim().ToLowerInvariant(),
                Sarcasm.Value,
                Urgency?.Trim().ToLowerInvariant() ?? "medium",
                Facts ?? [],
                new ProposedActionDto(actionType, ProposedAction!.ChargeId, ProposedAction.Amount),
                Risk?.Trim().ToLowerInvariant() ?? "medium",
                ManipulationAttempt ?? false);
        }
    }
}
