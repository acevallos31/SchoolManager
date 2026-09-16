using System.Text.Json;
using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class InvitacionesAccesoTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Preparar_invitacion_crea_usuario_sin_auth_y_es_idempotente()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);
        var correo = $"padre.{Guid.NewGuid():N}@schoolmanager.test";

        var primera = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
            """, institucion, "Maria", "Prueba", correo, rolDestino, "administracion");
        var segunda = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
            """, institucion, "Maria", "Prueba", correo, rolDestino, "administracion");

        using var json1 = JsonDocument.Parse(primera);
        using var json2 = JsonDocument.Parse(segunda);
        var usuarioId = json1.RootElement.GetProperty("usuarioId").GetGuid();
        var invitacionId = json1.RootElement.GetProperty("invitacionId").GetGuid();

        Assert.True(json1.RootElement.GetProperty("usuarioCreado").GetBoolean());
        Assert.True(json1.RootElement.GetProperty("invitacionCreada").GetBoolean());
        Assert.False(json2.RootElement.GetProperty("usuarioCreado").GetBoolean());
        Assert.False(json2.RootElement.GetProperty("invitacionCreada").GetBoolean());
        Assert.Equal(usuarioId, json2.RootElement.GetProperty("usuarioId").GetGuid());
        Assert.Equal(invitacionId, json2.RootElement.GetProperty("invitacionId").GetGuid());
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios where id=$1 and auth_user_id is null and activo", usuarioId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.invitaciones_acceso where id=$1 and estado='pendiente'", invitacionId));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.usuarios_roles
            where usuario_id=$1 and rol_id=$2 and institucion_id=$3 and activo
            """, usuarioId, rolDestino, institucion));
    }

    [Fact]
    public async Task Preparar_invitacion_no_delega_permiso_que_actor_no_posee()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.pagos.registrar"]);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => BackendScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text",
            institucion, "Carlos", "Prueba", $"carlos.{Guid.NewGuid():N}@schoolmanager.test",
            rolDestino, "administracion"));

        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task Preparar_invitacion_no_es_ejecutable_directamente_por_authenticated()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AuthenticatedDirectScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text",
            institucion, "Directo", "Bloqueado", $"directo.{Guid.NewGuid():N}@schoolmanager.test",
            rolDestino, "administracion"));

        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task Preparar_invitacion_serializa_altas_concurrentes_del_mismo_correo()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);
        var correo = $"concurrente.{Guid.NewGuid():N}@schoolmanager.test";

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            BackendScalarTextAsync(actor.AuthUserId, """
                select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
                """, institucion, "Ana", "Concurrente", correo, rolDestino, "administracion")));
        var documentos = respuestas.Select(respuesta => JsonDocument.Parse(respuesta)).ToArray();

        try
        {
            Assert.Single(documentos.Select(documento =>
                documento.RootElement.GetProperty("personaId").GetGuid()).Distinct());
            Assert.Single(documentos.Select(documento =>
                documento.RootElement.GetProperty("usuarioId").GetGuid()).Distinct());
            Assert.Single(documentos.Select(documento =>
                documento.RootElement.GetProperty("invitacionId").GetGuid()).Distinct());
            Assert.Single(documentos, documento =>
                documento.RootElement.GetProperty("personaCreada").GetBoolean());
            Assert.Single(documentos, documento =>
                documento.RootElement.GetProperty("invitacionCreada").GetBoolean());
            Assert.Equal(1, await ScalarLongAsync("""
                select count(*) from public.personas
                where lower(btrim(coalesce(correo,'')))=$1
                """, correo));
        }
        finally
        {
            foreach (var documento in documentos) documento.Dispose();
        }
    }

    private async Task<Actor> CrearActorAsync(Guid institucion, IEnumerable<string> permisos)
    {
        var authId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync(
            "insert into public.usuarios(auth_user_id,activo) values($1,true) returning id", authId);
        var rolId = await CrearRolAsync(institucion, permisos);
        await ExecuteAsync("""
            insert into public.usuarios_roles(usuario_id,rol_id,institucion_id)
            values($1,$2,$3)
            """, usuarioId, rolId, institucion);
        return new Actor(usuarioId, authId);
    }

    private async Task<Guid> CrearRolAsync(Guid institucion, IEnumerable<string> permisos)
    {
        var rolId = await ScalarGuidAsync("""
            insert into public.roles(codigo,nombre,tipo,institucion_id,activo)
            values($1,$2,'institucional',$3,true) returning id
            """, $"rol_{Guid.NewGuid():N}", "Rol prueba", institucion);
        foreach (var codigo in permisos)
        {
            await ExecuteAsync("""
                insert into public.roles_permisos(rol_id,permiso_id)
                select $1,id from public.permisos where codigo=$2
                """, rolId, codigo);
        }
        return rolId;
    }

    private async Task<string> BackendScalarTextAsync(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var auth = new NpgsqlCommand(
                "select set_config('request.jwt.claim.sub',$1,true)", connection, transaction))
            {
                auth.Parameters.AddWithValue(authUserId.ToString());
                await auth.ExecuteNonQueryAsync();
            }
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            var result = (string)(await command.ExecuteScalarAsync())!;
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string> AuthenticatedDirectScalarTextAsync(
        Guid authUserId,
        string sql,
        params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var role = new NpgsqlCommand("set local role authenticated", connection, transaction))
                await role.ExecuteNonQueryAsync();
            await using (var auth = new NpgsqlCommand(
                "select set_config('request.jwt.claim.sub',$1,true)", connection, transaction))
            {
                auth.Parameters.AddWithValue(authUserId.ToString());
                await auth.ExecuteNonQueryAsync();
            }
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            var result = (string)(await command.ExecuteScalarAsync())!;
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

    private async Task<long> ScalarLongAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value);
    }

    private sealed record Actor(Guid UsuarioId, Guid AuthUserId);
}
