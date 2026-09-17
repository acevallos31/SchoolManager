using System.Text.Json;
using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class InvitacionesPersistenciaTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Emitir_requiere_confirmacion_real_y_reemision_invalida_version_anterior()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);
        var correo = $"persistencia.{Guid.NewGuid():N}@schoolmanager.test";

        var preparada = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
            """, institucion, "Invitado", "Persistente", correo, rolDestino, "administracion");
        using var preparadaJson = JsonDocument.Parse(preparada);
        var invitacionId = preparadaJson.RootElement.GetProperty("invitacionId").GetGuid();

        var hash1 = new string('a', 64);
        var emitida1 = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_emitir_invitacion_acceso($1,$2,$3)::text
            """, invitacionId, hash1, DateTimeOffset.UtcNow.AddHours(24));
        using var emitida1Json = JsonDocument.Parse(emitida1);
        var version1 = emitida1Json.RootElement.GetProperty("emisionVersion").GetInt64();

        Assert.Equal("pendiente", emitida1Json.RootElement.GetProperty("estado").GetString());
        Assert.Equal(1, version1);
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.invitaciones_acceso
            where id=$1 and token_hash=$2 and token_emitido_at is not null
              and expira_at is not null and enviado_at is null
              and intentos_envio=1 and emision_version=1 and estado='pendiente'
            """, invitacionId, hash1));

        var confirmada1 = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_confirmar_envio_invitacion($1,$2,$3,$4)::text
            """, invitacionId, version1, "resend", "msg-1");
        using var confirmada1Json = JsonDocument.Parse(confirmada1);
        Assert.Equal("enviada", confirmada1Json.RootElement.GetProperty("estado").GetString());
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.invitaciones_acceso
            where id=$1 and estado='enviada' and enviado_at is not null
              and proveedor_envio='resend' and proveedor_mensaje_id='msg-1'
            """, invitacionId));

        var hash2 = new string('b', 64);
        var emitida2 = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_emitir_invitacion_acceso($1,$2,$3)::text
            """, invitacionId, hash2, DateTimeOffset.UtcNow.AddHours(36));
        using var emitida2Json = JsonDocument.Parse(emitida2);
        var version2 = emitida2Json.RootElement.GetProperty("emisionVersion").GetInt64();

        Assert.Equal(2, version2);
        Assert.Equal(0, await ScalarLongAsync(
            "select count(*) from public.invitaciones_acceso where id=$1 and token_hash=$2",
            invitacionId, hash1));

        var stale = await Assert.ThrowsAsync<PostgresException>(() => BackendScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_confirmar_envio_invitacion($1,$2,$3,$4)::text",
            invitacionId, version1, "resend", "msg-stale"));
        Assert.Equal("P0001", stale.SqlState);

        await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_confirmar_envio_invitacion($1,$2,$3,$4)::text
            """, invitacionId, version2, "resend", "msg-2");
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.invitaciones_acceso
            where id=$1 and token_hash=$2 and emision_version=2
              and intentos_envio=2 and estado='enviada' and proveedor_mensaje_id='msg-2'
            """, invitacionId, hash2));
    }

    [Fact]
    public async Task Error_de_proveedor_deja_invitacion_pendiente_y_elimina_token_no_entregado()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);
        var preparada = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
            """, institucion, "Proveedor", "Falla",
            $"error.{Guid.NewGuid():N}@schoolmanager.test", rolDestino, "administracion");
        using var preparadaJson = JsonDocument.Parse(preparada);
        var invitacionId = preparadaJson.RootElement.GetProperty("invitacionId").GetGuid();

        var emitida = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_emitir_invitacion_acceso($1,$2,$3)::text
            """, invitacionId, new string('d', 64), DateTimeOffset.UtcNow.AddHours(24));
        using var emitidaJson = JsonDocument.Parse(emitida);
        var version = emitidaJson.RootElement.GetProperty("emisionVersion").GetInt64();

        await BackendExecuteAsync(actor.AuthUserId, """
            select public.rpc_registrar_error_envio_invitacion($1,$2,$3)
            """, invitacionId, version, "provider_unavailable");

        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.invitaciones_acceso
            where id=$1 and estado='pendiente' and token_hash is null
              and token_emitido_at is null and expira_at is null and enviado_at is null
              and ultimo_error_envio='provider_unavailable' and emision_version=$2
            """, invitacionId, version));
    }

    [Fact]
    public async Task Emitir_invitacion_no_es_ejecutable_directamente_por_authenticated()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);
        var preparada = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
            """, institucion, "Directo", "Bloqueado",
            $"directo.{Guid.NewGuid():N}@schoolmanager.test", rolDestino, "administracion");
        using var json = JsonDocument.Parse(preparada);
        var invitacionId = json.RootElement.GetProperty("invitacionId").GetGuid();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AuthenticatedDirectScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_emitir_invitacion_acceso($1,$2,$3)::text",
            invitacionId, new string('c', 64), DateTimeOffset.UtcNow.AddHours(24)));

        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task Rls_oculta_invitacion_a_authenticated_aunque_reciba_select_accidentalmente()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);
        var preparada = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
            """, institucion, "RLS", "Protegido",
            $"rls.{Guid.NewGuid():N}@schoolmanager.test", rolDestino, "administracion");
        using var json = JsonDocument.Parse(preparada);
        var invitacionId = json.RootElement.GetProperty("invitacionId").GetGuid();

        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var grant = new NpgsqlCommand(
                "grant select on public.invitaciones_acceso to authenticated", connection, transaction))
                await grant.ExecuteNonQueryAsync();

            await using (var role = new NpgsqlCommand("set local role authenticated", connection, transaction))
                await role.ExecuteNonQueryAsync();

            await using var command = new NpgsqlCommand(
                "select count(*) from public.invitaciones_acceso where id=$1", connection, transaction);
            command.Parameters.AddWithValue(invitacionId);
            var visible = Convert.ToInt64(await command.ExecuteScalarAsync());
            Assert.Equal(0, visible);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Emitir_invitacion_rechaza_hash_que_no_sea_sha256_hex_minuscula(string hashInvalido)
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var actor = await CrearActorAsync(institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles", "academico.alumnos.ver"]);
        var rolDestino = await CrearRolAsync(institucion, ["academico.alumnos.ver"]);
        var preparada = await BackendScalarTextAsync(actor.AuthUserId, """
            select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text
            """, institucion, "Hash", "Invalido",
            $"hash.{Guid.NewGuid():N}@schoolmanager.test", rolDestino, "administracion");
        using var json = JsonDocument.Parse(preparada);
        var invitacionId = json.RootElement.GetProperty("invitacionId").GetGuid();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => BackendScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_emitir_invitacion_acceso($1,$2,$3)::text",
            invitacionId, hashInvalido, DateTimeOffset.UtcNow.AddHours(24)));

        Assert.Equal("22023", ex.SqlState);
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

    private async Task BackendExecuteAsync(Guid authUserId, string sql, params object[] values)
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
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
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
