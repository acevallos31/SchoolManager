using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolManager.API.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class RequestObservabilityMiddlewareTests
{
    [Fact]
    public async Task Agrega_request_id_a_la_respuesta()
    {
        using var metrics = new ApiObservabilityMetrics();
        var middleware = new RequestObservabilityMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RequestObservabilityMiddleware>.Instance,
            metrics
        );
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "request-test-123"
        };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(
            "request-test-123",
            context.Response.Headers[RequestObservabilityMiddleware.RequestIdHeader].ToString()
        );
    }

    [Fact]
    public async Task Excepcion_no_controlada_devuelve_problem_details_sin_filtrar_detalle_interno()
    {
        const string detalleInterno = "Password=secreto-no-debe-salir";

        using var metrics = new ApiObservabilityMetrics();
        var middleware = new RequestObservabilityMiddleware(
            _ => throw new InvalidOperationException(detalleInterno),
            NullLogger<RequestObservabilityMiddleware>.Instance,
            metrics
        );
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "request-error-456"
        };
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        Assert.Equal(
            "request-error-456",
            context.Response.Headers[RequestObservabilityMiddleware.RequestIdHeader].ToString()
        );

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        Assert.DoesNotContain(detalleInterno, body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("request-error-456", json.RootElement.GetProperty("requestId").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }
}
