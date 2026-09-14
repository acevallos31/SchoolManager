using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RbacLecturaInstitucionalEstrictaTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Admin_global_legacy_no_ve_rol_institucional_ajeno()
    {
        var institucion = await InsertInstitucionAsync();
        var rolId = await InsertRolInstitucionalAsync(institucion);
        var actor = await InsertUsuarioConAdminAsync(null);

        Assert.Equal(0, await AuthScalarLongAsync(
            actor.AuthUserId,
            "select count(*) from public.roles where id = $1",
            rolId));
    }

    [Fact]
    public async Task Admin_asignado_en_institucion_si_ve_sus_roles_institucionales()
    {
        var institucion = await InsertInstitucionAsync();
        var rolId = await InsertRolInstitucionalAsync(institucion);
        var actor = await InsertUsuarioConAdminAsync(institucion);

        Assert.Equal(1, await AuthScalarLongAsync(
            actor.AuthUserId,
            "select count(*) from public.roles where id = $1",
            rolId));
    }

    [Fact]
    public async Task Admin_de_otra_institucion_no_ve_asignaciones_del_objetivo()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        var rolB = await InsertRolInstitucionalAsync(institucionB);
        var destino = await InsertUsuarioAsync();
        var asignacionB = await ScalarGuidAsync("""
            insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
            values ($1, $2, $3)
            returning id
            """, destino.UsuarioId, rolB, institucionB);
        var actor = await InsertUsuarioConAdminAsync(institucionA);

        Assert.Equal(0, await AuthScalarLongAsync(
            actor.AuthUserId,
            "select count(*) from public.usuarios_roles where id = $1",
            asignacionB));
    }

    private async Task<Identidad> InsertUsuarioConAdminAsync(Guid? institucionId)
    {
        var actor = await InsertUsuarioAsync();
        var adminId = await ScalarGuidAsync(
            "select id from public.roles where codigo = 'admin' and institucion_id is null");

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
            values ($1, $2, true)
            returning id
            """, personaId, authUserId);
        return new Identidad(usuarioId, authUserId);
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones(nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private Task<Guid> InsertRolInstitucionalAsync(Guid institucionId) => ScalarGuidAsync("""
        insert into public.roles(codigo, nombre, activo, tipo, institucion_id)
        values ($1, 'Rol lectura', true, 'institucional', $2)
        returning id
        """, $"lectura_{Guid.NewGuid():N}", institucionId);

    private async Task<long> AuthScalarLongAsync(
        Guid authUserId,
        string sql,
        params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var roleCommand = new NpgsqlCommand(
                "set local role authenticated", connection, transaction);
            await roleCommand.ExecuteNonQueryAsync();
            await using var authCommand = new NpgsqlCommand(
                "select set_config('request.jwt.claim.sub', $1, true)", connection, transaction);
            authCommand.Parameters.AddWithValue(authUserId.ToString());
            await authCommand.ExecuteNonQueryAsync();

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            var result = (long)(await command.ExecuteScalarAsync())!;
            await transaction.CommitAsync();
            return result;
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
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    private sealed record Identidad(Guid UsuarioId, Guid AuthUserId);
}
