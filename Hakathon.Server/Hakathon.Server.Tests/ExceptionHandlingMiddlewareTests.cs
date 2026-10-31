using System.Text.Json;
using Hakathon.Server.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace Hakathon.Server.Tests;

public class ExceptionHandlingMiddlewareTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientCancellation_DoesNotLogErrorOrWriteBody(bool taskCanceled)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        using var body = new TrackingStream();
        context.Response.Body = body;
        var logger = new RecordingLogger();
        var middleware = new ExceptionHandlingMiddleware(logger);

        await middleware.InvokeAsync(context, _ => Task.FromException(CreateCancellation(taskCanceled)));

        Assert.Empty(logger.Errors);
        Assert.Equal(0, body.WriteAttempts);
        Assert.Equal(0, body.Length);
        Assert.Null(context.Response.ContentType);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientCancellation_WhenResponseStarted_PreservesResponse(bool taskCanceled)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature
        {
            StatusCode = StatusCodes.Status202Accepted
        });
        using var body = new TrackingStream();
        context.Response.Body = body;
        context.Response.ContentType = "text/plain";
        var logger = new RecordingLogger();
        var middleware = new ExceptionHandlingMiddleware(logger);
        Assert.True(context.Response.HasStarted);

        await middleware.InvokeAsync(context, _ => Task.FromException(CreateCancellation(taskCanceled)));

        Assert.Empty(logger.Errors);
        Assert.Equal(0, body.WriteAttempts);
        Assert.Equal(StatusCodes.Status202Accepted, context.Response.StatusCode);
        Assert.Equal("text/plain", context.Response.ContentType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InternalCancellation_LogsErrorAndWrites500Response(bool taskCanceled)
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;
        var logger = new RecordingLogger();
        var middleware = new ExceptionHandlingMiddleware(logger);
        var exception = CreateCancellation(taskCanceled);

        await middleware.InvokeAsync(context, _ => Task.FromException(exception));

        Assert.Same(exception, Assert.Single(logger.Errors));
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.Equal(500, json.RootElement.GetProperty("statusCode").GetInt32());
        Assert.Equal("internal_server_error", json.RootElement.GetProperty("errorCode").GetString());
        Assert.Equal(exception.Message, json.RootElement.GetProperty("error").GetString());
    }

    private static OperationCanceledException CreateCancellation(bool taskCanceled) => taskCanceled
        ? new TaskCanceledException("Operation canceled")
        : new OperationCanceledException("Operation canceled");

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class TrackingStream : MemoryStream
    {
        public int WriteAttempts { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAttempts++;
            base.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            WriteAttempts++;
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteAttempts++;
            return base.WriteAsync(buffer, cancellationToken);
        }
    }

    private sealed class RecordingLogger : ILogger<ExceptionHandlingMiddleware>
    {
        public List<Exception?> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
                Errors.Add(exception);
        }
    }
}
