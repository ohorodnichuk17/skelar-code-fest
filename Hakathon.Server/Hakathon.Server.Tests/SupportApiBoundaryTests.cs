using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Hakathon.Server.Features.Support;
using Hakathon.Server.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hakathon.Server.Tests;

public sealed class SupportApiBoundaryTests
{
    [Theory]
    [InlineData("unknown", "UA", "charges")]
    [InlineData("fitpulse", "unknown", "charges")]
    [InlineData("fitpulse", "DE", "charges")]
    [InlineData("fitpulse", "UA", "unknown")]
    [InlineData("fitpulse", "UA", "taxId")]
    [InlineData("fitpulse", "UA", "phone")]
    [InlineData("studydesk", "DE", "taxId")]
    [InlineData("studydesk", "DE", "phone")]
    public void UnknownOrRestrictedField_IsDenied(string company, string country, string field)
    {
        Assert.False(SupportAccessPolicy.IsAllowed(company, country, "refund_duplicate", field));
    }

    [Fact]
    public void PasswordReset_UsesDifferentCompanyProfilesAndDeniesUnknownAction()
    {
        Assert.True(SupportAccessPolicy.IsAllowed("fitpulse", "UA", "request_password_reset", "email"));
        Assert.False(SupportAccessPolicy.IsAllowed("studydesk", "DE", "request_password_reset", "email"));
        Assert.True(SupportAccessPolicy.IsAllowed("studydesk", "DE", "request_password_reset", "resetToken"));
        Assert.False(SupportAccessPolicy.IsAllowed("fitpulse", "UA", "request_password_reset", "resetToken"));
        Assert.False(SupportAccessPolicy.IsAllowed("fitpulse", "UA", "arbitrary_sql", "charges"));
    }

    [Fact]
    public async Task Catalog_ContainsTwoCompaniesAndApprovedProcedures()
    {
        using var factory = CreateFactory(new UnavailableGemma());
        var catalog = await Read(await factory.CreateClient().GetAsync("/api/support/catalog"));

        Assert.Equal(2, catalog["companies"]!.AsArray().Count);
        foreach (var company in catalog["companies"]!.AsArray())
        {
            Assert.True(company!["cases"]!.AsArray().Count >= 6);
            Assert.False(string.IsNullOrWhiteSpace(company["profileLabel"]!.GetValue<string>()));
            var instructions = catalog["instructions"]!.AsArray()
                .Where(i => Text(i!, "companyId") == Text(company, "id")).ToArray();
            Assert.True(instructions.Length >= 5);
            Assert.All(instructions, i => Assert.Equal("active", Text(i!, "status")));
        }
    }

    [Fact]
    public async Task UnknownCompanyOrCase_IsRejectedBeforeModelRequest()
    {
        var gemma = new UnavailableGemma();
        using var factory = CreateFactory(gemma);
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var company = catalog["companies"]![0]!;

        var unknownCompany = await client.PostAsJsonAsync("/api/support/resolve",
            new { companyId = "missing", caseId = Text(company["cases"]![0]!, "id"), message = "Потрібна допомога" });
        var unknownCase = await client.PostAsJsonAsync("/api/support/resolve",
            new { companyId = Text(company, "id"), caseId = "missing", message = "Потрібна допомога" });

        Assert.Equal(HttpStatusCode.NotFound, unknownCompany.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownCase.StatusCode);
        Assert.Equal(0, gemma.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task MissingMessage_IsBadRequest(string? message)
    {
        using var factory = CreateFactory(new UnavailableGemma());
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var company = catalog["companies"]![0]!;
        var response = await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = Text(company, "id"), caseId = Text(company["cases"]![0]!, "id"), message
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ModelUnavailable_DoesNotExecuteOrRefund()
    {
        using var factory = CreateFactory(new UnavailableGemma());
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var company = catalog["companies"]![0]!;
        var supportCase = company["cases"]![0]!;
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = Text(company, "id"), caseId = Text(supportCase, "id"), message = Text(supportCase, "message")
        }));

        Assert.Equal("fallback", Text(result, "source"));
        Assert.NotEqual("executed", Text(result, "status"));
        Assert.DoesNotContain(result["state"]!["charges"]!.AsArray(), c => Text(c!, "status") == "refunded");
        Assert.Contains(result["dataAccess"]!.AsArray(), f => f!["allowed"]!.GetValue<bool>() == false);
    }

