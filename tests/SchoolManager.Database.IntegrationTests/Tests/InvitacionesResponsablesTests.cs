using System.Text.Json;
using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class InvitacionesResponsablesTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Preparar_responsable_reutiliza_persona_exacta_y_es_idempotente()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "identidad.roles.crear"]);
        var correo = $"responsable.{Guid.NewGuid():N}@schoolmanager.test";
        var personaObjetivo = await ScalarGuidAsync("""
            insert into public.personas(nombres,apellidos,correo)
            values('Persona','Objetivo',$1) returning id
            """, correo);
        var personaMismoCorreo = await ScalarGuidAsync("""
            insert into public.personas(nombres,apellidos,correo)
            values('Persona','No Objetivo',$1) returning id
            """, correo);
        var responsable = await ScalarGuidAsync("""
            insert into public.responsables(persona_id,institucion_id)
            values($1,$2) returning id
            """, personaObjetivo, institucion);

        var primera = await BackendScalarTextAsync(actor.AuthUserId,
            "select public.rpc_preparar_invitacion_responsable($1)::text", responsable);
        var segunda = await BackendScalarTextAsync(actor.AuthUserId,
            "select public.rpc_preparar_invitacion_responsable($1)::text", responsable);

        using var json1 = JsonDocument.Parse(primera);
        using var json2 = JsonDocument.Parse(segunda);
        var root1 = json1.RootElement;
        var root2 = json2.RootElement;
        var usuarioId = root1.GetProperty("usuarioId").GetGuid();
        var rolId = root1.GetProperty("rolId").GetGuid();
        var invitacionId = root1.GetProperty("invitacionId").GetGuid();

        Assert.Equal(personaObjetivo, root1.GetProperty("personaId").GetGuid());
        Assert.NotEqual(personaMismoCorreo, root1.GetProperty("personaId").GetGuid());
        Assert.True(root1.GetProperty("usuarioCreado").GetBoolean());
        Assert.True(root1.GetProperty("rolCreado").GetBoolean());
        Assert.True(root1.GetProperty("asignacionCreada").GetBoolean());
        Assert.True(root1.GetProperty("invitacionCreada").GetBoolean());
        Assert.False(root2.GetProperty("usuarioCreado").GetBoolean());
        Assert.False(root2.GetProperty("rolCreado").GetBoolean());
        Assert.False(root2.GetProperty("asignacionCreada").GetBoolean());
        Assert.False(root2.GetProperty("invitacionCreada").GetBoolean());
        Assert.Equal(usuarioId, root2.GetProperty("usuarioId").GetGuid());
        Assert.Equal(invitacionId, root2.GetProperty("invitacionId").GetGuid());
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios where id=$1 and persona_id=$2 and auth_user_id is null",
            usuarioId, personaObjetivo));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.roles r
            join public.roles b on b.id=r.rol_base_id
            where r.id=$1 and r.institucion_id=$2 and r.codigo='parent'
              and r.tipo='institucional' and b.codigo='parent' and b.tipo='plantilla'
            """, rolId, institucion));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.invitaciones_acceso
            where id=$1 and persona_id=$2 and usuario_id=$3 and rol_id=$4
              and origen='responsable' and estado='pendiente'
            """, invitacionId, personaObjetivo, usuarioId, rolId));
    }

    [Fact]
    public async Task Preparar_responsable_rechaza_actor_sin_autoridad_y_correo_vacio()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actorSinPermiso = await CrearActorAsync(institucion, ["academico.responsables.ver"]);
        var persona = await ScalarGuidAsync("""
            insert into public.personas(nombres,apellidos,correo)
            values('Sin','Permiso','sin-permiso@example.test') returning id
            """);
        var responsable = await ScalarGuidAsync("""
            insert into public.responsables(persona_id,institucion_id)
            values($1,$2) returning id
            """, persona, institucion);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => BackendScalarTextAsync(
            actorSinPermiso.AuthUserId,
            "select public.rpc_preparar_invitacion_responsable($1)::text", responsable));
        Assert.Equal("42501", ex.SqlState);

        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "identidad.roles.crear"]);
        var personaSinCorreo = await ScalarGuidAsync("""
            insert into public.personas(nombres,apellidos)
            values('Sin','Correo') returning id
            """);
        var responsableSinCorreo = await ScalarGuidAsync("""
            insert into public.responsables(persona_id,institucion_id)
            values($1,$2) returning id
            """, personaSinCorreo, institucion);
        var exCorreo = await Assert.ThrowsAsync<PostgresException>(() => BackendScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_preparar_invitacion_responsable($1)::text", responsableSinCorreo));
        Assert.Equal("22023", exCorreo.SqlState);
    }

    [Fact]
    public async Task Preparar_responsable_no_es_ejecutable_directamente_por_authenticated()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "identidad.roles.crear"]);
        var persona = await ScalarGuidAsync("""
            insert into public.personas(nombres,apellidos,correo)
            values('Directo','Bloqueado',$1) returning id
            """, $"directo.{Guid.NewGuid():N}@example.test");
        var responsable = await ScalarGuidAsync("""
            insert into public.responsables(persona_id,institucion_id)
            values($1,$2) returning id
            """, persona, institucion);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AuthenticatedDirectScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_preparar_invitacion_responsable($1)::text", responsable));
        Assert.Equal("42501", ex.SqlState);
    }

    private async Task<Actor> CrearActorAsync(Guid institucion, IEnumerable<string> permisos)
    {
        var authId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync(
            "insert into public.usuarios(auth_user_id,activo) values($1,true) returning id", authId);
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
        await ExecuteAsync("""
            insert into public.usuarios_roles(usuario_id,rol_id,institucion_id)
            values($1,$2,$3)
            """, usuarioId, rolId, institucion);
        return new Actor(usuarioId, authId);
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
        Guid authUserId, string sql, params object[] values)
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
