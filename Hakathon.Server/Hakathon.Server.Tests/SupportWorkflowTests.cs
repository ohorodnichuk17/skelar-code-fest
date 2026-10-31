using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using Hakathon.Server.Features.Support;
using Hakathon.Server.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hakathon.Server.Tests;

public sealed class SupportWorkflowTests
{
    [Theory]
    [InlineData("2026-09", "subscription", false)]
    [InlineData("2026-10", "another-product", false)]
    [InlineData("", "subscription", false)]
    [InlineData("2026-10", "subscription", true)]
    public void Duplicate_refund_requires_same_nonempty_period_and_product(string secondPeriod, string secondProduct, bool expected)
    {
        var charges = new[]
        {
            new SupportCharge("charge-1", 399, "paid", "2026-10", "subscription"),
            new SupportCharge("charge-2", 399, "paid", secondPeriod, secondProduct)
        };
        Assert.Equal(expected, SupportActionRules.CanRefundDuplicate(charges));
    }

    [Fact]
    public async Task Catalog_has_two_safe_tenants_and_ten_active_instructions()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Gemma:API_Key", "test-key"));
        var response = await factory.CreateClient().GetAsync("/api/support/catalog");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("companies").GetArrayLength());
        Assert.Equal(10, json.RootElement.GetProperty("instructions").GetArrayLength());
        Assert.DoesNotContain("SECRET", json.RootElement.ToString());
    }

    [Theory]
    [InlineData("duplicate_charge", "refund_duplicate")]
    [InlineData("service_inactive", "activate_service")]
    [InlineData("no_access", "restore_access")]
    public async Task Valid_verdict_executes_the_permitted_demo_action_once(string caseId, string action)
    {
        var handler = new ModelHandler(new { caseId, action, summary = "Перевірено", reply = "Звернення перевірено" });
        using var factory = Factory(handler);
        var client = factory.CreateClient();
        var request = new { companyId = "fitpulse", caseId, message = await SeedMessage(client, caseId) };
        var first = await Read(await client.PostAsJsonAsync("/api/support/resolve", request));
        Assert.Equal("executed", first.Status);
        Assert.Equal("gemma", first.Source);
        switch (caseId)
        {
            case "duplicate_charge": Assert.Single(first.State.Charges, c => c.Status == "refunded"); break;
            case "service_inactive":
            case "no_access": Assert.Equal("active", first.State.Access); break;
            case "cancel_subscription": Assert.Equal("cancelled", first.State.Subscription); break;
        }
        var repeat = await Read(await client.PostAsJsonAsync("/api/support/resolve", request));
        Assert.Equal("blocked", repeat.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/support/resolutions/{first.Id}/approve", new { companyId = "fitpulse" })).StatusCode);
    }

    [Fact]
    public async Task Mismatched_model_classification_cannot_refund_selected_case()
    {
        using var factory = Factory(new ModelHandler(new { caseId = "other", action = "manual_review", summary = "Нове звернення", reply = "Потрібен фахівець" }));
        var client = factory.CreateClient();
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId = "fitpulse", caseId = "duplicate_charge", message = "Хочу перенести історію тренувань." }));
        Assert.Equal("needs_instruction", result.Status);
        Assert.Equal("unknown", result.Verification!.Status);
        Assert.Empty(result.Verification.Logs);
        Assert.All(result.State.Charges, c => Assert.Equal("paid", c.Status));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/support/resolutions/{result.Id}/approve", new { companyId = "fitpulse" })).StatusCode);
    }

    [Fact]
    public async Task Valid_model_semantic_match_accepts_paraphrase_without_fallback_keyword_match()
    {
        using var factory = Factory(new ModelHandler(new { caseId = "service_inactive", action = "activate_service", summary = "Перевірено", reply = "Перевірено" }));
        var result = await Read(await factory.CreateClient().PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = "fitpulse", caseId = "service_inactive", message = "Оплатив, але сервіс не підключено"
        }));
        Assert.Equal("executed", result.Status);
        Assert.Equal("active", result.State.Access);
    }

    [Fact]
    public async Task Successful_provider_response_with_markdown_json_is_not_reported_unavailable()
    {
        using var factory = Factory(new ModelHandler(new
        {
            caseId = "service_inactive", action = "activate_service", summary = "Оплата підтверджена", reply = "Потрібна активація"
        }) { WrapJson = true });
        var result = await Read(await factory.CreateClient().PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = "fitpulse", caseId = "service_inactive", message = "Оплатив, але сервіс не підключено"
        }));
        Assert.Equal("gemma", result.Source);
        Assert.Equal("executed", result.Status);
    }

    [Fact]
    public async Task Successful_provider_with_wrong_schema_reports_schema_failure_not_unavailability()
    {
        using var factory = Factory(new ModelHandler(new { unexpected = "value" }));
        var result = await Read(await factory.CreateClient().PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = "fitpulse", caseId = "duplicate_charge", message = "З мене двічі списали оплату за цей місяць."
        }));
        Assert.Equal("fallback", result.Source);
        Assert.Equal("invalid_schema", result.ModelFailure);
        Assert.Contains("структура рішення", result.Summary);
        Assert.DoesNotContain("недоступна", result.Summary);
    }

    [Fact]
    public async Task Output_and_input_are_redacted_and_projected_fields_follow_action()
    {
        const string secret = "FITPULSE-TAX-SECRET-7319";
        var handler = new ModelHandler(new { caseId = "login_failed", action = "request_password_reset", summary = secret, reply = "+380671112233 private@example.test" });
        using var factory = Factory(handler);
        var client = factory.CreateClient();
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId = "studydesk", caseId = "login_failed", message = $"Не можу увійти {secret} +4915112345678 private@example.test" }));
        var output = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(secret, output);
        Assert.DoesNotContain("+380671112233", output);
        Assert.DoesNotContain("private@example.test", output);
        using var envelope = JsonDocument.Parse(handler.Request!);
        var prompt = envelope.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        Assert.DoesNotContain(secret, prompt);
        Assert.DoesNotContain("private@example.test", prompt);
        Assert.DoesNotContain("STUDYDESK-TAX-SECRET-8426", prompt);
        using var context = JsonDocument.Parse(prompt[(prompt.IndexOf("Context: ", StringComparison.Ordinal) + 9)..]);
        var fields = context.RootElement.GetProperty("fields");
        Assert.True(fields.TryGetProperty("resetToken", out _));
        Assert.True(fields.TryGetProperty("securityBlocked", out _));
        Assert.False(fields.TryGetProperty("email", out _));
        Assert.False(fields.TryGetProperty("phone", out _));
        Assert.False(fields.TryGetProperty("taxId", out _));
        Assert.True(fields.TryGetProperty("caseLogs", out _));
        Assert.Equal(3, fields.EnumerateObject().Count());
    }

    [Theory]
    [InlineData("fitpulse")]
    [InlineData("studydesk")]
    public async Task Five_complaints_have_three_log_confirmations_and_two_unconfirmed_claims(string companyId)
    {
        using var factory = Factory(new ModelHandler(null));
        var client = factory.CreateClient();
        var catalog = (await client.GetFromJsonAsync<SupportCatalog>("/api/support/catalog"))!;
        var cases = catalog.Companies.Single(c => c.Id == companyId).Cases.Where(c => c.Id != "other").ToArray();
        var results = new List<SupportResolution>();
        foreach (var item in cases)
        {
            results.Add(await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId, caseId = item.Id, item.Message })));
        }

        Assert.Equal(5, results.Count);
        Assert.Equal(3, results.Count(r => r.Verification!.Status == "confirmed"));
        Assert.Equal(2, results.Count(r => r.Verification!.Status == "not_confirmed"));
        Assert.All(results, result =>
        {
            Assert.True(result.Verification!.Logs.Length >= 3);
            Assert.All(result.Verification.Logs, log => Assert.StartsWith(companyId + "-", log.Id));
            Assert.True(result.Verification.ResolutionPath.Length >= 3);
            Assert.NotEqual("executed", result.Status);
        });
        Assert.Equal(3, results.Where(r => r.Verification!.Status == "confirmed")
            .Select(r => string.Join(";", r.Verification!.ResolutionPath)).Distinct().Count());
        foreach (var result in results.Where(r => r.Verification!.Status == "not_confirmed"))
        {
            Assert.Equal("blocked", result.Status);
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/support/resolutions/{result.Id}/approve", new { companyId })).StatusCode);
        }
    }

    [Theory]
    [InlineData("login_failed", "request_password_reset")]
    [InlineData("cancel_subscription", "cancel_subscription")]
    public async Task Unconfirmed_logs_block_even_a_positive_model_verdict(string caseId, string action)
    {
        using var factory = Factory(new ModelHandler(new { caseId, action, summary = "Скаргу підтверджено", reply = "Операцію виконано" }));
        var client = factory.CreateClient();
        var first = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = "fitpulse", caseId, message = await SeedMessage(client, caseId)
        }));
        Assert.Equal("gemma", first.Source);
        Assert.Equal("blocked", first.Status);
        Assert.Equal("not_confirmed", first.Verification!.Status);
        Assert.DoesNotContain("Операцію виконано", first.Reply);
        Assert.All(first.State.Charges, charge => Assert.Equal("paid", charge.Status));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/support/resolutions/{first.Id}/approve", new { companyId = "fitpulse" })).StatusCode);
    }

    [Fact]
    public async Task Valid_generated_draft_is_manual_only_and_matches_exact_normalized_message()
    {
        using var factory = Factory(new ModelHandler(new { title = "Перенесення", summary = "Ручна перевірка профілів", steps = new[] { "Перевірити володіння профілями.", "Оператор переносить прогрес вручну." }, action = "manual_review" }));
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/support/instructions/drafts", new { companyId = "fitpulse", caseId = "other", message = "Перенести  прогрес", resolution = "Фахівець перевірив володіння профілями." });
        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<SupportInstruction>())!;
        Assert.Equal("gemma", draft.Source);
        Assert.Equal("manual_review", draft.Action);
        Assert.Empty(draft.RequiredFields);
        (await client.PostAsJsonAsync($"/api/support/instructions/{draft.Id}/approve", new { companyId = "fitpulse" })).EnsureSuccessStatusCode();
        var reused = await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId = "fitpulse", caseId = "other", message = "  ПЕРЕНЕСТИ прогрес " }));
        Assert.Equal(draft.Id, reused.Instruction!.Id);
        Assert.Equal("needs_approval", reused.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/support/resolutions/{reused.Id}/approve", new { companyId = "fitpulse" })).StatusCode);
        var different = await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId = "fitpulse", caseId = "other", message = "Перенести прогрес до іншої особи" }));
        Assert.Null(different.Instruction);
    }

    [Fact]
    public async Task Edited_preset_novel_complaint_can_create_approve_and_reuse_manual_draft()
    {
        using var factory = Factory(new ModelHandler(new { caseId = "other", action = "manual_review", summary = "Нова проблема", reply = "Розгляд фахівцем" }));
        var client = factory.CreateClient();
        var request = new { companyId = "fitpulse", caseId = "duplicate_charge", message = "Перенести прогрес до іншої групи" };
        var before = await Read(await client.PostAsJsonAsync("/api/support/resolve", request));
        Assert.Equal("needs_instruction", before.Status);
        Assert.Null(before.Instruction);
        var created = await client.PostAsJsonAsync("/api/support/instructions/drafts", new
        {
            request.companyId, request.caseId, request.message, resolution = "Фахівець перевіряє профіль та переносить прогрес вручну."
        });
        created.EnsureSuccessStatusCode();
        var draft = (await created.Content.ReadFromJsonAsync<SupportInstruction>())!;
        Assert.Equal("manual_review", draft.Action);
        (await client.PostAsJsonAsync($"/api/support/instructions/{draft.Id}/approve", new { request.companyId })).EnsureSuccessStatusCode();
        var reused = await Read(await client.PostAsJsonAsync("/api/support/resolve", request));
        Assert.Equal(draft.Id, reused.Instruction!.Id);
        Assert.Equal("needs_approval", reused.Status);
        Assert.All(reused.State.Charges, c => Assert.Equal("paid", c.Status));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/support/resolutions/{reused.Id}/approve", new { request.companyId })).StatusCode);
    }

    [Fact]
    public async Task Unavailable_model_with_unrelated_message_cannot_be_manually_approved()
    {
        using var factory = Factory(new ModelHandler(null));
        var client = factory.CreateClient();
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId = "fitpulse", caseId = "duplicate_charge", message = "Змініть колір мого профілю" }));
        Assert.Equal("fallback", result.Source);
        Assert.Equal("needs_instruction", result.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/support/resolutions/{result.Id}/approve", new { companyId = "fitpulse" })).StatusCode);
    }

    [Fact]
    public async Task Provider_timeout_is_bounded_and_does_not_execute()
    {
        using var factory = Factory(new ModelHandler(null) { Stall = true });
        var client = factory.CreateClient();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = "fitpulse", caseId = "duplicate_charge", message = "З мене двічі списали оплату за цей місяць."
        }));
        Assert.Equal("fallback", result.Source);
        Assert.Equal("needs_approval", result.Status);
        Assert.Equal("timeout", result.ModelFailure);
        Assert.All(result.State.Charges, c => Assert.Equal("paid", c.Status));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("login_failed", "request_password_reset", "blocked")]
    [InlineData("duplicate_charge", "refund_duplicate", "needs_instruction")]
    public async Task Model_cannot_claim_email_delivery_or_completed_refund_in_outcome_text(string caseId, string action, string expected)
    {
        using var factory = Factory(new ModelHandler(new { caseId = expected != "needs_instruction" ? caseId : "other", action = expected != "needs_instruction" ? action : "manual_review", summary = "Лист надіслано. Гроші повернуто.", reply = "Лист надіслано. Гроші повернуто." }));
        var client = factory.CreateClient();
        var message = caseId == "login_failed" ? await SeedMessage(client, caseId) : "Змініть колір профілю";
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId = "fitpulse", caseId, message }));
        Assert.Equal(expected, result.Status);
        Assert.DoesNotContain("Лист надіслано", result.Summary + result.Reply);
        Assert.DoesNotContain("Гроші повернуто", result.Summary + result.Reply);
        if (caseId == "login_failed") { Assert.Contains("Операцій не виконано", result.Summary); }
    }

    [Fact]
    public async Task Concurrent_resolutions_execute_only_one_refund_and_leave_other_tenant_intact()
    {
        using var factory = Factory(new ModelHandler(new { caseId = "duplicate_charge", action = "refund_duplicate", summary = "Перевірено", reply = "Перевірено" }));
        var client = factory.CreateClient();
        var request = new { companyId = "fitpulse", caseId = "duplicate_charge", message = "З мене двічі списали оплату за цей місяць." };
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/support/resolve", request), client.PostAsJsonAsync("/api/support/resolve", request));
        var resolutions = await Task.WhenAll(responses.Select(Read));
        Assert.Single(resolutions, r => r.Status == "executed");
        Assert.Single(resolutions, r => r.Status == "blocked");
        Assert.All(resolutions, r => Assert.Single(r.State.Charges, c => c.Status == "refunded"));
        var other = await Read(await client.PostAsJsonAsync("/api/support/resolve", new { companyId = "studydesk", request.caseId, request.message }));
        Assert.Equal("executed", other.Status);
        Assert.All(other.State.Charges, c => Assert.StartsWith("studydesk-", c.Id));
    }

    private static async Task<string> SeedMessage(HttpClient client, string caseId)
    {
        var catalog = (await client.GetFromJsonAsync<SupportCatalog>("/api/support/catalog"))!;
        return catalog.Companies.Single(c => c.Id == "fitpulse").Cases.Single(c => c.Id == caseId).Message;
    }

    private static async Task<SupportResolution> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupportResolution>())!;
    }

    private static WebApplicationFactory<Program> Factory(ModelHandler handler) => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseSetting("Gemma:API_Key", "test-key");
        b.UseSetting("Gemma:AI_TimeoutSeconds", "0.1");
        b.ConfigureTestServices(s => s.AddHttpClient<GemmaService>().ConfigurePrimaryHttpMessageHandler(() => handler));
    });

    private sealed class ModelHandler(object? model) : HttpMessageHandler
    {
        internal string? Request;
        internal bool Stall;
        internal bool WrapJson;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (Stall) { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            if (model is null) { return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable); }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = WrapJson ? "```json\n" + JsonSerializer.Serialize(model) + "\n```" : JsonSerializer.Serialize(model) } } } } } }), Encoding.UTF8, "application/json")
            };
        }
    }
}
