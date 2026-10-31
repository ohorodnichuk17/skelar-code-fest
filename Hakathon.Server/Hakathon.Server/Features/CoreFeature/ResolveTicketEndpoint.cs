using Carter;
using FluentValidation;
using MediatR;

namespace Hakathon.Server.Features.CoreFeature;

public sealed class ResolveTicketEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/tickets/{ticketId}/messages",
            async (string ticketId, ResolveTicketRequest request, ISender sender, CancellationToken cancellationToken) =>
                Results.Ok(await sender.Send(new ResolveTicketCommand(ticketId, request.Message), cancellationToken)));
    }
}

public sealed class ResolveTicketCommandValidator : AbstractValidator<ResolveTicketCommand>
{
    public ResolveTicketCommandValidator()
    {
        RuleFor(x => x.TicketId).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
    }
}
