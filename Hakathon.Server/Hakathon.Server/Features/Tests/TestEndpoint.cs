using Carter;
using Hakathon.Server.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hakathon.Server.Features.Tests;

public class TestEndpoint(GemmaService gemmaService) : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/test",async(string prompt) =>
        {
            var result = await gemmaService.RequestAsync(prompt);
            return Results.Ok(result);
        });
    }
}
