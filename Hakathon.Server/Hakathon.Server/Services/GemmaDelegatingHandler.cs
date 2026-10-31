using Hakathon.Server.Options;
using Microsoft.Extensions.Options;

namespace Hakathon.Server.Services;

internal sealed class GemmaDelegatingHandler(IOptions<GemmaOption> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("x-goog-api-key", options.Value.API_Key);
        return base.SendAsync(request, cancellationToken);
    }
}
