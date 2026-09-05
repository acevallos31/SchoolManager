using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Pruebas de la migracion 019: generacion y gestion basica de obligaciones
/// (cargos/mensualidades) a partir de un plan de pago asignado a una matricula.
///
/// Verifica: atomicidad (todas las cuotas o ninguna), proteccion contra
/// generacion duplicada, aislamiento institucional (cross-tenant), rollback ante
/// montos/vencimientos invalidos, soft-state (anulacion sin DELETE) y resumen de
/// saldo. Se prueban las RPC SECURITY DEFINER directamente porque su bypass de RLS
/// deja la aislacion en manos de los cheques internos (permiso + pertenencia).
/// </summary>
public sealed class CargosMultitenancyTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Authenticated_no_accede_directo_a_cargos_pero_opera_por_RPC()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);

        var ajeno = Guid.NewGuid();
        foreach (var sql in new[]
        {
            "select * from public.cargos where matricula_id=$1",
            "update public.cargos set estado='anulado' where id=$1",
            "delete from public.cargos where matricula_id=$1"
        })
        {
            var valor = sql.StartsWith("update") || sql.StartsWith("delete") ? ajeno : ctx.MatriculaId;
            var error = await Assert.ThrowsAsync<PostgresException>(
                () => AuthExecuteAsync(ctx.AdminAuthId, sql, valor));
            Assert.Equal("42501", error.SqlState);
        }

        // Via RPC SI se puede operar (crear plan, asignar, generar, listar).
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto,
            (1, "Cuota 1", 500m, 10), (2, "Cuota 2", 500m, 20));
        await AsignarAsync(ctx, plan);
        var generados = await GenerarAsync(ctx);
        Assert.Equal(2, generados);
        Assert.Equal(2L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_listar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
    }

    [Fact]
    public async Task Generar_desde_plan_materializa_todas_las_cuotas_y_resume_saldo()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto,
            (1, "Matricula", 400m, 0), (2, "Mensualidad 1", 500m, 30), (3, "Mensualidad 2", 500m, 60));

        var matriculaId = await AsignarAsync(ctx, plan);
        var generados = await GenerarAsync(ctx);
        Assert.Equal(3, generados);

        // Listar por matricula: 3 cargos pendientes con montos y vencimientos calculados.
        var total = await AuthScalarAsync<decimal>(ctx.AdminAuthId,
            "select coalesce(sum(monto_original),0) from public.rpc_listar_cargos_matricula($1,$2)",
            matriculaId, ctx.InstitutionId);
        Assert.Equal(1400m, total);
        Assert.Equal(3L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) filter (where estado='pendiente') from public.rpc_listar_cargos_matricula($1,$2)",
            matriculaId, ctx.InstitutionId));
        Assert.Equal("Colegiatura", await AuthScalarAsync<string>(ctx.AdminAuthId,
            "select min(concepto_nombre) from public.rpc_listar_cargos_matricula($1,$2)",
            matriculaId, ctx.InstitutionId));

        // Vencimiento de la cuota de orden 3 = fecha_inicio del ciclo + 60 dias.
        // Npgsql devuelve el date como DateTime en esta suite; se convierte a DateOnly
        // para comparar con la fecha del ciclo (no se altera el tipo SQL ni el contrato).
        var venc = DateOnly.FromDateTime(await AuthScalarAsync<DateTime>(ctx.AdminAuthId,
            "select min(fecha_vencimiento) filter (where orden=3) from public.rpc_listar_cargos_matricula($1,$2)",
            matriculaId, ctx.InstitutionId));
        Assert.Equal(ctx.CicloInicio.AddDays(60), venc);

        // Listar por alumno agrega las matriculas del alumno.
        Assert.Equal(3L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_listar_cargos_alumno($1,$2)", ctx.AlumnoId, ctx.InstitutionId));

        // Resumen de saldo: 1400 pendientes, 0 vencido (ningun vencimiento en el pasado).
        Assert.Equal(1400m, await AuthScalarAsync<decimal>(ctx.AdminAuthId,
            "select total_pendiente from public.rpc_resumen_financiero_alumno($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal(1400m, await AuthScalarAsync<decimal>(ctx.AdminAuthId,
            "select total_monto_original from public.rpc_resumen_financiero_alumno($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal(0m, await AuthScalarAsync<decimal>(ctx.AdminAuthId,
            "select total_vencido from public.rpc_resumen_financiero_alumno($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal(3L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select total_obligaciones from public.rpc_resumen_financiero_alumno($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
    }

    [Fact]
    public async Task Regenerar_la_misma_matricula_rechaza_duplicado()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto, (1, "Cuota 1", 500m, 10), (2, "Cuota 2", 500m, 20));
        await AsignarAsync(ctx, plan);
        Assert.Equal(2, await GenerarAsync(ctx));

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuthExecuteAsync(ctx.AdminAuthId,
                "select public.rpc_generar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
        Assert.Equal("23505", error.SqlState);
        Assert.Equal(2L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_listar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
    }

    [Fact]
    public async Task Generar_sin_plan_asignado_rechaza()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuthExecuteAsync(ctx.AdminAuthId,
                "select public.rpc_generar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
        Assert.Equal("P0002", error.SqlState);
    }

    [Fact]
    public async Task AdminB_no_accede_a_cargos_de_AdminA()
    {
        await ResetAsync();
        var ctxA = await InsertAcademicContextAsync(90, "A");
        var ctxB = await InsertAcademicContextAsync(90, "B");
        var concepto = await CrearConceptoAsync(ctxA, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctxA, concepto, (1, "Cuota", 800m, 10));
        await AsignarAsync(ctxA, plan);
        Assert.Equal(1, await GenerarAsync(ctxA));

        // AdminB no puede listar/generar/resumir en la institucion A (permiso/inst).
        var listar = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctxB.AdminAuthId, "select public.rpc_listar_cargos_matricula($1,$2)", ctxA.MatriculaId, ctxA.InstitutionId));
        Assert.Equal("42501", listar.SqlState);

        var resumen = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctxB.AdminAuthId, "select public.rpc_resumen_financiero_alumno($1,$2)", ctxA.AlumnoId, ctxA.InstitutionId));
        Assert.Equal("42501", resumen.SqlState);

        var generar = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctxB.AdminAuthId, "select public.rpc_generar_cargos_matricula($1,$2)", ctxA.MatriculaId, ctxB.InstitutionId));
        Assert.Equal("P0002", generar.SqlState);

        // En su propia institucion B, AdminB no puede siquiera consultar el alumno de A:
        // la RPC devuelve P0002 sin confirmar que ese alumno existe en otra institucion.
        // (Comportamiento seguro: no se revela existencia cross-tenant.)
        var listarAlumnoA = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctxB.AdminAuthId, "select public.rpc_listar_cargos_alumno($1,$2)", ctxA.AlumnoId, ctxB.InstitutionId));
        Assert.Equal("P0002", listarAlumnoA.SqlState);

        // Los cargos de A quedaron intactos.
        Assert.Equal(800m, await AuthScalarAsync<decimal>(ctxA.AdminAuthId,
            "select coalesce(sum(monto_original),0) from public.rpc_listar_cargos_matricula($1,$2)",
            ctxA.MatriculaId, ctxA.InstitutionId));
    }

    [Fact]
    public async Task Cuota_con_monto_invalido_revierte_toda_la_generacion()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto, (1, "Cuota 1", 500m, 10), (2, "Cuota 2", 0m, 20));
        await AsignarAsync(ctx, plan);

        // Cuota de monto 0 viola la invariante monto>0 => generacion falla atomica.
        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctx.AdminAuthId, "select public.rpc_generar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
        Assert.Equal("22023", error.SqlState);

        // Ninguna cuota se materializo (rollback total).
        Assert.Equal(0L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_listar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
    }

    [Fact]
    public async Task Cuota_con_vencimiento_fuera_del_ciclo_revierte_toda_la_generacion()
    {
        await ResetAsync();
        // Ciclo de 60 dias; la cuota 2 vence a los 90 dias (fuera del ciclo).
        var ctx = await InsertAcademicContextAsync(60);
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto, (1, "Cuota 1", 500m, 10), (2, "Cuota 2", 500m, 90));
        await AsignarAsync(ctx, plan);

        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctx.AdminAuthId, "select public.rpc_generar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
        Assert.Equal("22023", error.SqlState);
        Assert.Equal(0L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_listar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
    }

    [Fact]
    public async Task Anular_cargo_es_soft_state_y_actualiza_el_resumen()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto, (1, "Matricula", 400m, 0), (2, "Mensualidad", 500m, 30));
        await AsignarAsync(ctx, plan);
        Assert.Equal(2, await GenerarAsync(ctx));

        // No se usa min(id): el agregado min(uuid) no existe. Se obtiene un cargo
        // concreto con una lectura explicitamente calificada sobre la funcion.
        var cargoId = await AuthScalarAsync<Guid>(ctx.AdminAuthId,
            "select id from public.rpc_listar_cargos_matricula($1,$2) limit 1", ctx.MatriculaId, ctx.InstitutionId);

        await AuthExecuteAsync(ctx.AdminAuthId,
            "select public.rpc_anular_cargo($1,$2,$3)", cargoId, "Emitido por error", ctx.InstitutionId);

        // El cargo quedo anulado (no eliminado) y el resumen bajo.
        Assert.Equal("anulado", await AuthScalarAsync<string>(ctx.AdminAuthId,
            "select min(estado) filter (where id=$1) from public.rpc_listar_cargos_matricula($2,$3)",
            cargoId, ctx.MatriculaId, ctx.InstitutionId));
        Assert.Equal(500m, await AuthScalarAsync<decimal>(ctx.AdminAuthId,
            "select total_pendiente from public.rpc_resumen_financiero_alumno($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal(400m, await AuthScalarAsync<decimal>(ctx.AdminAuthId,
            "select total_anulado from public.rpc_resumen_financiero_alumno($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));

        // Re-anular un cargo ya anulado se rechaza.
        var duplicado = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctx.AdminAuthId, "select public.rpc_anular_cargo($1,'De nuevo',$2)", cargoId, ctx.InstitutionId));
        Assert.Equal("22023", duplicado.SqlState);
    }

    [Fact]
    public async Task Matricula_finalizada_o_sin_plan_no_genera()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto, (1, "Cuota", 800m, 10));

        // Sin plan asignado ya se cubre en Generar_sin_plan_asignado_rechaza.
        // Asigna el plan (matricula pendiente) y luego finaliza la matricula:
        // ya no debe poder generar cargos.
        await AsignarAsync(ctx, plan);
        await ExecuteAsync("update public.matriculas set estado='finalizada' where id=$1", ctx.MatriculaId);
        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctx.AdminAuthId, "select public.rpc_generar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
        Assert.Equal("22023", error.SqlState);
        Assert.Equal(0L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_listar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId));
    }

    // ==================== SETUP ACADEMICO + FINANCIERO ====================

    private async Task ResetAsync()
    {
        await ExecuteAsync("delete from public.cargos");
        await ExecuteAsync("delete from public.matricula_estado_historial");
        await ExecuteAsync("delete from public.matriculas");
        await ExecuteAsync("delete from public.plan_cuotas");
        await ExecuteAsync("delete from public.planes_pago");
        await ExecuteAsync("delete from public.conceptos_financieros");
        await ExecuteAsync("delete from public.periodos_matricula");
        await ExecuteAsync("delete from public.secciones");
        await ExecuteAsync("delete from public.grados");
        await ExecuteAsync("delete from public.jornadas");
        await ExecuteAsync("delete from public.ciclos_escolares");
        await ExecuteAsync("delete from public.alumnos");
        await ExecuteAsync("delete from public.usuarios_roles");
        await ExecuteAsync("delete from public.usuarios");
        await ExecuteAsync("delete from public.personas");
        await ExecuteAsync("delete from public.instituciones");
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = false where id = 1");
    }

    private async Task<CargoContext> InsertAcademicContextAsync(int cicloDias, string tag = "I")
    {
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");
        var institucion = await ScalarAsync<Guid>(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Institucion {tag}-{Guid.NewGuid():N}");
        var admin = await InsertUsuarioAsync("admin", institucion);

        var cicloInicio = DateOnly.FromDateTime(DateTime.Today);
        var ciclo = await ScalarAsync<Guid>(
            "insert into public.ciclos_escolares (institucion_id, nombre, fecha_inicio, fecha_fin) values ($1,$2,$3,$4) returning id",
            institucion, $"Ciclo {Guid.NewGuid():N}", cicloInicio, cicloInicio.AddDays(cicloDias));
        var grado = await ScalarAsync<Guid>(
            "insert into public.grados (nombre, orden) values ($1, 0) returning id", $"Grado {Guid.NewGuid():N}");
        var jornada = await ScalarAsync<Guid>(
            "insert into public.jornadas (nombre) values ($1) returning id", $"Jornada {Guid.NewGuid():N}");
        var seccion = await ScalarAsync<Guid>(
            "insert into public.secciones (nombre, institucion_id, ciclo_id, grado_id) values ($1,$2,$3,$4) returning id",
            $"Seccion {Guid.NewGuid():N}", institucion, ciclo, grado);
        var periodo = await ScalarAsync<Guid>(
            "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1,$2,$3,$4) returning id",
            ciclo, $"Periodo {Guid.NewGuid():N}", cicloInicio, cicloInicio.AddDays(cicloDias));

        var persona = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Alumno', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var alumno = await ScalarAsync<Guid>(
            "insert into public.alumnos (persona_id, institucion_id) values ($1,$2) returning id",
            persona, institucion);

        var matricula = await ScalarAsync<Guid>(
            "insert into public.matriculas (alumno_id, institucion_id, ciclo_id, seccion_id, periodo_matricula_id) values ($1,$2,$3,$4,$5) returning id",
            alumno, institucion, ciclo, seccion, periodo);

        return new CargoContext(institucion, admin, alumno, matricula, cicloInicio);
    }

    private async Task<Guid> InsertUsuarioAsync(string rol, Guid institucionId)
    {
        var persona = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Usuario', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authUser = Guid.NewGuid();
        var usuario = await ScalarAsync<Guid>(
            "insert into public.usuarios (persona_id, auth_user_id) values ($1, $2) returning id",
            persona, authUser);
        var rolId = await ScalarAsync<Guid>("select id from public.roles where codigo = $1", rol);
        await ExecuteAsync(
            "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)",
            usuario, rolId, institucionId);
        return authUser;
    }

    private Task<Guid> CrearConceptoAsync(CargoContext ctx, string nombre, decimal monto) =>
        AuthScalarAsync<Guid>(ctx.AdminAuthId,
            "select public.rpc_crear_concepto_financiero($1, $2, null, $3)", nombre, monto, ctx.InstitutionId);

    private async Task<Guid> CrearPlanAsync(CargoContext ctx, Guid concepto,
        params (int orden, string descripcion, decimal monto, int vencimiento)[] cuotas)
    {
        var filas = string.Join(",", cuotas.Select(c =>
            $"{{\"orden\":{c.orden},\"concepto_id\":\"{concepto:N}\",\"descripcion\":\"{c.descripcion}\",\"monto\":{c.monto.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"vencimiento_dias\":{c.vencimiento}}}"));
        return await AuthScalarAsync<Guid>(ctx.AdminAuthId,
            "select public.rpc_crear_plan_pago($1, null, $2::jsonb, $3)",
            $"Plan {Guid.NewGuid():N}", $"[{filas}]", ctx.InstitutionId);
    }

    private async Task<Guid> AsignarAsync(CargoContext ctx, Guid plan) =>
        await AuthScalarAsync<Guid>(ctx.AdminAuthId,
            "select public.rpc_asignar_plan_pago_matricula($1,$2,$3)",
            ctx.MatriculaId, plan, ctx.InstitutionId);

    private Task<int> GenerarAsync(CargoContext ctx) =>
        AuthScalarAsync<int>(ctx.AdminAuthId,
            "select public.rpc_generar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId);

    // ==================== HELPERS DE EJECUCION ====================

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

    private async Task<T> AuthScalarAsync<T>(Guid authUserId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetAuthenticatedAsync(connection, transaction, authUserId);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        AddParameters(command, values);
        var result = (T)(await command.ExecuteScalarAsync())!;
        await transaction.CommitAsync();
        return result;
    }

    private static async Task SetAuthenticatedAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid authUserId)
    {
        await new NpgsqlCommand("set local role authenticated", connection, transaction)
            .ExecuteNonQueryAsync();
        await using var command = new NpgsqlCommand(
            "select set_config('request.jwt.claim.sub', $1, true)", connection, transaction);
        command.Parameters.AddWithValue(authUserId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)result;
    }

    private static void AddParameters(NpgsqlCommand command, params object[] values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value ?? DBNull.Value);
        }
    }

    private sealed record CargoContext(
        Guid InstitutionId, Guid AdminAuthId, Guid AlumnoId, Guid MatriculaId, DateOnly CicloInicio);
}
