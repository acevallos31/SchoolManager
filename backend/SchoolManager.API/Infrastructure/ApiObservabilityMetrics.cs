using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SchoolManager.API.Infrastructure;

/// <summary>
/// Métricas internas del API basadas en System.Diagnostics.Metrics.
/// No abre un endpoint público ni requiere un servicio externo: cualquier
/// exporter/collector futuro puede suscribirse al meter por nombre.
/// </summary>
public sealed class ApiObservabilityMetrics : IDisposable
{
    public const string MeterName = "SchoolManager.API";

    private readonly Meter _meter = new(MeterName, "1.0.0");
    private readonly Counter<long> _requests;
    private readonly Counter<long> _serverErrors;
    private readonly Histogram<double> _requestDuration;

    public ApiObservabilityMetrics()
    {
        _requests = _meter.CreateCounter<long>(
            "schoolmanager.api.requests",
            unit: "{request}",
            description: "Cantidad de solicitudes HTTP procesadas por SchoolManager.API."
        );
        _serverErrors = _meter.CreateCounter<long>(
            "schoolmanager.api.server_errors",
            unit: "{request}",
            description: "Cantidad de respuestas HTTP 5xx generadas por SchoolManager.API."
        );
        _requestDuration = _meter.CreateHistogram<double>(
            "schoolmanager.api.request.duration",
            unit: "ms",
            description: "Duración de solicitudes HTTP en milisegundos."
        );
    }

    public void RecordRequest(string method, string route, int statusCode, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "http.request.method", method },
            { "http.route", route },
            { "http.response.status_code", statusCode }
        };

        _requests.Add(1, tags);
        _requestDuration.Record(elapsedMilliseconds, tags);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _serverErrors.Add(1, tags);
        }
    }

    public void Dispose() => _meter.Dispose();
}
