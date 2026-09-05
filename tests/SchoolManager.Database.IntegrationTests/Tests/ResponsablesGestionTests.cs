using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

// Cobertura DB del Bloque 018 (migración 017): RPC de gestión de responsables
// y sus invariantes (persona+institución único, un principal activo por alumno,
// aislamiento por institución, sin DELETE físico).
public sealed class ResponsablesGestionTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Crear_responsable_con_documento_devuelve_responsable()
    {
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");

        var responsableId = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucion, "Padre", "Prueba", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);

        Assert.NotEqual(Guid.Empty, responsableId);
        Assert.Equal(1, await AuthScalarLongAsync(admin.AuthUserId,
            "select count(*) from public.responsables where id = $1", responsableId));
    }

    [Fact]
    public async Task Duplicado_persona_institucion_es_rechazado()
    {
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var doc = $"DOC-{Guid.NewGuid():N}";

        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucion, "Padre", "Uno", "DNI", doc, null, null);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucion, "Padre", "Dos", "DNI", doc, null, null));
        Assert.Equal("23505", exception.SqlState);
    }

    [Fact]
    public async Task Misma_persona_reutilizada_en_otra_institucion_sin_duplicar_identidad()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var doc = $"DOC-{Guid.NewGuid():N}";

        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucionA, "Global", "Uno", "DNI", doc, null, null);
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucionB, "Global", "Uno", "DNI", doc, null, null);

        var docNormalizado = new string(doc.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.personas where numero_identificacion_normalizado = $1",
            docNormalizado));
        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.responsables where institucion_id = $1", institucionA));
        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.responsables where institucion_id = $1", institucionB));
    }

    [Fact]
    public async Task Responsable_sin_permiso_en_institucion_es_rechazado()
    {
        var institucion = await InsertInstitucionAsync();
        var manual = await InsertUsuarioGlobalAsync("usuario"); // rol sin permisos

        var exception = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            manual.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucion, "Padre", "Prueba", "DNI", $"DOC-{Guid.NewGuid():N}", null, null));
        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task Vincular_responsable_a_alumno_y_un_solo_principal_activo()
    {
        var contexto = await CreateContextoAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var alumno = await InsertAlumnoAsync(contexto);
        var padre = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Padre", "Vincular", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);
        var madre = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Madre", "Vincular", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);

        // Padre principal
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, padre, "PADRE", true, true);

        // Madre no principal
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, madre, "MADRE", false, true);

        // Solo un principal activo
        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo'",
            alumno));

        // Mover principal a la madre libera al padre
        var vinculoMadre = await AdminScalarGuidAsync(
            "select ar.id from public.alumno_responsable ar join public.responsables r on r.id=ar.responsable_id where ar.alumno_id=$1 and r.persona_id=(select persona_id from public.responsables where id=$2)",
            alumno, madre);
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_editar_vinculo_responsable($1,$2,$3,$4)",
            vinculoMadre, "MADRE", true, true);

        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo'",
            alumno));
    }

    [Fact]
    public async Task Reasignar_principal_via_vincular_libera_al_anterior()
    {
        var contexto = await CreateContextoAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var alumno = await InsertAlumnoAsync(contexto);
        var responsableA = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Padre", "PrincipalA", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);
        var responsableB = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Madre", "PrincipalB", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);

        // A principal
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, responsableA, "PADRE", true, true);

        // Reasignar: vincular B principal con A ya principal no debe violar
        // ux_alumno_responsable_principal_activo (antes fallaba con 23514).
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, responsableB, "MADRE", true, true);

        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo'",
            alumno));
        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo' and responsable_id = $2",
            alumno, responsableB));
        Assert.Equal(0, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo' and responsable_id = $2",
            alumno, responsableA));
    }

    [Fact]
    public async Task Reasignar_principal_reactivando_vinculo_inactivo_libera_al_anterior()
    {
        var contexto = await CreateContextoAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var alumno = await InsertAlumnoAsync(contexto);
        var responsableA = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Padre", "ReactA", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);
        var responsableB = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Madre", "ReactB", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);

        // A principal, B no principal
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, responsableA, "PADRE", true, true);
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, responsableB, "MADRE", false, true);

        // Desactivar el vinculo de B
        var vinculoB = await AdminScalarGuidAsync(
            "select ar.id from public.alumno_responsable ar where ar.alumno_id=$1 and ar.responsable_id=$2 and estado='activo'",
            alumno, responsableB);
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_desactivar_vinculo_responsable($1,$2)",
            vinculoB, "Prueba temporal");

        // Reactivar vinculo de B como principal con A aun principal
        // (no debe violar ux_alumno_responsable_principal_activo).
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, responsableB, "MADRE", true, true);

        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo'",
            alumno));
        Assert.Equal(0, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo' and responsable_id = $2",
            alumno, responsableA));
        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where alumno_id = $1 and es_principal and estado='activo' and responsable_id = $2",
            alumno, responsableB));
    }

    [Fact]
    public async Task Vincular_a_traves_de_instituciones_es_rechazado()
    {
        var contextoA = await CreateContextoAsync();
        var contextoB = await CreateContextoAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var alumnoA = await InsertAlumnoAsync(contextoA);
        var responsableB = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contextoB.InstitucionId, "Otro", "Colegio", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumnoA, responsableB, "X", false, false));
        Assert.Equal("23514", exception.SqlState);
    }

    [Fact]
    public async Task Desactivar_y_reactivar_responsable_sin_delete_fisico()
    {
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var responsable = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucion, "Padre", "Desactivar", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);

        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_inactivar_responsable($1,$2)", responsable, "Se retira del colegio");

        Assert.Equal("inactivo", await AdminScalarStringAsync(
            "select estado from public.responsables where id = $1", responsable));
        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.responsables where id = $1", responsable)); // sin DELETE

        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_reactivar_responsable($1)", responsable);
        Assert.Equal("activo", await AdminScalarStringAsync(
            "select estado from public.responsables where id = $1", responsable));
    }

    [Fact]
    public async Task Reactivar_vinculo_rechazado_si_alumno_inactivo()
    {
        var contexto = await CreateContextoAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var alumno = await InsertAlumnoAsync(contexto);
        var responsable = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Padre", "AluInact", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, responsable, "PADRE", true, true);

        // Desactivar el vinculo, luego inactivar al alumno.
        var vinculo = await AdminScalarGuidAsync(
            "select ar.id from public.alumno_responsable ar where ar.alumno_id=$1 and ar.responsable_id=$2 and estado='activo'",
            alumno, responsable);
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_desactivar_vinculo_responsable($1,$2)", vinculo, "Temporal");
        await AdminExecuteAsync(
            "update public.alumnos set estado = 'inactivo' where id = $1", alumno);

        // Reactivar debe rechazarse: el alumno esta inactivo.
        var exception = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin.AuthUserId,
            "select public.rpc_reactivar_vinculo_responsable($1)", vinculo));
        Assert.Equal("22023", exception.SqlState);

        // El vinculo sigue inactivo y el responsable permanece activo.
        Assert.Equal("inactivo", await AdminScalarStringAsync(
            "select estado from public.alumno_responsable where id = $1", vinculo));
        Assert.Equal("activo", await AdminScalarStringAsync(
            "select r.estado from public.responsables r join public.alumno_responsable ar on ar.responsable_id=r.id where ar.id = $1",
            vinculo));
    }

    [Fact]
    public async Task Editar_datos_permitidos_del_responsable()
    {
        var institucion = await InsertInstitucionAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var responsable = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            institucion, "Padre", "Editar", "DNI", $"DOC-{Guid.NewGuid():N}", "5551", "a@b.com");

        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_editar_responsable($1,$2,$3,$4,$5)",
            responsable, "Padre Nuevo", "Editado", "5552", "c@d.com");

        Assert.Equal("Padre Nuevo", await AdminScalarStringAsync(
            "select p.nombres from public.responsables r join public.personas p on p.id=r.persona_id where r.id = $1",
            responsable));
        Assert.Equal("5552", await AdminScalarStringAsync(
            "select p.telefono from public.responsables r join public.personas p on p.id=r.persona_id where r.id = $1",
            responsable));
    }

    [Fact]
    public async Task Desactivar_responsable_desactiva_sus_vinculos_activos()
    {
        var contexto = await CreateContextoAsync();
        var admin = await InsertUsuarioGlobalAsync("admin");
        var alumno = await InsertAlumnoAsync(contexto);
        var responsable = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_responsable_con_documento($1,$2,$3,$4,$5,$6,$7)",
            contexto.InstitucionId, "Padre", "VincAct", "DNI", $"DOC-{Guid.NewGuid():N}", null, null);
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_vincular_alumno_responsable($1,$2,$3,$4,$5)",
            alumno, responsable, "PADRE", true, true);

        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_inactivar_responsable($1,$2)", responsable, "Retiro");

        Assert.Equal(1, await AdminScalarLongAsync(
            "select count(*) from public.alumno_responsable where responsable_id = $1 and estado='inactivo'",
            responsable));
    }

    // -----------------------------------------------------------------------
    // Helpers (patrón RlsSecurityTests / InstitutionConfigurationTests)
    // -----------------------------------------------------------------------
    private async Task<Contexto> CreateContextoAsync()
    {
        var institucion = await InsertInstitucionAsync();
        var ciclo = await AdminScalarGuidAsync(
            "insert into public.ciclos_escolares (nombre, institucion_id) values ($1, $2) returning id",
            $"Ciclo {Guid.NewGuid():N}", institucion);
        var grado = await AdminScalarGuidAsync(
            "insert into public.grados (nombre) values ($1) returning id",
            $"Grado {Guid.NewGuid():N}");
        var seccion = await AdminScalarGuidAsync(
            "insert into public.secciones (nombre, institucion_id, ciclo_id, grado_id) values ($1, $2, $3, $4) returning id",
            $"Seccion {Guid.NewGuid():N}", institucion, ciclo, grado);
        return new Contexto(institucion, seccion, ciclo, grado);
    }

    private Task<Guid> InsertInstitucionAsync() => AdminScalarGuidAsync(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private async Task<Guid> InsertAlumnoAsync(Contexto contexto)
    {
        var persona = await AdminScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Alumno', $1) returning id",
            Guid.NewGuid().ToString("N"));
        return await AdminScalarGuidAsync(
            "insert into public.alumnos (persona_id, institucion_id) values ($1, $2) returning id",
            persona, contexto.InstitucionId);
    }

    private async Task<Identidad> InsertUsuarioGlobalAsync(string rolCodigo)
    {
        var rolId = await AdminScalarGuidAsync("select id from public.roles where codigo = $1", rolCodigo);
        var personaId = await AdminScalarGuidAsync(
            "insert into public.personas (nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUserId = Guid.NewGuid();
        var usuarioId = await AdminScalarGuidAsync(
            "insert into public.usuarios (persona_id, auth_user_id, activo) values ($1, $2, true) returning id",
            personaId, authUserId);
        await AdminExecuteAsync(
            "insert into public.usuarios_roles (usuario_id, rol_id) values ($1, $2)", usuarioId, rolId);
        return new Identidad(usuarioId, personaId, authUserId);
    }

    private Task<Guid> AuthScalarGuidAsync(Guid authUserId, string sql, params object?[] values) =>
        AuthScalarAsync<Guid>(authUserId, sql, values);
    private Task<long> AuthScalarLongAsync(Guid authUserId, string sql, params object?[] values) =>
        AuthScalarAsync<long>(authUserId, sql, values);
    private Task AuthExecuteAsync(Guid authUserId, string sql, params object?[] values) =>
        AuthExecuteCoreAsync(authUserId, sql, values);

    private async Task<T> AuthScalarAsync<T>(Guid authUserId, string sql, params object?[] values)
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

    private async Task AuthExecuteCoreAsync(Guid authUserId, string sql, params object?[] values)
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

    private async Task<Guid> AdminScalarGuidAsync(string sql, params object?[] values) =>
        await AdminScalarAsync<Guid>(sql, values);
    private async Task<long> AdminScalarLongAsync(string sql, params object?[] values) =>
        await AdminScalarAsync<long>(sql, values);
    private async Task<string> AdminScalarStringAsync(string sql, params object?[] values) =>
        await AdminScalarAsync<string>(sql, values);
    private Task AdminExecuteAsync(string sql, params object?[] values) => AdminExecuteCoreAsync(sql, values);

    private async Task<T> AdminScalarAsync<T>(string sql, params object?[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private async Task AdminExecuteCoreAsync(string sql, params object?[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
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

    private static void AddParameters(NpgsqlCommand command, params object?[] values)
    {
        // Parámetros posicionales sin nombre: Npgsql los vincula al orden del
        // SQL ($1, $2, ...) igual que en RlsSecurityTests.
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value ?? DBNull.Value);
        }
    }

    private sealed record Contexto(Guid InstitucionId, Guid SeccionId, Guid CicloId, Guid GradoId);
    private sealed record Identidad(Guid UsuarioId, Guid PersonaId, Guid AuthUserId);
}