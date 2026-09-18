using System.Text.Json;
using Npgsql;

namespace SchoolManager.API.Services;

public sealed record DemoSessionResult(
    Guid SessionId,
    Guid InstitucionId,
    bool Reused
);

public interface IDemoSandboxService
{
    Task<DemoSessionResult> CrearOReutilizarAsync(
        Guid authUserId,
        Guid templateInstitutionId,
        CancellationToken cancellationToken = default
    );

    Task<DemoSessionResult> ReiniciarAsync(
        Guid authUserId,
        Guid templateInstitutionId,
        CancellationToken cancellationToken = default
    );
}

public sealed class DemoSandboxService(NpgsqlDataSource dataSource) : IDemoSandboxService
{
    public Task<DemoSessionResult> CrearOReutilizarAsync(
        Guid authUserId,
        Guid templateInstitutionId,
        CancellationToken cancellationToken = default
    ) => EjecutarAsync(
        "select public.rpc_crear_sandbox_demo(@authUserId,@templateInstitutionId)::text",
        authUserId,
        templateInstitutionId,
        cancellationToken
    );

    public Task<DemoSessionResult> ReiniciarAsync(
        Guid authUserId,
        Guid templateInstitutionId,
        CancellationToken cancellationToken = default
    ) => EjecutarAsync(
        "select public.rpc_reset_sandbox_demo(@authUserId,@templateInstitutionId)::text",
        authUserId,
        templateInstitutionId,
        cancellationToken
    );

    private async Task<DemoSessionResult> EjecutarAsync(
        string sql,
        Guid authUserId,
        Guid templateInstitutionId,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("authUserId", authUserId);
        command.Parameters.AddWithValue("templateInstitutionId", templateInstitutionId);

        var json = (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("La operación Demo no devolvió resultado.");

        await transaction.CommitAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new DemoSessionResult(
            root.GetProperty("sessionId").GetGuid(),
            root.GetProperty("institucionId").GetGuid(),
            root.GetProperty("reused").GetBoolean()
        );
    }
}
