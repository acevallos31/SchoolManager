using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RlsSecurityTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Usuario_institucional_solo_ve_alumnos_de_su_institucion()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        await InsertAlumnoAsync(institucionA);
        await InsertAlumnoAsync(institucionB);
        var usuario = await InsertUsuarioAsync("operador", institucionA);

        Assert.Equal(1, await AuthScalarLongAsync(
            usuario.AuthUserId, "select count(*) from public.alumnos"));
    }

    [Fact]
    public async Task Rol_global_con_permiso_ve_todas_las_instituciones()
    {
        await InsertAlumnoAsync(await InsertInstitucionAsync());
        await InsertAlumnoAsync(await InsertInstitucionAsync());
        var usuario = await InsertUsuarioAsync("admin");

        Assert.True(await AuthScalarLongAsync(
            usuario.AuthUserId, "select count(*) from public.alumnos") >= 2);
    }

    [Fact]
    public async Task Sin_permiso_usuario_inactivo_y_rol_inactivo_no_ven_alumnos()
    {
        var institucion = await InsertInstitucionAsync();
        await InsertAlumnoAsync(institucion);

        var sinPermiso = await InsertUsuarioAsync("usuario", institucion);
        Assert.Equal(0, await AuthScalarLongAsync(
            sinPermiso.AuthUserId, "select count(*) from public.alumnos"));

        var inactivo = await InsertUsuarioAsync("operador", institucion, activo: false);
        Assert.Equal(0, await AuthScalarLongAsync(
            inactivo.AuthUserId, "select count(*) from public.alumnos"));

        var rolCodigo = $"rol_inactivo_{Guid.NewGuid():N}";
        var rolId = await ScalarGuidAsync(
            "insert into public.roles (codigo, nombre, activo) values ($1, 'Inactivo', false) returning id",
            rolCodigo);
        var permisoId = await ScalarGuidAsync(
            "select id from public.permisos where codigo = 'academico.alumnos.ver'");
        await ExecuteAsync(
            "insert into public.roles_permisos (rol_id, permiso_id) values ($1, $2)", rolId, permisoId);
        var conRolInactivo = await InsertUsuarioConRolIdAsync(rolId, institucion);
        Assert.Equal(0, await AuthScalarLongAsync(
            conRolInactivo.AuthUserId, "select count(*) from public.alumnos"));
    }

    [Fact]
    public async Task Rpc_matricula_exige_permiso_en_la_institucion_correcta()
    {
        var contextoA = await CreateContextAsync();
        var contextoB = await CreateContextAsync();
        var alumnoA = await InsertAlumnoAsync(contextoA.InstitucionId);
        var alumnoB = await InsertAlumnoAsync(contextoB.InstitucionId);
        var sinPermiso = await InsertUsuarioAsync("usuario", contextoA.InstitucionId);

        await Assert.ThrowsAsync<PostgresException>(() => AuthScalarGuidAsync(
            sinPermiso.AuthUserId,
            "select public.rpc_matricular_alumno($1, $2, $3)",
            alumnoA, contextoA.SeccionId, contextoA.PeriodoId));

        var operadorA = await InsertUsuarioAsync("operador", contextoA.InstitucionId);
        var matricula = await AuthScalarGuidAsync(
            operadorA.AuthUserId,
            "select public.rpc_matricular_alumno($1, $2, $3)",
            alumnoA, contextoA.SeccionId, contextoA.PeriodoId);
        Assert.NotEqual(Guid.Empty, matricula);

        await Assert.ThrowsAsync<PostgresException>(() => AuthScalarGuidAsync(
            operadorA.AuthUserId,
            "select public.rpc_matricular_alumno($1, $2, $3)",
            alumnoB, contextoB.SeccionId, contextoB.PeriodoId));
    }

    [Fact]
    public async Task Authenticated_no_puede_escribir_directamente_tablas_criticas()
    {
        var contexto = await CreateContextAsync();
        var alumno = await InsertAlumnoAsync(contexto.InstitucionId);
        var matricula = await ScalarGuidAsync(
            "select public.matricular_alumno($1, $2, $3)",
            alumno, contexto.SeccionId, contexto.PeriodoId);
        var usuario = await InsertUsuarioAsync("admin");

        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            usuario.AuthUserId,
            "insert into public.alumnos (persona_id, institucion_id) values ($1, $2)",
            Guid.NewGuid(), contexto.InstitucionId));
        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            usuario.AuthUserId,
            "update public.matriculas set estado = 'activa' where id = $1", matricula));
        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            usuario.AuthUserId,
            "delete from public.matriculas where id = $1", matricula));
        await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            usuario.AuthUserId,
            "insert into public.matricula_estado_historial (matricula_id, estado_nuevo) values ($1, 'activa')",
            matricula));
    }

    [Fact]
    public async Task Usuario_normal_no_asigna_roles_y_admin_global_si()
    {
        var institucion = await InsertInstitucionAsync();
        var normal = await InsertUsuarioAsync("usuario", institucion);
        var admin = await InsertUsuarioAsync("admin");
        var destino = await InsertUsuarioAsync("usuario", institucion);

        await Assert.ThrowsAsync<PostgresException>(() => AuthScalarGuidAsync(
            normal.AuthUserId,
            "select public.rpc_asignar_rol_usuario($1, 'consulta', $2)",
            destino.UsuarioId, institucion));

        var asignacionId = await AuthScalarGuidAsync(
            admin.AuthUserId,
            "select public.rpc_asignar_rol_usuario($1, 'consulta', $2)",
            destino.UsuarioId, institucion);
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where id = $1 and institucion_id = $2 and activo",
            asignacionId, institucion));
    }

    [Fact]
    public async Task Padre_solo_ve_alumno_representado()
    {
        var institucion = await InsertInstitucionAsync();
        var representado = await InsertAlumnoAsync(institucion);
        await InsertAlumnoAsync(institucion);
        var padre = await InsertUsuarioAsync("padre", institucion);
        var responsableId = await ScalarGuidAsync(
            "insert into public.responsables (persona_id, institucion_id) values ($1, $2) returning id",
            padre.PersonaId, institucion);
        await ExecuteAsync(
            "insert into public.alumno_responsable (alumno_id, responsable_id) values ($1, $2)",
            representado, responsableId);

        Assert.Equal(representado, await AuthScalarGuidAsync(
            padre.AuthUserId, "select id from public.alumnos"));
        Assert.Equal(1, await AuthScalarLongAsync(
            padre.AuthUserId, "select count(*) from public.alumnos"));
    }

    [Fact]
    public async Task Usuario_alumno_solo_ve_su_propio_perfil()
    {
        var institucion = await InsertInstitucionAsync();
        var usuario = await InsertUsuarioAsync("usuario", institucion);
        var alumnoPropio = await ScalarGuidAsync(
            "insert into public.alumnos (persona_id, institucion_id) values ($1, $2) returning id",
            usuario.PersonaId, institucion);
        await InsertAlumnoAsync(institucion);

        Assert.Equal(alumnoPropio, await AuthScalarGuidAsync(
            usuario.AuthUserId, "select id from public.alumnos"));
    }

    [Fact]
    public async Task Anon_no_puede_leer_datos_privados_ni_ejecutar_rpc()
    {
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsAnonAsync(
            "select count(*) from public.alumnos"));
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsAnonAsync(
            "select public.rpc_reactivar_alumno($1)", Guid.NewGuid()));
    }

    private async Task<Contexto> CreateContextAsync()
    {
        var institucion = await InsertInstitucionAsync();
        var ciclo = await ScalarGuidAsync(
            "insert into public.ciclos_escolares (institucion_id, nombre) values ($1, $2) returning id",
            institucion, $"Ciclo {Guid.NewGuid():N}");
        var grado = await ScalarGuidAsync(
            "insert into public.grados (nombre) values ($1) returning id", $"Grado {Guid.NewGuid():N}");
        var periodo = await ScalarGuidAsync(
            "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1, $2, current_date, current_date + 30) returning id",
            ciclo, $"Periodo {Guid.NewGuid():N}");
        var seccion = await ScalarGuidAsync(
            "select public.crear_seccion($1, $2, $3, null, 'A')", institucion, ciclo, grado);
        return new Contexto(institucion, seccion, periodo);
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private async Task<Guid> InsertAlumnoAsync(Guid institucionId)
    {
        var personaId = await ScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Alumno', $1) returning id",
            Guid.NewGuid().ToString("N"));
        return await ScalarGuidAsync(
            "insert into public.alumnos (persona_id, institucion_id) values ($1, $2) returning id",
            personaId, institucionId);
    }

    private async Task<Identidad> InsertUsuarioAsync(
        string rolCodigo,
        Guid? institucionId = null,
        bool activo = true)
    {
        var rolId = await ScalarGuidAsync("select id from public.roles where codigo = $1", rolCodigo);
        return await InsertUsuarioConRolIdAsync(rolId, institucionId, activo);
    }

    private async Task<Identidad> InsertUsuarioConRolIdAsync(
        Guid rolId,
        Guid? institucionId,
        bool activo = true)
    {
        var personaId = await ScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUserId = Guid.NewGuid();
        var usuarioId = await ScalarGuidAsync(
            "insert into public.usuarios (persona_id, auth_user_id, activo) values ($1, $2, $3) returning id",
            personaId, authUserId, activo);
        if (institucionId.HasValue)
        {
            await ExecuteAsync(
                "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)",
                usuarioId, rolId, institucionId.Value);
        }
        else
        {
            await ExecuteAsync(
                "insert into public.usuarios_roles (usuario_id, rol_id) values ($1, $2)", usuarioId, rolId);
        }
        return new Identidad(usuarioId, personaId, authUserId);
    }

    private Task<long> AuthScalarLongAsync(Guid authUserId, string sql, params object[] values) =>
        AuthScalarAsync<long>(authUserId, sql, values);

    private Task<Guid> AuthScalarGuidAsync(Guid authUserId, string sql, params object[] values) =>
        AuthScalarAsync<Guid>(authUserId, sql, values);

    private async Task<T> AuthScalarAsync<T>(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await SetAuthenticatedAsync(connection, transaction, authUserId);
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

    private async Task AuthExecuteAsync(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await SetAuthenticatedAsync(connection, transaction, authUserId);
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

    private async Task ExecuteAsAnonAsync(string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var roleCommand = new NpgsqlCommand(
                "set local role anon", connection, transaction);
            await roleCommand.ExecuteNonQueryAsync();
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

    private static async Task SetAuthenticatedAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid authUserId)
    {
        await using var roleCommand = new NpgsqlCommand(
            "set local role authenticated", connection, transaction);
        await roleCommand.ExecuteNonQueryAsync();
        await using var authCommand = new NpgsqlCommand(
            "select set_config('request.jwt.claim.sub', $1, true)", connection, transaction);
        authCommand.Parameters.AddWithValue(authUserId.ToString());
        await authCommand.ExecuteNonQueryAsync();
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
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    private sealed record Identidad(Guid UsuarioId, Guid PersonaId, Guid AuthUserId);
    private sealed record Contexto(Guid InstitucionId, Guid SeccionId, Guid PeriodoId);
}
