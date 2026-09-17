using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SchoolManager.API.Identity;

public sealed class InvitationEmailOptions
{
    public string Provider { get; set; } = "resend";
    public string? ApiKey { get; set; }
    public string? From { get; set; }
    public string FrontendBaseUrl { get; set; } = "http://localhost:4200";
    public int TtlHours { get; set; } = 24;
}

public sealed record InvitationEmailMessage(
    Guid InvitationId,
    long EmissionVersion,
    string To,
    string AcceptanceUrl,
    DateTimeOffset ExpiresAt);

public sealed record InvitationEmailSendResult(string Provider, string MessageId);

public interface IInvitationEmailSender
{
    bool IsConfigured { get; }
    Task<InvitationEmailSendResult> SendAsync(InvitationEmailMessage message, CancellationToken ct);
}

public sealed class InvitationEmailNotConfiguredException()
    : InvalidOperationException("El servicio de correo de invitaciones no está configurado.");

public sealed class InvitationEmailDeliveryException(string code)
    : Exception("No se pudo enviar el correo de invitación.")
{
    public string Code { get; } = code;
}

public sealed class ResendInvitationEmailSender(
    HttpClient httpClient,
    IOptions<InvitationEmailOptions> optionsAccessor) : IInvitationEmailSender
{
    private readonly InvitationEmailOptions _options = optionsAccessor.Value;

    public bool IsConfigured =>
        string.Equals(_options.Provider, "resend", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.From);

    public async Task<InvitationEmailSendResult> SendAsync(
        InvitationEmailMessage message,
        CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvitationEmailNotConfiguredException();

        var encodedUrl = HtmlEncoder.Default.Encode(message.AcceptanceUrl);
        var encodedExpiry = HtmlEncoder.Default.Encode(message.ExpiresAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'"));
        var payload = new
        {
            from = _options.From,
            to = new[] { message.To },
            subject = "Invitación a SchoolManager",
            html = $"""
                <p>Has recibido una invitación para acceder a SchoolManager.</p>
                <p><a href="{encodedUrl}">Aceptar invitación</a></p>
                <p>Este enlace vence el {encodedExpiry} y solo puede utilizarse para esta invitación.</p>
                <p>Si no esperabas este mensaje, puedes ignorarlo.</p>
                """,
            text = $"Has recibido una invitación para acceder a SchoolManager.\n\nAceptar invitación: {message.AcceptanceUrl}\n\nEl enlace vence el {message.ExpiresAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC."
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            $"schoolmanager-invitacion/{message.InvitationId:N}/v{message.EmissionVersion}");
        request.Content = JsonContent.Create(payload);

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvitationEmailDeliveryException($"resend_http_{(int)response.StatusCode}");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!document.RootElement.TryGetProperty("id", out var idElement)
            || string.IsNullOrWhiteSpace(idElement.GetString()))
        {
            throw new InvitationEmailDeliveryException("resend_missing_message_id");
        }

        return new InvitationEmailSendResult("resend", idElement.GetString()!);
    }
}
