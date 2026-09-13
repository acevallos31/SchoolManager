using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RbacCanonicalizacionConfiguracionTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Rol_dinamico_con_academico_ciclos_ver_satisface_alias_interno()
    {
        var institucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioAsync();
        var rol = await InsertRolConPermisoAsync(institucion, "academico.ciclos.ver");
        await AsignarAsync(actor.UsuarioId, rol, institucion);

        Assert.True(await AuthScalarBoolAsync(
            actor.AuthUserId,
            "select public.usuario_tiene_permiso_actual('configuracion.ciclos.ver', $1)",
            institucion));
    }

    [Fact]
    public async Task Permiso_canonico_de_ciclos_no_se_extiende_a_otra_institucion()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        var actor = await InsertUsuarioAsync();
        var rol = await InsertRolConPermisoAsync(institucionA, "academico.ciclos.ver");
        await AsignarAsync(actor.UsuarioId, rol, institucionA);

        Assert.False(await AuthScalarBoolAsync(
            actor.AuthUserId,
            "select public.usuario_tiene_permiso_actual('configuracion.ciclos.ver', $1)",
            institucionB));
    }

    [Theory]
    [InlineData("configuracion.periodos_matricula.crear", "academico.ciclos.crear")]
    [InlineData("configuracion.periodos_matricula.editar", "academico.ciclos.editar")]
    [InlineData("configuracion.periodos_matricula.desactivar", "academico.ciclos.desactivar")]
    [InlineData("configuracion.grados.ver", "academico.estructura.ver")]
    [InlineData("configuracion.jornadas.crear", "academico.estructura.editar")]
    [InlineData("configuracion.secciones.editar", "academico.estructura.editar")]
    [InlineData("configuracion.grados.desactivar", "academico.estructura.desactivar")]
    public async Task Alias_interno_se_satisface_con_permiso_canonico(
        string aliasInterno,
        string permisoCanonico)
    {
        var institucion = await InsertInstitucionAsync();
        var actor = await InsertUsuarioAsync();
        var rol = await InsertRolConPermisoAsync(institucion, permisoCanonico);
        await AsignarAsync(actor.UsuarioId, rol, institucion);

        Assert.True(await AuthScalarBoolAsync(
            actor.AuthUserId,
            "select public.usuario_tiene_permiso_actual($1, $2)",
            aliasInterno,
            institucion));
    }

    private async Task<Guid> InsertRolConPermisoAsync(Guid institucionId, string permisoCodigo)
    {
        var rolId = await ScalarGuidAsync("""
            insert into public.roles(codigo, nombre, activo, tipo, institucion_id)
            values ($1, 'Rol canonicalización', true, 'institucional', $2)
            returning id
            """, $"canon_{Guid.NewGuid():N}", institucionId);

        await ExecuteAsync("""
            insert into public.roles_permisos(rol_id, permiso_id)
            select $1, id from public.permisos where codigo = $2
            """, rolId, permisoCodigo);

        return rolId;
    }

    private Task AsignarAsync(Guid usuarioId, Guid rolId, Guid institucionId) => ExecuteAsync("""
        insert into public.usuarios_roles(usuario_id, rol_id, institucion_id)
        values ($1, $2, $3)
        """, usuarioId, rolId, institucionId);

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

    private Task<bool> AuthScalarBoolAsync(
        Guid authUserId,
        string sql,
        params object[] values) =>
        AuthScalarAsync<bool>(authUserId, sql, values);

    private async Task<T> AuthScalarAsync<T>(
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
            var result = (T)(await command.ExecuteScalarAsync())!;
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
