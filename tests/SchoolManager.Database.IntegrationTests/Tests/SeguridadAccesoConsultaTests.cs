using System.Text.Json;
using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class SeguridadAccesoConsultaTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Admin_global_legacy_no_puede_consultar_seguridad_de_una_institucion()
    {
        var institucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioConAdminAsync(null);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthJsonAsync(actor.AuthUserId,
                "select public.rpc_obtener_seguridad_acceso($1)", institucion));

        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task Admin_explicito_recibe_snapshot_filtrado_de_su_institucion()
    {
        var institucion = await InsertInstitucionAsync();
        var otraInstitucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioConAdminAsync(institucion);
        var rolVisible = await InsertRolInstitucionalAsync(institucion, "Secretaria");
        _ = await InsertRolInstitucionalAsync(otraInstitucion, "No visible");

        using var json = await AuthJsonAsync(actor.AuthUserId,
            "select public.rpc_obtener_seguridad_acceso($1)", institucion);
        var root = json.RootElement;

        Assert.Equal(institucion, root.GetProperty("institucionId").GetGuid());
        Assert.True(root.GetProperty("capacidades").GetProperty("rolesVer").GetBoolean());
        Assert.True(root.GetProperty("capacidades").GetProperty("usuariosVer").GetBoolean());

        var roles = root.GetProperty("roles").EnumerateArray().ToArray();
        Assert.Contains(roles, r => r.GetProperty("id").GetGuid() == rolVisible);
        Assert.DoesNotContain(roles, r => r.GetProperty("nombre").GetString() == "No visible");

        var permisos = root.GetProperty("permisosDelegables").EnumerateArray().ToArray();
        Assert.NotEmpty(permisos);
        Assert.All(permisos, p => Assert.False(
            p.GetProperty("codigo").GetString()!.StartsWith("platform.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Admin_de_institucion_A_no_puede_consultar_institucion_B()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        var actor = await InsertUsuarioConAdminAsync(institucionA);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthJsonAsync(actor.AuthUserId,
                "select public.rpc_obtener_seguridad_acceso($1)", institucionB));

        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task Snapshot_no_filtra_asignaciones_de_otra_institucion()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        var actor = await InsertUsuarioConAdminAsync(institucionA);
        var destinoA = await InsertUsuarioAsync();
        var destinoB = await InsertUsuarioAsync();
        var adminId = await GetAdminIdAsync();

        var asignacionA = await ScalarGuidAsync("""
            insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
            values ($1, $2, $3) returning id
            """, destinoA.UsuarioId, adminId, institucionA);
        var asignacionB = await ScalarGuidAsync("""
            insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
            values ($1, $2, $3) returning id
            """, destinoB.UsuarioId, adminId, institucionB);

        using var json = await AuthJsonAsync(actor.AuthUserId,
            "select public.rpc_obtener_seguridad_acceso($1)", institucionA);
        var asignaciones = json.RootElement.GetProperty("asignaciones")
            .EnumerateArray().ToArray();

        Assert.Contains(asignaciones, a => a.GetProperty("id").GetGuid() == asignacionA);
        Assert.DoesNotContain(asignaciones, a => a.GetProperty("id").GetGuid() == asignacionB);
    }

    private async Task<Identidad> InsertUsuarioConAdminAsync(Guid? institucionId)
    {
        var actor = await InsertUsuarioAsync();
        var adminId = await GetAdminIdAsync();
        if (institucionId.HasValue)
        {
            await ExecuteAsync("""
                insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
                values ($1, $2, $3)
                """, actor.UsuarioId, adminId, institucionId.Value);
        }
        else
        {
            await ExecuteAsync(
                "insert into public.usuarios_roles(usuario_id, rol_id) values ($1, $2)",
                actor.UsuarioId, adminId);
        }
        return actor;
    }

    private async Task<Identidad> InsertUsuarioAsync()
    {
        var personaId = await ScalarGuidAsync(
            "insert into public.personas(nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUserId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync("""
            insert into public.usuarios(persona_id, auth_user_id, activo)
            values ($1, $2, true) returning id
            """, personaId, authUserId);
        return new Identidad(usuarioId, authUserId);
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones(nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private Task<Guid> InsertRolInstitucionalAsync(Guid institucionId, string nombre) =>
        ScalarGuidAsync("""
            insert into public.roles(codigo, nombre, activo, tipo, institucion_id)
            values ($1, $2, true, 'institucional', $3) returning id
            """, $"rol_{Guid.NewGuid():N}", nombre, institucionId);

    private Task<Guid> GetAdminIdAsync() => ScalarGuidAsync(
        "select id from public.roles where codigo = 'admin' and institucion_id is null");

    private async Task<JsonDocument> AuthJsonAsync(
        Guid authUserId,
        string sql,
        params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var roleCommand = new NpgsqlCommand(
                "set local role authenticated", connection, transaction))
            {
                await roleCommand.ExecuteNonQueryAsync();
            }
            await using (var authCommand = new NpgsqlCommand(
                "select set_config('request.jwt.claim.sub', $1, true)", connection, transaction))
            {
                authCommand.Parameters.AddWithValue(authUserId.ToString());
                await authCommand.ExecuteNonQueryAsync();
            }

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            var value = await command.ExecuteScalarAsync();
            var json = JsonDocument.Parse((string)value!);
            await transaction.CommitAsync();
            return json;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ExecuteAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value);
    }

    private sealed record Identidad(Guid UsuarioId, Guid AuthUserId);
}
