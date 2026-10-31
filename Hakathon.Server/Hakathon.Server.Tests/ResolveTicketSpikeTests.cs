using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Hakathon.Server.Features.CoreFeature;
using Hakathon.Server.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hakathon.Server.Tests;

public class ResolveTicketSpikeTests
{
    private const string SarcasticDoubleCharge = "Чудово, ви знову зняли з мене гроші двічі. Обожнюю ваш сервіс 🙂";

    [Fact]
    public async Task HappyPath_SarcasticDoubleCharge_ReturnsTriageContractAndRefundsDuplicate()
    {
        var gemma = new StubGemmaHandler(GemmaEnvelope("""
            {
              "trueGoal": "Повернути подвійне списання за жовтень",
              "intent": "refund_duplicate_charge",
              "literalTone": "positive",
              "actualTone": "angry",
              "sarcasm": true,
              "urgency": "high",
              "facts": ["CH-1002 і CH-1003: два списання по ₴399 за жовтень з різницею 1 хв"],
              "proposedAction": { "type": "refund", "chargeId": "CH-1003", "amount": 399 },
              "risk": "low",
              "manipulationAttempt": false,
              "reply": "Вибачте, ви праві — ми списали ₴399 двічі. Повертаємо зайвий платіж."
            }
            """));
        using var factory = CreateFactory(gemma, timeoutSeconds: 4);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/tickets/T-1001/messages", new ResolveTicketRequest(SarcasticDoubleCharge));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ResolveTicketResponse>())!;
        Assert.Equal(TriageSources.Gemma, body.Source);
        Assert.True(body.Triage.Sarcasm);
        Assert.Equal("angry", body.Triage.ActualTone);
        Assert.Equal(ActionTypes.Refund, body.Triage.ProposedAction.Type);
        Assert.Equal(PolicyDecisionStatuses.Auto, body.Decision.Status);
        Assert.True(body.Action.Executed);
        Assert.Equal("refunded", body.Billing.Charges.Single(c => c.Id == "CH-1003").Status);
        Assert.Equal("paid", body.Billing.Charges.Single(c => c.Id == "CH-1002").Status);
        Assert.Equal(["message", "triage", "policy", "action", "audit"], body.Timeline.Select(t => t.Step));
        Assert.False(string.IsNullOrWhiteSpace(body.Reply));
        Assert.Equal("test-key", gemma.LastApiKeyHeader);
    }

    [Fact]
    public async Task ProviderTimeout_ReturnsDeterministicFallbackAndDoesNotTouchBilling()
    {
        var gemma = new StubGemmaHandler(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new UnreachableException();
        });
        using var factory = CreateFactory(gemma, timeoutSeconds: 0.3);

        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/tickets/T-1001/messages", new ResolveTicketRequest(SarcasticDoubleCharge));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ResolveTicketResponse>())!;
        Assert.Equal(TriageSources.Fallback, body.Source);
        Assert.True(body.Triage.Sarcasm);
        Assert.Equal(PolicyDecisionStatuses.NeedsApproval, body.Decision.Status);
        Assert.Equal("provider_unavailable", body.Decision.Rule);
        Assert.False(body.Action.Executed);
        Assert.All(body.Billing.Charges, c => Assert.Equal("paid", c.Status));
    }

    [Fact]
    public async Task HumanApproval_ExecutesPendingRefundOnce()
    {
        var gemma = new StubGemmaHandler(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new UnreachableException();
        });
        using var factory = CreateFactory(gemma, timeoutSeconds: 0.3);
        var client = factory.CreateClient();

        var resolved = (await (await client.PostAsJsonAsync(
            "/api/tickets/T-1001/messages", new ResolveTicketRequest(SarcasticDoubleCharge)))
            .Content.ReadFromJsonAsync<ResolveTicketResponse>())!;
        Assert.NotNull(resolved.PendingAction);
        Assert.True(resolved.PendingAction.CanApprove);

        var approve = await client.PostAsync($"/api/actions/{resolved.PendingAction.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var approved = (await approve.Content.ReadFromJsonAsync<ApprovePendingActionResponse>())!;
        Assert.Equal("approved", approved.Status);
        Assert.True(approved.Action.Executed);
        Assert.Equal("refunded", approved.Billing.Charges.Single(c => c.Id == "CH-1003").Status);

        var secondApprove = await client.PostAsync($"/api/actions/{resolved.PendingAction.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(StubGemmaHandler gemma, double timeoutSeconds) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Gemma:API_Key", "test-key");
            builder.UseSetting("Gemma:AI_TimeoutSeconds", timeoutSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("InMemoryDbName", Guid.NewGuid().ToString());
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<GemmaService>().ConfigurePrimaryHttpMessageHandler(() => gemma));
        });

    private static Func<CancellationToken, Task<HttpResponseMessage>> GemmaEnvelope(string modelJson) => _ =>
    {
        var envelope = new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        role = "model",
                        parts = new object[] { new { text = "", thought = true }, new { text = modelJson } }
                    },
                    finishReason = "STOP"
                }
            }
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(envelope), Encoding.UTF8, "application/json")
        });
    };

    private sealed class StubGemmaHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public string? LastApiKeyHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastApiKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var values) ? values.Single() : null;
            return respond(cancellationToken);
        }
    }

    private sealed class UnreachableException : Exception;
}
