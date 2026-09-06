using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Cobertura DB del Bloque 022 (migración 022): portal responsable de SOLO LECTURA.
/// Verifica el aislamiento por identidad (NO por permiso academico.*): un usuario
/// autenticado cuyo persona_id es responsable financiero activo de un alumno
/// (misma institucion, alumno_responsable.acceso_financiero=true) solo ve sus
/// hijos y su estado financiero real; nunca un alumno ajeno ni de otra institucion;
/// sin sesion no ve nada. Se prueban las RPC SECURITY DEFINER directamente porque
/// su bypass de RLS deja la aislacion en manos de los cheques internos.
/// </summary>
public sealed class PortalResponsableLecturaTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Responsable_financiero_ve_solo_a_sus_alumnos_con_acceso()
    {
        await ResetAsync();
        var ctx = await InsertContextoConHijoFinancieroAsync(90, "A");
        // Hijo adicional vinculado SIN acceso_financiero -> no debe aparecer.
        var alumnoSinAcceso = await InsertHijoAdicionalAsync(
            ctx, "SinAcceso", accesoFinanciero: false);

        var ids = await MisAlumnosIdsAsync(ctx.PadreAuthId);

        Assert.Single(ids);
        Assert.Equal(ctx.AlumnoId, ids[0]);
        Assert.DoesNotContain(alumnoSinAcceso, ids);
    }

    [Fact]
    public async Task Responsable_no_ve_alumno_ajeno_ni_de_otra_institucion()
    {
        await ResetAsync();
        var ctxA = await InsertContextoConHijoFinancieroAsync(90, "A");
        // Segundo alumno en OTRA institucion (B) con responsable propio.
        var ctxB = await InsertContextoConHijoFinancieroAsync(90, "B");

        // El responsable de A solo ve su hijo de A.
        var idsA = await MisAlumnosIdsAsync(ctxA.PadreAuthId);
        Assert.Single(idsA);
        Assert.Equal(ctxA.AlumnoId, idsA[0]);

        // No ve el alumno de B (aislamiento cross-tenant) ni sus datos.
        var cross = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctxA.PadreAuthId, "select public.rpc_resumen_financiero_responsable($1,$2)",
            ctxB.AlumnoId, ctxA.InstitutionId));
        Assert.Equal("42501", cross.SqlState);
    }

    [Fact]
    public async Task Resumen_cargos_y_pagos_del_hijo_devuelven_datos_reales()
    {
        await ResetAsync();
        var ctx = await InsertContextoConHijoFinancieroAsync(90, "A");
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));

        var cargo = (await ListarCargosAsync(ctx))[0];
        var pagoId = await RegistrarAsync(ctx, 800m, (cargo.Id, 800m));

        // Resumen real: cuota pagada en su totalidad => pendiente 0, aplicado 800.
        Assert.Equal(0m, await PadreScalarAsync<decimal>(ctx.PadreAuthId,
            "select total_pendiente from public.rpc_resumen_financiero_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal(800m, await PadreScalarAsync<decimal>(ctx.PadreAuthId,
            "select total_aplicado from public.rpc_resumen_financiero_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));

        // Cargos del hijo (proyeccion saldo/aplicado).
        var saldo = await PadreScalarAsync<decimal>(ctx.PadreAuthId,
            "select saldo from public.rpc_cargos_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId);
        Assert.Equal(0m, saldo);
        var estadoCargo = await PadreScalarAsync<string>(ctx.PadreAuthId,
            "select estado from public.rpc_cargos_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId);
        Assert.Equal("pagado", estadoCargo);

        // Historial de pagos + aplicaciones del hijo.
        Assert.Equal(1L, await PadreScalarAsync<long>(ctx.PadreAuthId,
            "select count(*) from public.rpc_pagos_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal(1L, await PadreScalarAsync<long>(ctx.PadreAuthId,
            "select count(*) from public.rpc_pago_aplicaciones_responsable($1,$2)",
            pagoId, ctx.InstitutionId));
    }

    [Fact]
    public async Task Cargo_vencido_expone_saldo_y_flag_en_cargos()
    {
        await ResetAsync();
        var ctx = await InsertContextoConHijoFinancieroAsync(30, "A");
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        // Vencido real: mover la fecha de vencimiento del cargo al pasado.
        await ExecuteAsync(
            "update public.cargos set fecha_vencimiento = (current_date - 1) where matricula_id = $1",
            ctx.MatriculaId);

        var saldo = await PadreScalarAsync<decimal>(ctx.PadreAuthId,
            "select saldo from public.rpc_cargos_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId);
        var esVencido = await PadreScalarAsync<bool>(ctx.PadreAuthId,
            "select es_vencido from public.rpc_cargos_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId);
        Assert.Equal(800m, saldo);
        Assert.True(esVencido);
    }

    [Fact]
    public async Task Sin_acceso_financiero_no_ve_datos_del_alumno()
    {
        await ResetAsync();
        var ctx = await InsertContextoConHijoFinancieroAsync(90, "A");
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));

        // Vinculacion del padre al alumno SIN acceso_financiero.
        var padreSinAcceso = await CrearPadreAsync(ctx.InstitutionId);
        var alumno = await InsertHijoAdicionalAsync(
            ctx, "HijoSinFin", accesoFinanciero: false, padreAuth: padreSinAcceso.PadreAuthId);

        // No ve sus cargos ni el alumno aparece en "mis alumnos".
        var ids = await MisAlumnosIdsAsync(padreSinAcceso.PadreAuthId);
        Assert.DoesNotContain(alumno, ids);

        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            padreSinAcceso.PadreAuthId,
            "select public.rpc_cargos_responsable($1,$2)", alumno, ctx.InstitutionId));
        Assert.Equal("42501", error.SqlState);
    }

    [Fact]
    public async Task Sin_sesion_no_ve_datos()
    {
        await ResetAsync();
        var ctx = await InsertContextoConHijoFinancieroAsync(90, "A");
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));

        // Sin set_config del sub (sin sesion) auth.uid() es null => guard niega.
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "select public.rpc_resumen_financiero_responsable($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal("42501", error.SqlState);
    }

    // ==================== SETUP ====================

    private async Task ResetAsync()
    {
        await ExecuteAsync("delete from public.pagos_aplicaciones");
        await ExecuteAsync("delete from public.pagos");
        await ExecuteAsync("delete from public.alumno_responsable");
        await ExecuteAsync("delete from public.responsables");
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

    /// <summary>Institución con un admin (para setup 021) y UN hijo del que el
    /// padre/responsable es responsable financiero activo (mismo persona_id en
    /// usuario y responsable).</summary>
    private async Task<PortalContext> InsertContextoConHijoFinancieroAsync(int cicloDias, string tag)
    {
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");
        var institucion = await ScalarAsync<Guid>(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Institucion {tag}-{Guid.NewGuid():N}");
        var admin = await InsertUsuarioConRolAsync("admin", institucion);

        var cicloInicio = DateOnly.FromDateTime(await ScalarAsync<DateTime>("select current_date"));
        var ciclo = await ScalarAsync<Guid>(
            "insert into public.ciclos_escolares (institucion_id, nombre, fecha_inicio, fecha_fin) values ($1,$2,$3,$4) returning id",
            institucion, $"Ciclo {Guid.NewGuid():N}", cicloInicio, cicloInicio.AddDays(cicloDias));
        var grado = await ScalarAsync<Guid>(
            "insert into public.grados (nombre, orden, institucion_id) values ($1, 0, $2) returning id",
            $"Grado {Guid.NewGuid():N}", institucion);
        var jornada = await ScalarAsync<Guid>(
            "insert into public.jornadas (nombre, institucion_id) values ($1, $2) returning id",
            $"Jornada {Guid.NewGuid():N}", institucion);
        var seccion = await ScalarAsync<Guid>(
            "insert into public.secciones (nombre, institucion_id, ciclo_id, grado_id, jornada_id) values ($1,$2,$3,$4,$5) returning id",
            $"Seccion {Guid.NewGuid():N}", institucion, ciclo, grado, jornada);
        var periodo = await ScalarAsync<Guid>(
            "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1,$2,$3,$4) returning id",
            ciclo, $"Periodo {Guid.NewGuid():N}", cicloInicio, cicloInicio.AddDays(cicloDias));

        var personaHijo = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Hijo', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var alumno = await ScalarAsync<Guid>(
            "insert into public.alumnos (persona_id, institucion_id) values ($1,$2) returning id",
            personaHijo, institucion);
        var matricula = await ScalarAsync<Guid>(
            "insert into public.matriculas (alumno_id, institucion_id, ciclo_id, seccion_id, periodo_matricula_id) values ($1,$2,$3,$4,$5) returning id",
            alumno, institucion, ciclo, seccion, periodo);

        // Padre/responsable: unica persona compartida entre usuario y responsable.
        var personaPadre = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Padre', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authPadre = Guid.NewGuid();
        var usuarioPadre = await ScalarAsync<Guid>(
            "insert into public.usuarios (persona_id, auth_user_id) values ($1, $2) returning id",
            personaPadre, authPadre);
        var responsable = await ScalarAsync<Guid>(
            "insert into public.responsables (persona_id, institucion_id) values ($1, $2) returning id",
            personaPadre, institucion);
        await ExecuteAsync(
            "insert into public.alumno_responsable (alumno_id, responsable_id, parentesco, es_principal, acceso_financiero, estado) values ($1,$2,'Padre',true,true,'activo')",
            alumno, responsable);

        return new PortalContext(
            institucion, admin, authPadre, usuarioPadre, responsable,
            alumno, matricula, cicloInicio);
    }

    /// <summary>Crea otro hijo en la misma institucion del padre (con su propio
    /// persona y matricula de contexto) y, opcionalmente, una vinculacion
    /// alumno_responsable controlada por el flag de acceso financiero.</summary>
    private async Task<Guid> InsertHijoAdicionalAsync(
        PortalContext ctx, string apellido,
        bool accesoFinanciero = true, Guid? padreAuth = null)
    {
        var personaHijo = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Hijo2', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var alumno = await ScalarAsync<Guid>(
            "insert into public.alumnos (persona_id, institucion_id) values ($1,$2) returning id",
            personaHijo, ctx.InstitutionId);
        await ExecuteAsync(
            "insert into public.matriculas (alumno_id, institucion_id, ciclo_id, seccion_id, periodo_matricula_id) select $1, institucion_id, ciclo_id, seccion_id, periodo_matricula_id from public.matriculas where alumno_id = $2 limit 1",
            alumno, ctx.AlumnoId);

        var auth = padreAuth ?? ctx.PadreAuthId;
        var responsable = await ScalarAsync<Guid>(
            "select r.id from public.responsables r join public.usuarios u on u.persona_id = r.persona_id where u.auth_user_id = $1 and r.institucion_id = $2",
            auth, ctx.InstitutionId);
        await ExecuteAsync(
            "insert into public.alumno_responsable (alumno_id, responsable_id, parentesco, es_principal, acceso_financiero, estado) values ($1,$2,'Padre',false,$3,'activo')",
            alumno, responsable, accesoFinanciero);
        return alumno;
    }

    private async Task<(Guid PadreAuthId, Guid UsuarioId)> CrearPadreAsync(Guid institucion)
    {
        var personaPadre = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Padre2', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var authPadre = Guid.NewGuid();
        var usuario = await ScalarAsync<Guid>(
            "insert into public.usuarios (persona_id, auth_user_id) values ($1, $2) returning id",
            personaPadre, authPadre);
        await ScalarAsync<Guid>(
            "insert into public.responsables (persona_id, institucion_id) values ($1, $2) returning id",
            personaPadre, institucion);
        return (authPadre, usuario);
    }

    private async Task<Guid> InsertUsuarioConRolAsync(string rol, Guid institucionId)
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

    // ==================== PLAN 021 (setup como admin) ====================

    private async Task PrepararPlanAsync(PortalContext ctx,
        params (int orden, string descripcion, decimal monto, int vencimiento)[] cuotas)
    {
        var concepto = await AdminScalarAsync<Guid>(ctx,
            "select public.rpc_crear_concepto_financiero($1, $2, null, $3)",
            "Colegiatura", 1500m, ctx.InstitutionId);
        var filas = string.Join(",", cuotas.Select(c2 =>
            $"{{\"orden\":{c2.orden},\"concepto_id\":\"{concepto:N}\",\"descripcion\":\"{c2.descripcion}\",\"monto\":{c2.monto.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"vencimiento_dias\":{c2.vencimiento}}}"));
        var plan = await AdminScalarAsync<Guid>(ctx,
            "select public.rpc_crear_plan_pago($1, null, $2::jsonb, $3)",
            $"Plan {Guid.NewGuid():N}", $"[{filas}]", ctx.InstitutionId);
        await AdminExecuteAsync(ctx, "select public.rpc_asignar_plan_pago_matricula($1,$2,$3)",
            ctx.MatriculaId, plan, ctx.InstitutionId);
        await AdminExecuteAsync(ctx, "select public.rpc_generar_cargos_matricula($1,$2)",
            ctx.MatriculaId, ctx.InstitutionId);
    }

    private async Task<List<CargoLigero>> ListarCargosAsync(PortalContext ctx)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetAuthenticatedAsync(connection, transaction, ctx.AdminAuthId);
        await using var command = new NpgsqlCommand(
            "select id, monto_original from public.rpc_listar_cargos_matricula($1,$2) order by orden",
            connection, transaction);
        command.Parameters.AddWithValue(ctx.MatriculaId);
        command.Parameters.AddWithValue(ctx.InstitutionId);
        await using var reader = await command.ExecuteReaderAsync();
        var lista = new List<CargoLigero>();
        while (await reader.ReadAsync())
        {
            lista.Add(new CargoLigero(reader.GetGuid(0), reader.GetDecimal(1)));
        }
        await reader.DisposeAsync();
        await transaction.CommitAsync();
        return lista;
    }

    private async Task<Guid> RegistrarAsync(PortalContext ctx, decimal montoTotal,
        (Guid cargoId, decimal monto) aplicacion)
    {
        var filas = $"{{\"cargo_id\":\"{aplicacion.cargoId:N}\",\"monto\":{aplicacion.monto.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}";
        return await AdminScalarAsync<Guid>(ctx,
            "select public.rpc_registrar_pago($1, $2::jsonb, $3, $4, null, null, null)",
            ctx.AlumnoId, $"[{filas}]", montoTotal, ctx.InstitutionId);
    }

    // ==================== LECTURAS COMO PADRE ====================

    private async Task<List<Guid>> MisAlumnosIdsAsync(Guid padreAuthId)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await SetAuthenticatedAsync(connection, transaction, padreAuthId);
        await using var command = new NpgsqlCommand(
            "select id from public.rpc_mis_alumnos_responsable() order by id", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var ids = new List<Guid>();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetGuid(0));
        }
        await reader.DisposeAsync();
        await transaction.CommitAsync();
        return ids;
    }

    private Task<T> PadreScalarAsync<T>(Guid padreAuthId, string sql, params object?[] values) =>
        AuthScalarAsync<T>(padreAuthId, sql, values);

    // ==================== HELPERS DE EJECUCION ====================

    private async Task AdminExecuteAsync(PortalContext ctx, string sql, params object?[] values) =>
        await AuthExecuteAsync(ctx.AdminAuthId, sql, values);

    private Task<T> AdminScalarAsync<T>(PortalContext ctx, string sql, params object?[] values) =>
        AuthScalarAsync<T>(ctx.AdminAuthId, sql, values);

    private async Task AuthExecuteAsync(Guid authUserId, string sql, params object?[] values)
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

    private async Task ExecuteAsync(string sql, params object?[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params object?[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)result;
    }

    private static void AddParameters(NpgsqlCommand command, params object?[] values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value ?? DBNull.Value);
        }
    }

    private sealed record PortalContext(
        Guid InstitutionId, Guid AdminAuthId, Guid PadreAuthId, Guid PadreUsuarioId,
        Guid PadreResponsableId, Guid AlumnoId, Guid MatriculaId, DateOnly CicloInicio);

    private sealed record CargoLigero(Guid Id, decimal MontoOriginal);
}