    [Fact]
    public async Task ForeignInstructionApproval_IsRejected()
    {
        using var factory = CreateFactory(new UnavailableGemma());
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var instruction = catalog["instructions"]![0]!;
        var otherCompany = catalog["companies"]!.AsArray().Single(c => Text(c!, "id") != Text(instruction, "companyId"))!;

        var response = await client.PostAsJsonAsync($"/api/support/instructions/{Text(instruction, "id")}/approve",
            new { companyId = Text(otherCompany, "id") });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SensitiveValuesInFreeText_AreRemovedBeforeModelAndDraftStorage()
    {
        var gemma = new UnavailableGemma();
        using var factory = CreateFactory(gemma);
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var company = catalog["companies"]![0]!;
        var companyId = Text(company, "id");
        const string sensitiveText = "FITPULSE-TAX-SECRET-7319 +380671112233 STUDYDESK-TAX-SECRET-8426 +4915112345678 studydesk-demo-reset-token fitpulse-private-reset-token studydesk-private@example.test";

        var resolved = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId, caseId = "duplicate_charge", message = $"Поверніть подвійне списання. Мої дані: {sensitiveText}"
        }));
        var draft = await Read(await client.PostAsJsonAsync("/api/support/instructions/drafts", new
        {
            companyId, caseId = "other", message = $"Нове звернення {sensitiveText}",
            resolution = $"Фахівець перевірив і вирішив звернення. {sensitiveText}"
        }));

        Assert.NotEmpty(gemma.RequestBodies);
        foreach (var secret in sensitiveText.Split(' '))
        {
            Assert.All(gemma.RequestBodies, body => Assert.DoesNotContain(secret, body));
            Assert.DoesNotContain(secret, resolved.ToJsonString());
            Assert.DoesNotContain(secret, draft.ToJsonString());
        }
    }

    [Fact]
    public async Task ForeignResolutionApproval_IsRejectedWithoutMutatingOriginal()
    {
        using var factory = CreateFactory(new UnavailableGemma());
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var companies = catalog["companies"]!.AsArray();
        var companyId = Text(companies[0]!, "id");
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId, caseId = "duplicate_charge", message = "З мене двічі списали оплату за цей місяць."
        }));
        var approval = await client.PostAsJsonAsync($"/api/support/resolutions/{Text(result, "id")}/approve",
            new { companyId = Text(companies[1]!, "id") });

        Assert.Equal(HttpStatusCode.NotFound, approval.StatusCode);
        var ownApproval = await client.PostAsJsonAsync($"/api/support/resolutions/{Text(result, "id")}/approve", new { companyId });
        var approved = await Read(ownApproval);
        Assert.Equal("executed", Text(approved, "status"));
        var repeated = await client.PostAsJsonAsync($"/api/support/resolutions/{Text(result, "id")}/approve", new { companyId });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }

    [Fact]
    public async Task ConcurrentApprovals_RefundExactlyOnce()
    {
        using var factory = CreateFactory(new UnavailableGemma());
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var companyId = Text(catalog["companies"]![0]!, "id");
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId, caseId = "duplicate_charge", message = "З мене двічі списали оплату за цей місяць."
        }));
        var url = $"/api/support/resolutions/{Text(result, "id")}/approve";

        var approvals = await Task.WhenAll(
            client.PostAsJsonAsync(url, new { companyId }),
            client.PostAsJsonAsync(url, new { companyId }));

        Assert.Single(approvals, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(approvals, r => r.StatusCode == HttpStatusCode.Conflict);
        var approved = await Read(approvals.Single(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Single(approved["state"]!["charges"]!.AsArray(), c => Text(c!, "status") == "refunded");
    }

    [Fact]
    public async Task OversizedMessage_IsRejectedBeforeModelRequest()
    {
        var gemma = new UnavailableGemma();
        using var factory = CreateFactory(gemma);
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var companyId = Text(catalog["companies"]![0]!, "id");
        var response = await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId, caseId = "duplicate_charge", message = new string('а', 20001)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, gemma.Calls);
    }

    [Theory]
    [InlineData("service_inactive", "activate_service", "active", "active")]
    [InlineData("no_access", "restore_access", "active", "active")]
    public async Task ValidModelVerdict_ExecutesOnlyApprovedInstruction(string caseId, string action, string subscription, string access)
    {
        using var factory = CreateFactory(new ModelGemma(caseId, action));
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var company = catalog["companies"]![0]!;
        var supportCase = company["cases"]!.AsArray().Single(c => Text(c!, "id") == caseId)!;
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId = Text(company, "id"), caseId, message = Text(supportCase, "message")
        }));

        Assert.Equal("gemma", Text(result, "source"));
        Assert.Equal("executed", Text(result, "status"));
        Assert.Equal(subscription, Text(result["state"]!, "subscription"));
        Assert.Equal(access, Text(result["state"]!, "access"));
        Assert.Equal(action, Text(result["instruction"]!, "action"));
        if (caseId == "duplicate_charge")
        {
            Assert.Single(result["state"]!["charges"]!.AsArray(), c => Text(c!, "status") == "refunded");
        }
    }

    [Fact]
    public async Task UnrelatedComplaint_DoesNotExecuteSelectedRefundInstruction()
    {
        using var factory = CreateFactory(new ModelGemma("other", "manual_review"));
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var companyId = Text(catalog["companies"]![0]!, "id");
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId, caseId = "duplicate_charge", message = "Потрібно перенести історію тренувань до іншої групи."
        }));

        Assert.Equal("needs_instruction", Text(result, "status"));
        Assert.Null(result["instruction"]);
        Assert.All(result["state"]!["charges"]!.AsArray(), c => Assert.Equal("paid", Text(c!, "status")));
        var approval = await client.PostAsJsonAsync($"/api/support/resolutions/{Text(result, "id")}/approve", new { companyId });
        Assert.Equal(HttpStatusCode.Conflict, approval.StatusCode);

        var draft = await Read(await client.PostAsJsonAsync("/api/support/instructions/drafts", new
        {
            companyId, caseId = "duplicate_charge", message = "Потрібно перенести історію тренувань до іншої групи.",
            resolution = "Фахівець перевіряє доступ до обох груп і переносить історію вручну."
        }));
        (await client.PostAsJsonAsync($"/api/support/instructions/{Text(draft, "id")}/approve", new { companyId })).EnsureSuccessStatusCode();
        var reused = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId, caseId = "duplicate_charge", message = "Потрібно перенести історію тренувань до іншої групи."
        }));
        Assert.Equal(Text(draft, "id"), Text(reused["instruction"]!, "id"));
        Assert.Equal("needs_approval", Text(reused, "status"));
    }

    [Fact]
    public async Task ModelEchoOfSensitiveValues_IsRedactedFromResponse()
    {
        const string secret = "FITPULSE-TAX-SECRET-7319 +380671112233 studydesk-private@example.test";
        using var factory = CreateFactory(new ModelGemma("duplicate_charge", "refund_duplicate", secret));
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var companyId = Text(catalog["companies"]![0]!, "id");
        var result = await Read(await client.PostAsJsonAsync("/api/support/resolve", new
        {
            companyId, caseId = "duplicate_charge", message = "З мене двічі списали оплату за цей місяць."
        }));

        Assert.Equal("gemma", Text(result, "source"));
        foreach (var value in secret.Split(' ')) Assert.DoesNotContain(value, result.ToJsonString());
    }

    [Fact]
    public async Task ModelGeneratedInstruction_CannotStoreOrReturnAnotherCompanyResetToken()
    {
        const string token = "studydesk-demo-reset-token";
        var model = new
        {
            title = "Ручне перенесення профілю", summary = $"Оператор перевіряє запит {token}",
            steps = new[] { $"Перевірити звернення {token}", "Передати відповідальному фахівцю." }, action = "manual_review"
        };
        using var factory = CreateFactory(new ModelGemma("other", "manual_review", modelOverride: model));
        var client = factory.CreateClient();
        var draft = await Read(await client.PostAsJsonAsync("/api/support/instructions/drafts", new
        {
            companyId = "fitpulse", caseId = "other", message = "Перенесіть історію до іншого профілю.",
            resolution = "Оператор перевірив профілі та передав звернення відповідальній команді."
        }));

        Assert.Equal("gemma", Text(draft, "source"));
        Assert.DoesNotContain(token, draft.ToJsonString());
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        Assert.DoesNotContain(token, catalog.ToJsonString());
    }

    [Fact]
    public async Task NovelComplaint_DraftRequiresApprovalAndIsReusedAfterApproval()
    {
        using var factory = CreateFactory(new UnavailableGemma());
        var client = factory.CreateClient();
        var catalog = await Read(await client.GetAsync("/api/support/catalog"));
        var company = catalog["companies"]![0]!;
        var supportCase = company["cases"]!.AsArray().Last()!;
        var request = new
        {
            companyId = Text(company, "id"), caseId = Text(supportCase, "id"),
            message = "Потрібно перенести історію тренувань зі старого профілю до нового"
        };

        var draft = await Read(await client.PostAsJsonAsync("/api/support/instructions/drafts", new
        {
            request.companyId, request.caseId, request.message,
            resolution = "Фахівець перевіряє володіння двома профілями й переносить лише історію тренувань."
        }));
        Assert.Equal("draft", Text(draft, "status"));
        Assert.Equal("manual_review", Text(draft, "action"));

        var before = await Read(await client.PostAsJsonAsync("/api/support/resolve", request));
        Assert.Equal("needs_instruction", Text(before, "status"));

        var approvalUrl = $"/api/support/instructions/{Text(draft, "id")}/approve";
        var approved = await Read(await client.PostAsJsonAsync(approvalUrl, new { request.companyId }));
        Assert.Equal("active", Text(approved, "status"));

        var after = await Read(await client.PostAsJsonAsync("/api/support/resolve", request));
        Assert.Equal(Text(draft, "id"), Text(after["instruction"]!, "id"));
        Assert.NotEqual("executed", Text(after, "status"));

        var repeated = await client.PostAsJsonAsync(approvalUrl, new { request.companyId });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
    }

    private static string Text(JsonNode node, string key) => node[key]!.GetValue<string>();

    private static async Task<JsonNode> Read(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return JsonNode.Parse(body)!;
    }

    private static WebApplicationFactory<Program> CreateFactory(HttpMessageHandler handler) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Gemma:API_Key", "test-key");
            builder.UseSetting("Gemma:AI_TimeoutSeconds", "0.3");
            builder.UseSetting("InMemoryDbName", Guid.NewGuid().ToString());
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<GemmaService>().ConfigurePrimaryHttpMessageHandler(() => handler));
        });

    private sealed class UnavailableGemma : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class ModelGemma(string caseId, string action, string? echo = null, object? modelOverride = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var model = System.Text.Json.JsonSerializer.Serialize(modelOverride ?? new
            {
                caseId, action, summary = echo ?? "Звернення відповідає затвердженій інструкції.",
                reply = echo ?? "Перевірку завершено. Результат операції визначає сервер."
            });
            var envelope = System.Text.Json.JsonSerializer.Serialize(new
            {
                candidates = new[] { new { content = new { parts = new[] { new { text = model } } } } }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "application/json")
            });
        }
    }
}
