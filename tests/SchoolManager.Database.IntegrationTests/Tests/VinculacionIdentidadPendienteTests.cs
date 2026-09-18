using System.Text.Json;
using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class VinculacionIdentidadPendienteTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Aceptar_y_aprobar_vincula_la_identidad_de_la_invitacion()
    {
        var institucion = await CrearInstitucionAsync();
        var operadorInvitacion = await CrearActorAsync(
            institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles"]);
        var aprobador = await CrearActorAsync(
            institucion,
            ["identidad.usuarios.editar"]);
        var preparada = await PrepararYEnviarAsync(institucion, operadorInvitacion);
        var authSolicitado = Guid.NewGuid();

        var aceptada = await ScalarTextAsync(
            "select public.rpc_solicitar_vinculacion_invitacion($1,$2)::text",
            preparada.TokenHash,
            authSolicitado);
        using var aceptadaJson = JsonDocument.Parse(aceptada);
        Assert.Equal("aceptada", aceptadaJson.RootElement.GetProperty("estado").GetString());

        var aprobada = await BackendScalarTextAsync(
            aprobador.AuthUserId,
            "select public.rpc_operar_vinculacion_identidad($1,$2,$3,$4,$5)::text",
            preparada.InvitacionId,
            preparada.UsuarioId,
            institucion,
            "aprobar",
            string.Empty);
        using var aprobadaJson = JsonDocument.Parse(aprobada);
        Assert.Equal("aprobada", aprobadaJson.RootElement.GetProperty("estado").GetString());

        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios where id=$1 and auth_user_id=$2",
            preparada.UsuarioId,
            authSolicitado));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.invitaciones_acceso where id=$1 and estado='aprobada'",
            preparada.InvitacionId));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.seguridad_auditoria
            where entidad_id=$1 and accion='identidad.vinculacion.aprobar'
            """, preparada.InvitacionId));
    }

    [Fact]
    public async Task Rechazar_no_modifica_auth_user_id()
    {
        var institucion = await CrearInstitucionAsync();
        var operadorInvitacion = await CrearActorAsync(
            institucion,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles"]);
        var aprobador = await CrearActorAsync(
            institucion,
            ["identidad.usuarios.editar"]);
        var preparada = await PrepararYEnviarAsync(institucion, operadorInvitacion);
        var authSolicitado = Guid.NewGuid();

        await ScalarTextAsync(
            "select public.rpc_solicitar_vinculacion_invitacion($1,$2)::text",
            preparada.TokenHash,
            authSolicitado);

        var rechazada = await BackendScalarTextAsync(
            aprobador.AuthUserId,
            "select public.rpc_operar_vinculacion_identidad($1,$2,$3,$4,$5)::text",
            preparada.InvitacionId,
            preparada.UsuarioId,
            institucion,
            "rechazar",
            "Identidad no reconocida");
        using var rechazadaJson = JsonDocument.Parse(rechazada);
        Assert.Equal("rechazada", rechazadaJson.RootElement.GetProperty("estado").GetString());

        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios where id=$1 and auth_user_id is null",
            preparada.UsuarioId));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.seguridad_auditoria
            where entidad_id=$1 and accion='identidad.vinculacion.rechazar'
              and detalle->>'motivo'='Identidad no reconocida'
            """, preparada.InvitacionId));
    }

    [Fact]
    public async Task Operador_de_otra_institucion_no_puede_aprobar()
    {
        var institucionA = await CrearInstitucionAsync();
        var institucionB = await CrearInstitucionAsync();
        var operadorInvitacion = await CrearActorAsync(
            institucionA,
            ["identidad.usuarios.crear", "identidad.usuarios.asignar_roles"]);
        var aprobadorB = await CrearActorAsync(
            institucionB,
            ["identidad.usuarios.editar"]);
        var preparada = await PrepararYEnviarAsync(institucionA, operadorInvitacion);

        await ScalarTextAsync(
            "select public.rpc_solicitar_vinculacion_invitacion($1,$2)::text",
            preparada.TokenHash,
            Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<PostgresException>(() => BackendScalarTextAsync(
            aprobadorB.AuthUserId,
            "select public.rpc_operar_vinculacion_identidad($1,$2,$3,$4,$5)::text",
            preparada.InvitacionId,
            preparada.UsuarioId,
            institucionB,
            "aprobar",
            string.Empty));

        Assert.Equal("42501", ex.SqlState);
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios where id=$1 and auth_user_id is null",
            preparada.UsuarioId));
    }

    private async Task<InvitacionPreparada> PrepararYEnviarAsync(Guid institucion, Actor actor)
    {
        var rolDestino = await CrearRolAsync(institucion, []);
        var correo = $"vinculacion.{Guid.NewGuid():N}@schoolmanager.test";
        var preparada = await BackendScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_preparar_invitacion_usuario($1,$2,$3,$4,$5,$6)::text",
            institucion,
            "Invitado",
            "Vinculacion",
            correo,
            rolDestino,
            "administracion");
        using var preparadaJson = JsonDocument.Parse(preparada);
        var invitacionId = preparadaJson.RootElement.GetProperty("invitacionId").GetGuid();
        var usuarioId = preparadaJson.RootElement.GetProperty("usuarioId").GetGuid();

        var tokenHash = Convert.ToHexString(Guid.NewGuid().ToByteArray())
            .ToLowerInvariant()
            .PadRight(64, 'a')[..64];

        var emitida = await BackendScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_emitir_invitacion_acceso($1,$2,$3)::text",
            invitacionId,
            tokenHash,
            DateTimeOffset.UtcNow.AddHours(24));
        using var emitidaJson = JsonDocument.Parse(emitida);
        var version = emitidaJson.RootElement.GetProperty("emisionVersion").GetInt64();

        await BackendScalarTextAsync(
            actor.AuthUserId,
            "select public.rpc_confirmar_envio_invitacion($1,$2,$3,$4)::text",
            invitacionId,
            version,
            "test",
            $"msg-{Guid.NewGuid():N}");

        return new InvitacionPreparada(invitacionId, usuarioId, tokenHash);
    }

    private async Task<Guid> CrearInstitucionAsync() =>
        await ScalarGuidAsync(
            "insert into public.instituciones(nombre) values($1) returning id",
            $"Institucion {Guid.NewGuid():N}");

    private async Task<Actor> CrearActorAsync(Guid institucion, IEnumerable<string> permisos)
    {
        var authId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync(
            "insert into public.usuarios(auth_user_id,activo) values($1,true) returning id",
            authId);
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

    private async Task<string> ScalarTextAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (string)(await command.ExecuteScalarAsync())!;
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
    private sealed record InvitacionPreparada(Guid InvitacionId, Guid UsuarioId, string TokenHash);
}
