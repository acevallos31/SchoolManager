using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SchoolManager.API.Identity;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class InvitationEmailSenderTests
{
    [Fact]
    public async Task Resend_envia_con_bearer_idempotencia_y_no_modifica_el_enlace()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "{\"id\":\"email-123\"}");
        var sender = new ResendInvitationEmailSender(
            new HttpClient(handler),
            Options.Create(new InvitationEmailOptions
            {
                Provider = "resend",
                ApiKey = "re_test_secret",
                From = "SchoolManager <acceso@example.com>",
                FrontendBaseUrl = "https://schoolmanager.test"
            }));
        var url = "https://schoolmanager.test/invitacion/aceptar?token=token-secreto";

        var result = await sender.SendAsync(new InvitationEmailMessage(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            7,
            "padre@example.com",
            url,
            DateTimeOffset.Parse("2026-09-18T02:00:00Z")), CancellationToken.None);

        Assert.Equal("resend", result.Provider);
        Assert.Equal("email-123", result.MessageId);
        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.resend.com/emails", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization?.Scheme);
        Assert.Equal("re_test_secret", handler.Request.Headers.Authorization?.Parameter);
        Assert.Equal(
            "schoolmanager-invitacion/11111111111141118111111111111111/v7",
            handler.Request.Headers.GetValues("Idempotency-Key").Single());
        Assert.Contains("padre@example.com", handler.Body);
        Assert.Contains("token-secreto", handler.Body);
    }

    [Fact]
    public async Task Resend_rechaza_configuracion_sin_api_key_antes_de_hacer_http()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "{\"id\":\"unused\"}");
        var sender = new ResendInvitationEmailSender(
            new HttpClient(handler),
            Options.Create(new InvitationEmailOptions
            {
                Provider = "resend",
                From = "SchoolManager <acceso@example.com>"
            }));

        Assert.False(sender.IsConfigured);
        await Assert.ThrowsAsync<InvitationEmailNotConfiguredException>(() => sender.SendAsync(
            new InvitationEmailMessage(
                Guid.NewGuid(), 1, "x@example.com", "https://example.com/t", DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Resend_mapea_error_http_sin_exponer_respuesta_del_proveedor()
    {
        var handler = new CapturingHandler(HttpStatusCode.TooManyRequests, "{\"message\":\"detalle sensible\"}");
        var sender = new ResendInvitationEmailSender(
            new HttpClient(handler),
            Options.Create(new InvitationEmailOptions
            {
                Provider = "resend",
                ApiKey = "re_test_secret",
                From = "SchoolManager <acceso@example.com>"
            }));

        var ex = await Assert.ThrowsAsync<InvitationEmailDeliveryException>(() => sender.SendAsync(
            new InvitationEmailMessage(
                Guid.NewGuid(), 2, "x@example.com", "https://example.com/t", DateTimeOffset.UtcNow.AddHours(1)),
            CancellationToken.None));

        Assert.Equal("resend_http_429", ex.Code);
        Assert.DoesNotContain("detalle sensible", ex.Message);
    }

    private sealed class CapturingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
