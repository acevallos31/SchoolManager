using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Cobertura DB del Bloque 021 (migración 021): modelo transaccional de pagos.
/// Verifica el contrato docs/decisiones/021-pagos-cobranza-fase1-invariantes.md:
/// pago a uno/varios cargos, parcial/total, múltiples pagos al mismo cargo,
/// rechazo de sobrepago, suma != monto_total, cross-tenant, responsable inválido,
/// referencia duplicada, cargo anulado, anulación con reversión (sin DELETE) y
/// recálculo de estados. Se prueban las RPC SECURITY DEFINER directamente porque
/// su bypass de RLS deja la aislación en manos de los cheques internos.
/// </summary>
public sealed class PagosMultitenancyTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Pago_a_un_cargo_total_lo_deja_pagado()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));

        var cargo = (await ListarCargosAsync(ctx))[0];

        var pagoId = await RegistrarAsync(ctx, 800m, (cargo.Id, 800m));

        Assert.NotEqual(Guid.Empty, pagoId);
        Assert.Equal("registrado", await EstadoPagoAsync(ctx, pagoId));
        Assert.Equal(800m, await ScalarAsync<decimal>(
            "select monto_total from public.pagos where id=$1", pagoId));
        Assert.Equal("pagado", await EstadoCargoAsync(ctx, cargo.Id));
        // Aplicación vigente y única.
        Assert.Equal(1L, await ScalarAsync<long>(
            "select count(*) from public.pagos_aplicaciones where pago_id=$1 and estado='vigente'", pagoId));
    }

    [Fact]
    public async Task Pago_a_varios_cargos_se_reparte_y_los_completa()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota 1", 300m, 10), (2, "Cuota 2", 400m, 20));

        var cargos = await ListarCargosAsync(ctx);

        var pagoId = await RegistrarAsync(ctx, 700m, (cargos[0].Id, 300m), (cargos[1].Id, 400m));

        Assert.Equal(2L, await ScalarAsync<long>(
            "select count(*) from public.pagos_aplicaciones where pago_id=$1 and estado='vigente'", pagoId));
        Assert.Equal("pagado", await EstadoCargoAsync(ctx, cargos[0].Id));
        Assert.Equal("pagado", await EstadoCargoAsync(ctx, cargos[1].Id));
        Assert.Equal(0m, await ResumenAsync(ctx, "total_pendiente"));
    }

    [Fact]
    public async Task Pago_parcial_deja_cargo_en_parcial_y_saldo_derivado()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        var pagoId = await RegistrarAsync(ctx, 300m, (cargo.Id, 300m));

        Assert.Equal("parcial", await EstadoCargoAsync(ctx, cargo.Id));
        // Saldo derivado = 800 - 300 = 500.
        Assert.Equal(500m, await SaldoCargoAsync(ctx, cargo.Id));
        Assert.Equal(300m, await AplicadoCargoAsync(ctx, cargo.Id));
        Assert.Equal(500m, await ResumenAsync(ctx, "total_pendiente"));
        Assert.Equal(300m, await ResumenAsync(ctx, "total_aplicado"));
    }

    [Fact]
    public async Task Multiples_pagos_al_mismo_cargo_acumulan_hasta_completarlo()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 1000m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        await RegistrarAsync(ctx, 400m, (cargo.Id, 400m));
        Assert.Equal("parcial", await EstadoCargoAsync(ctx, cargo.Id));

        var pago2 = await RegistrarAsync(ctx, 600m, (cargo.Id, 600m));
        Assert.Equal("pagado", await EstadoCargoAsync(ctx, cargo.Id));
        Assert.Equal(0m, await SaldoCargoAsync(ctx, cargo.Id));
        Assert.Equal(2L, await ScalarAsync<long>(
            "select count(*) from public.pagos where alumno_id=$1 and estado='registrado'", ctx.AlumnoId));
        Assert.NotEqual(Guid.Empty, pago2);
    }

    [Fact]
    public async Task Sobrepago_a_un_cargo_es_rechazado()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        var error = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctx, 900m, (cargo.Id, 900m)));
        Assert.Equal("23514", error.SqlState);

        // El cargo sigue pendiente y no quedó ningún pago.
        Assert.Equal("pendiente", await EstadoCargoAsync(ctx, cargo.Id));
        Assert.Equal(0L, await ScalarAsync<long>(
            "select count(*) from public.pagos where alumno_id=$1", ctx.AlumnoId));
    }

    [Fact]
    public async Task Suma_aplicaciones_distinta_de_monto_total_es_rechazada()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota 1", 300m, 10), (2, "Cuota 2", 400m, 20));
        var cargos = await ListarCargosAsync(ctx);

        // Las aplicaciones suman 700 pero se declara monto_total 600.
        var error = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctx, 600m, (cargos[0].Id, 300m), (cargos[1].Id, 400m)));
        Assert.Equal("23514", error.SqlState);
        Assert.Equal(0L, await ScalarAsync<long>(
            "select count(*) from public.pagos where alumno_id=$1", ctx.AlumnoId));
        Assert.Equal("pendiente", await EstadoCargoAsync(ctx, cargos[0].Id));
        Assert.Equal("pendiente", await EstadoCargoAsync(ctx, cargos[1].Id));
    }

    [Fact]
    public async Task Cross_tenant_es_rechazado()
    {
        await ResetAsync();
        var ctxA = await InsertAcademicContextAsync(90, "A");
        await PrepararPlanAsync(ctxA, (1, "Cuota", 800m, 10));
        var ctxB = await InsertAcademicContextAsync(90, "B");
        await PrepararPlanAsync(ctxB, (1, "Cuota", 500m, 10));

        var cargoA = (await ListarCargosAsync(ctxA))[0];

        // Admin B intenta registrar un pago del alumno A en su institucion B.
        var error = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctxB, 800m, (cargoA.Id, 800m)));
        // El cargo no pertenece al alumno de B en B => P0002 sin revelar existencia.
        Assert.Equal("P0002", error.SqlState);

        // Admin B no puede ver los pagos de A.
        var listar = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctxB.AdminAuthId, "select public.rpc_listar_pagos_alumno($1,$2)",
            ctxA.AlumnoId, ctxB.InstitutionId));
        Assert.Equal("P0002", listar.SqlState);
    }

    [Fact]
    public async Task Responsable_invalido_o_de_otra_institucion_es_rechazado()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        // Responsable de OTRA institucion (creado directo por superusuario:
        // no existe admin de la institucion ajena para crear via RPC).
        var otraInstitucion = await InsertInstitucionAsync("Otra");
        var responsableAjena = await CrearResponsableAsync(otraInstitucion, "Ajeno");

        var error = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctx, 800m, (cargo.Id, 800m), responsableId: responsableAjena));
        Assert.Equal("23503", error.SqlState);
        Assert.Equal(0L, await ScalarAsync<long>(
            "select count(*) from public.pagos where alumno_id=$1", ctx.AlumnoId));

        // Responsable de la misma institucion pero SIN vínculo al alumno.
        var responsableSinVinculo = await CrearResponsableAsync(ctx.InstitutionId, "SinVinculo");
        var error2 = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctx, 800m, (cargo.Id, 800m), responsableId: responsableSinVinculo));
        Assert.Equal("23503", error2.SqlState);
    }

    [Fact]
    public async Task Referencia_externa_duplicada_en_misma_institucion_es_rechazada()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota 1", 300m, 10), (2, "Cuota 2", 400m, 20));
        var cargos = await ListarCargosAsync(ctx);

        var refExterna = $"TRF-{Guid.NewGuid():N}";
        await RegistrarAsync(ctx, 300m, (cargos[0].Id, 300m), referenciaExterna: refExterna);

        var error = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctx, 400m, (cargos[1].Id, 400m), referenciaExterna: refExterna));
        Assert.Equal("23505", error.SqlState);
    }

    [Fact]
    public async Task Pago_a_cargo_anulado_es_rechazado()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota 1", 300m, 10), (2, "Cuota 2", 500m, 20));
        var cargos = await ListarCargosAsync(ctx);

        // Anular el cargo 2 directamente (no tiene aplicaciones aún).
        await AuthExecuteAsync(ctx.AdminAuthId, "select public.rpc_anular_cargo($1,'Error',$2)",
            cargos[1].Id, ctx.InstitutionId);
        Assert.Equal("anulado", await EstadoCargoAsync(ctx, cargos[1].Id));

        var error = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctx, 500m, (cargos[1].Id, 500m)));
        Assert.Equal("23503", error.SqlState);
    }

    [Fact]
    public async Task Anular_pago_revierte_aplicaciones_y_recalcula_estados()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        var pagoId = await RegistrarAsync(ctx, 300m, (cargo.Id, 300m));
        Assert.Equal("parcial", await EstadoCargoAsync(ctx, cargo.Id));

        await AuthExecuteAsync(ctx.AdminAuthId, "select public.rpc_anular_pago($1,'Pago erroneo',$2)",
            pagoId, ctx.InstitutionId);

        Assert.Equal("anulado", await EstadoPagoAsync(ctx, pagoId));
        Assert.Equal(1L, await ScalarAsync<long>(
            "select count(*) from public.pagos_aplicaciones where pago_id=$1 and estado='reversada'", pagoId));
        // Aplicación NO eliminada físicamente (trazabilidad).
        Assert.Equal(1L, await ScalarAsync<long>(
            "select count(*) from public.pagos_aplicaciones where pago_id=$1", pagoId));
        // El cargo vuelve a pendiente con saldo original.
        Assert.Equal("pendiente", await EstadoCargoAsync(ctx, cargo.Id));
        Assert.Equal(800m, await SaldoCargoAsync(ctx, cargo.Id));
        Assert.Equal(800m, await ResumenAsync(ctx, "total_pendiente"));
        Assert.Equal(0m, await ResumenAsync(ctx, "total_aplicado"));

        // No se puede anular dos veces.
        var doble = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctx.AdminAuthId, "select public.rpc_anular_pago($1,'Otra vez',$2)", pagoId, ctx.InstitutionId));
        Assert.Equal("22023", doble.SqlState);
    }

    [Fact]
    public async Task Listar_y_detalle_de_pagos_devuelven_historial_completo()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        var pagoId = await RegistrarAsync(ctx, 800m, (cargo.Id, 800m), referenciaExterna: "REF-LISTADO");

        Assert.Equal(1L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_listar_pagos_alumno($1,$2)", ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal(800m, await AuthScalarAsync<decimal>(ctx.AdminAuthId,
            "select monto_total from public.rpc_obtener_pago($1,$2)", pagoId, ctx.InstitutionId));
        Assert.Equal("REF-LISTADO", await AuthScalarAsync<string>(ctx.AdminAuthId,
            "select referencia_externa from public.rpc_obtener_pago($1,$2)", pagoId, ctx.InstitutionId));
        Assert.Equal(1L, await AuthScalarAsync<long>(ctx.AdminAuthId,
            "select count(*) from public.rpc_obtener_aplicaciones_pago($1,$2)", pagoId, ctx.InstitutionId));
    }

    [Fact]
    public async Task Sin_permiso_no_se_puede_registrar_ni_anular()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        // Usuario de rol sin permisos de pagos (operador no recibe academico.pagos.*).
        var operador = await InsertUsuarioAsync("operador", ctx.InstitutionId);

        var registrar = await Assert.ThrowsAsync<PostgresException>(() => RegistrarAsync(
            ctx, 800m, (cargo.Id, 800m), authOverride: operador));
        Assert.Equal("42501", registrar.SqlState);

        var listar = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            operador, "select public.rpc_listar_pagos_alumno($1,$2)", ctx.AlumnoId, ctx.InstitutionId));
        Assert.Equal("42501", listar.SqlState);
    }

    [Fact]
    public async Task No_se_puede_anular_cargo_con_pagos_vigentes()
    {
        await ResetAsync();
        var ctx = await InsertAcademicContextAsync(90);
        await PrepararPlanAsync(ctx, (1, "Cuota", 800m, 10));
        var cargo = (await ListarCargosAsync(ctx))[0];

        await RegistrarAsync(ctx, 300m, (cargo.Id, 300m));
        Assert.Equal("parcial", await EstadoCargoAsync(ctx, cargo.Id));

        // Anular el cargo directamente debe rechazarse (tiene aplicaciones).
        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            ctx.AdminAuthId, "select public.rpc_anular_cargo($1,'Error',$2)", cargo.Id, ctx.InstitutionId));
        Assert.Equal("23503", error.SqlState);
        Assert.Equal("parcial", await EstadoCargoAsync(ctx, cargo.Id));
    }

    // ==================== SETUP ACADEMICO + FINANCIERO ====================

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

    private async Task<PagoContext> InsertAcademicContextAsync(int cicloDias, string tag = "I")
    {
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");
        var institucion = await ScalarAsync<Guid>(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Institucion {tag}-{Guid.NewGuid():N}");
        var admin = await InsertUsuarioAsync("admin", institucion);

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

        var persona = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Alumno', $1) returning id",
            Guid.NewGuid().ToString("N"));
        var alumno = await ScalarAsync<Guid>(
            "insert into public.alumnos (persona_id, institucion_id) values ($1,$2) returning id",
            persona, institucion);

        var matricula = await ScalarAsync<Guid>(
            "insert into public.matriculas (alumno_id, institucion_id, ciclo_id, seccion_id, periodo_matricula_id) values ($1,$2,$3,$4,$5) returning id",
            alumno, institucion, ciclo, seccion, periodo);

        return new PagoContext(institucion, admin, alumno, matricula, cicloInicio);
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

    private Task<Guid> InsertInstitucionAsync(string tag) => ScalarAsync<Guid>(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {tag}-{Guid.NewGuid():N}");

    // Crea un responsable directo por superusuario (persona + responsable) sin
    // vínculo a alumno; evita depender de un admin de la institucion destino.
    private async Task<Guid> CrearResponsableAsync(Guid institucion, string apellido)
    {
        var persona = await ScalarAsync<Guid>(
            "insert into public.personas (nombres, apellidos) values ('Responsable', $1) returning id",
            Guid.NewGuid().ToString("N"));
        return await ScalarAsync<Guid>(
            "insert into public.responsables (persona_id, institucion_id) values ($1, $2) returning id",
            persona, institucion);
    }

    private async Task PrepararPlanAsync(PagoContext ctx,
        params (int orden, string descripcion, decimal monto, int vencimiento)[] cuotas)
    {
        var concepto = await CrearConceptoAsync(ctx, "Colegiatura", 1500m);
        var plan = await CrearPlanAsync(ctx, concepto, cuotas);
        await AsignarAsync(ctx, plan);
        await GenerarAsync(ctx);
    }

    private async Task<List<CargoLigero>> ListarCargosAsync(PagoContext ctx)
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

    private Task<Guid> CrearConceptoAsync(PagoContext ctx, string nombre, decimal monto) =>
        AuthScalarAsync<Guid>(ctx.AdminAuthId,
            "select public.rpc_crear_concepto_financiero($1, $2, null, $3)", nombre, monto, ctx.InstitutionId);

    private async Task<Guid> CrearPlanAsync(PagoContext ctx, Guid concepto,
        params (int orden, string descripcion, decimal monto, int vencimiento)[] cuotas)
    {
        var filas = string.Join(",", cuotas.Select(c =>
            $"{{\"orden\":{c.orden},\"concepto_id\":\"{concepto:N}\",\"descripcion\":\"{c.descripcion}\",\"monto\":{c.monto.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"vencimiento_dias\":{c.vencimiento}}}"));
        return await AuthScalarAsync<Guid>(ctx.AdminAuthId,
            "select public.rpc_crear_plan_pago($1, null, $2::jsonb, $3)",
            $"Plan {Guid.NewGuid():N}", $"[{filas}]", ctx.InstitutionId);
    }

    private async Task<Guid> AsignarAsync(PagoContext ctx, Guid plan) =>
        await AuthScalarAsync<Guid>(ctx.AdminAuthId,
            "select public.rpc_asignar_plan_pago_matricula($1,$2,$3)",
            ctx.MatriculaId, plan, ctx.InstitutionId);

    private Task<int> GenerarAsync(PagoContext ctx) =>
        AuthScalarAsync<int>(ctx.AdminAuthId,
            "select public.rpc_generar_cargos_matricula($1,$2)", ctx.MatriculaId, ctx.InstitutionId);

    // ==================== OPERACIONES DE PAGO ====================

    private Task<Guid> RegistrarAsync(PagoContext ctx, decimal montoTotal,
        params (Guid cargoId, decimal monto)[] aplicaciones) =>
        RegistrarAsync(ctx, montoTotal, aplicaciones, null, null);

    // Conveniencia: un solo cargo + opciones (responsable/referencia/auth).
    private Task<Guid> RegistrarAsync(PagoContext ctx, decimal montoTotal,
        (Guid cargoId, decimal monto) aplicacion, string? referenciaExterna = null,
        Guid? responsableId = null, Guid? authOverride = null) =>
        RegistrarAsync(ctx, montoTotal, new[] { aplicacion }, referenciaExterna, responsableId, authOverride);

    private async Task<Guid> RegistrarAsync(PagoContext ctx, decimal montoTotal,
        (Guid cargoId, decimal monto)[] aplicaciones, string? referenciaExterna = null,
        Guid? responsableId = null, Guid? authOverride = null)
    {
        var filas = string.Join(",", aplicaciones.Select(a =>
            $"{{\"cargo_id\":\"{a.cargoId:N}\",\"monto\":{a.monto.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}"));
        var auth = authOverride ?? ctx.AdminAuthId;
        return await AuthScalarAsync<Guid>(auth,
            "select public.rpc_registrar_pago($1, $2::jsonb, $3, $4, $5, null, $6)",
            ctx.AlumnoId, $"[{filas}]", montoTotal, ctx.InstitutionId,
            responsableId ?? (object)DBNull.Value, referenciaExterna ?? (object)DBNull.Value);
    }

    // Lecturas de persistencia (estado/saldo) van por el superusuario del
    // DataSource porque authenticated tiene RLS y privilegios revocados sobre
    // estas tablas (021). Las invocaciones RPC SÍ van como authenticated para
    // ejercer el control de permisos y RLS.
    private Task<string> EstadoPagoAsync(PagoContext ctx, Guid pagoId) =>
        ScalarAsync<string>("select estado from public.pagos where id=$1", pagoId);

    private Task<string> EstadoCargoAsync(PagoContext ctx, Guid cargoId) =>
        ScalarAsync<string>("select estado from public.cargos where id=$1", cargoId);

    private Task<decimal> SaldoCargoAsync(PagoContext ctx, Guid cargoId) =>
        ScalarAsync<decimal>(
            "select round(monto_original - coalesce((select sum(pa.monto_aplicado) from public.pagos_aplicaciones pa join public.pagos pg on pg.id=pa.pago_id where pa.cargo_id=$1 and pa.estado='vigente' and pg.estado='registrado'),0),2) from public.cargos where id=$1",
            cargoId);

    private Task<decimal> AplicadoCargoAsync(PagoContext ctx, Guid cargoId) =>
        ScalarAsync<decimal>(
            "select coalesce(sum(pa.monto_aplicado),0) from public.pagos_aplicaciones pa join public.pagos pg on pg.id=pa.pago_id where pa.cargo_id=$1 and pa.estado='vigente' and pg.estado='registrado'",
            cargoId);

    private Task<decimal> ResumenAsync(PagoContext ctx, string columna) =>
        AuthScalarAsync<decimal>(ctx.AdminAuthId,
            $"select {columna} from public.rpc_resumen_financiero_alumno($1,$2)",
            ctx.AlumnoId, ctx.InstitutionId);

    // ==================== HELPERS DE EJECUCION ====================

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

    private sealed record PagoContext(
        Guid InstitutionId, Guid AdminAuthId, Guid AlumnoId, Guid MatriculaId, DateOnly CicloInicio);
    private sealed record CargoLigero(Guid Id, decimal MontoOriginal);
}
