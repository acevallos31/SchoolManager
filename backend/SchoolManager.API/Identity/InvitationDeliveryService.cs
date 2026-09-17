using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Npgsql;

namespace SchoolManager.API.Identity;

public sealed record InvitationDeliveryResult(
    Guid InvitationId,
    string Estado,
    DateTimeOffset ExpiresAt,
    long EmissionVersion);

public sealed class InvitationDeliveryService(
    NpgsqlDataSource dataSource,
    IInvitationEmailSender sender,
    IOptions<InvitationEmailOptions> optionsAccessor,
    ILogger<InvitationDeliveryService> logger)
{
    private readonly InvitationEmailOptions _options = optionsAccessor.Value;

    public async Task<InvitationDeliveryResult> SendAsync(
        Guid invitationId,
        string authSub,
        CancellationToken ct)
    {
        if (!sender.IsConfigured)
            throw new InvitationEmailNotConfiguredException();

        var ttlHours = _options.TtlHours;
        if (ttlHours is < 1 or > 168)
            throw new InvalidOperationException("InvitationEmail:TtlHours debe estar entre 1 y 168.");

        if (!Uri.TryCreate(_options.FrontendBaseUrl, UriKind.Absolute, out var frontendBase)
            || (frontendBase.Scheme != Uri.UriSchemeHttps
                && !frontendBase.IsLoopback))
        {
            throw new InvalidOperationException(
                "InvitationEmail:FrontendBaseUrl debe ser HTTPS salvo en localhost.");
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Base64Url(tokenBytes);
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(ttlHours);

        var issued = await IssueAsync(invitationId, tokenHash, expiresAt, authSub, ct);
        var acceptanceUrl = BuildAcceptanceUrl(frontendBase, token);

        try
        {
            var sendResult = await sender.SendAsync(
                new InvitationEmailMessage(
                    invitationId,
                    issued.EmissionVersion,
                    issued.Email,
                    acceptanceUrl,
                    expiresAt),
                ct);

            await ConfirmAsync(
                invitationId,
                issued.EmissionVersion,
                sendResult,
                authSub,
                ct);

            logger.LogInformation(
                "Invitation email sent. InvitationId={InvitationId} Version={EmissionVersion} Provider={Provider}",
                invitationId,
                issued.EmissionVersion,
                sendResult.Provider);

            return new InvitationDeliveryResult(
                invitationId,
                "enviada",
                expiresAt,
                issued.EmissionVersion);
        }
        catch (InvitationEmailDeliveryException ex)
        {
            await RegisterFailureAsync(
                invitationId,
                issued.EmissionVersion,
                ex.Code,
                authSub,
                ct);

            logger.LogWarning(
                "Invitation email delivery failed. InvitationId={InvitationId} Version={EmissionVersion} Code={Code}",
                invitationId,
                issued.EmissionVersion,
                ex.Code);
            throw;
        }
    }

    private async Task<IssuedInvitation> IssueAsync(
        Guid invitationId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string authSub,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await SetAuthSubAsync(connection, tx, authSub, ct);

        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            select public.rpc_emitir_invitacion_acceso(@id,@hash,@expira)::text
            """;
        command.Parameters.AddWithValue("id", invitationId);
        command.Parameters.AddWithValue("hash", tokenHash);
        command.Parameters.AddWithValue("expira", expiresAt);

        var json = (string)(await command.ExecuteScalarAsync(ct))!;
        await tx.CommitAsync(ct);

        using var document = JsonDocument.Parse(json);
        return new IssuedInvitation(
            document.RootElement.GetProperty("correo").GetString()!,
            document.RootElement.GetProperty("emisionVersion").GetInt64());
    }

    private async Task ConfirmAsync(
        Guid invitationId,
        long emissionVersion,
        InvitationEmailSendResult sendResult,
        string authSub,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await SetAuthSubAsync(connection, tx, authSub, ct);

        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            select public.rpc_confirmar_envio_invitacion(@id,@version,@provider,@message_id)
            """;
        command.Parameters.AddWithValue("id", invitationId);
        command.Parameters.AddWithValue("version", emissionVersion);
        command.Parameters.AddWithValue("provider", sendResult.Provider);
        command.Parameters.AddWithValue("message_id", sendResult.MessageId);
        await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task RegisterFailureAsync(
        Guid invitationId,
        long emissionVersion,
        string errorCode,
        string authSub,
        CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await SetAuthSubAsync(connection, tx, authSub, ct);

        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            select public.rpc_registrar_error_envio_invitacion(@id,@version,@error)
            """;
        command.Parameters.AddWithValue("id", invitationId);
        command.Parameters.AddWithValue("version", emissionVersion);
        command.Parameters.AddWithValue("error", errorCode);
        await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task SetAuthSubAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        string authSub,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "select set_config('request.jwt.claim.sub', @sub, true)";
        command.Parameters.AddWithValue("sub", authSub);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string BuildAcceptanceUrl(Uri frontendBase, string token)
    {
        var baseUrl = frontendBase.ToString().TrimEnd('/');
        return $"{baseUrl}/invitacion/aceptar?token={Uri.EscapeDataString(token)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed record IssuedInvitation(string Email, long EmissionVersion);
}
