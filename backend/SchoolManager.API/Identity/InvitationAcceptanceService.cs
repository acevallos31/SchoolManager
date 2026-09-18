using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace SchoolManager.API.Identity;

public sealed record InvitationAcceptanceResult(
    Guid InvitacionId,
    Guid UsuarioId,
    Guid InstitucionId,
    string Estado);

public sealed class InvitationAcceptanceService(NpgsqlDataSource dataSource)
{
    public async Task<InvitationAcceptanceResult> AcceptAsync(
        string token,
        string authSub,
        CancellationToken ct)
    {
        if (!Guid.TryParse(authSub, out var authUserId))
            throw new UnauthorizedAccessException("El claim sub no contiene un UUID valido.");

        var normalizedToken = token?.Trim() ?? string.Empty;
        if (normalizedToken.Length < 32 || normalizedToken.Length > 512)
            throw new ArgumentException("El token de invitacion no es valido.", nameof(token));

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalizedToken)))
            .ToLowerInvariant();

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            select public.rpc_solicitar_vinculacion_invitacion(@hash,@auth_user_id)::text
            """;
        command.Parameters.AddWithValue("hash", tokenHash);
        command.Parameters.AddWithValue("auth_user_id", authUserId);

        var json = (string?)await command.ExecuteScalarAsync(ct) ?? "{}";
        await tx.CommitAsync(ct);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new InvitationAcceptanceResult(
            root.GetProperty("invitacionId").GetGuid(),
            root.GetProperty("usuarioId").GetGuid(),
            root.GetProperty("institucionId").GetGuid(),
            root.GetProperty("estado").GetString() ?? "aceptada");
    }
}
