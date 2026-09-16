using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace SchoolManager.API.Infrastructure;

/// <summary>
/// Correlación, logging estructurado y respuesta segura ante excepciones no controladas.
/// Nunca registra query strings, headers, cuerpos, tokens, correos ni nombres.
/// </summary>
public sealed class RequestObservabilityMiddleware
{
    public const string RequestIdHeader = "X-Request-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestObservabilityMiddleware> _logger;
    private readonly ApiObservabilityMetrics _metrics;

    public RequestObservabilityMiddleware(
        RequestDelegate next,
        ILogger<RequestObservabilityMiddleware> logger,
        ApiObservabilityMetrics metrics
    )
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var traceId = Activity.Current?.TraceId.ToString() ?? requestId;
        var route = GetSafeRoute(context);
        var userId = context.User.FindFirst("sub")?.Value;

        context.Response.Headers[RequestIdHeader] = requestId;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId,
            ["TraceId"] = traceId,
            ["Route"] = route,
            ["UserId"] = userId,
            ["Authenticated"] = context.User.Identity?.IsAuthenticated == true
        });

        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            route = GetSafeRoute(context);

            _logger.LogError(
                exception,
                "Unhandled exception processing {Method} {Route}",
                context.Request.Method,
                route
            );

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers[RequestIdHeader] = requestId;

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocurrió un error interno al procesar la solicitud.",
                Type = "about:blank"
            };
            problem.Extensions["requestId"] = requestId;
            problem.Extensions["traceId"] = traceId;

            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
        finally
        {
            stopwatch.Stop();
            route = GetSafeRoute(context);

            _metrics.RecordRequest(
                context.Request.Method,
                route,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds
            );

            _logger.LogInformation(
                "HTTP request completed {Method} {Route} with {StatusCode} in {ElapsedMilliseconds:F2} ms",
                context.Request.Method,
                route,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds
            );
        }
    }

    private static string GetSafeRoute(HttpContext context)
    {
        var routePattern = context.GetEndpoint()
            ?.Metadata
            .GetMetadata<RouteEndpoint>()
            ?.RoutePattern
            .RawText;

        if (!string.IsNullOrWhiteSpace(routePattern))
        {
            return routePattern.StartsWith('/') ? routePattern : $"/{routePattern}";
        }

        return context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
    }
}
