using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Pruebas de aislamiento institucional (multitenancy) y de atomicidad de las RPC
/// de configuracion financiera (migracion 018).
///
/// Multitenancy: se prueba directamente sobre las RPC SECURITY DEFINER porque el
/// bypass de RLS que estas funciones ejercen deja la aislacion por entero en manos
/// de los cheques internos (permiso por institucion + pertenencia del recurso), que
/// es exactamente lo que la revision humana pidio verificar: que no hay fuga
/// cross-tenant via RPC.
///
/// Atomicidad: rpc_actualizar_plan_pago reemplaza nombre + cuotas dentro de un mismo
/// statement; si una cuota falla, todo debe revertirse (rollback completo).
/// </summary>
public sealed class ConfiguracionFinancieraMultitenancyTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task AdminB_no_accede_a_recursos_de_AdminA_via_RPC()
    {
        await ResetAsync();
        await SetMultiInstitucionAsync();
        var instA = await InsertInstitucionAsync();
        var instB = await InsertInstitucionAsync();
        var adminA = await InsertUsuarioAsync("admin", instA);
        var adminB = await InsertUsuarioAsync("admin", instB);

        // AdminA crea un concepto y un plan (con cuotas) en su institucion.
        var conceptoA = await AuthScalarAsync<Guid>(adminA, """
            select public.rpc_crear_concepto_financiero('Colegiatura', 1500.00, 'Mensualidad', $1)
            """, instA);
        var cuotas = $$"""
            [{"orden":1,"concepto_id":"{{conceptoA:N}}","descripcion":"Cuota 1","monto":100,"vencimiento_dias":30},
             {"orden":2,"concepto_id":"{{conceptoA:N}}","descripcion":"Cuota 2","monto":200,"vencimiento_dias":60}]
            """;
        var planA = await AuthScalarAsync<Guid>(adminA,
            "select public.rpc_crear_plan_pago('Plan Anual', 'Pago anual', $1::jsonb, $2)",
            cuotas, instA);

        // 1) AdminB no puede LISTAR el contexto de AdminA (apunta a instA): denegado, no data.
        var listadoA = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminB, "select public.rpc_listar_conceptos_financieros($1)", instA));
        Assert.Equal("42501", listadoA.SqlState);

        var listadoPlanesA = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminB, "select public.rpc_listar_planes_pago($1)", instA));
        Assert.Equal("42501", listadoPlanesA.SqlState);

        // 2) En su propio contexto (instB), AdminB no ve los recursos de A.
        Assert.Equal(0L, await AuthScalarAsync<long>(adminB,
            "select count(*) from public.rpc_listar_conceptos_financieros($1)", instB));
        Assert.Equal(0L, await AuthScalarAsync<long>(adminB,
            "select count(*) from public.rpc_listar_planes_pago($1)", instB));

        // 3) AdminB no puede leer/modificar/desactivar los recursos de A por id.
        var obtener = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminB, "select public.rpc_obtener_plan_pago($1, $2)", planA, instB));
        Assert.Equal("P0002", obtener.SqlState);

        var editar = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminB, "select public.rpc_actualizar_concepto_financiero($1, 'Intento', 999, null, $2)",
            conceptoA, instB));
        Assert.Equal("P0002", editar.SqlState);

        var desactivar = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminB, "select public.rpc_desactivar_concepto_financiero($1, 'Intruso', $2)", conceptoA, instB));
        Assert.Equal("P0002", desactivar.SqlState);

        var editarPlan = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminB, "select public.rpc_actualizar_plan_pago($1, 'Robado', null, $2::jsonb, $3)",
            planA, cuotas, instB));
        Assert.Equal("P0002", editarPlan.SqlState);

        // 4) Los recursos de A quedaron intactos (nada fue mutado).
        Assert.Equal(1L, await AuthScalarAsync<long>(adminA,
            "select count(*) filter (where activo) from public.rpc_listar_conceptos_financieros($1)", instA));
        Assert.Equal("Plan Anual", await AuthScalarAsync<string>(adminA,
            "select min(nombre) from public.rpc_obtener_plan_pago($1, $2)", planA, instA));
    }

    [Fact]
    public async Task Actualizar_plan_con_cuota_invalida_hace_rollback_total()
    {
        await ResetAsync();
        await SetMultiInstitucionAsync();
        var inst = await InsertInstitucionAsync();
        var admin = await InsertUsuarioAsync("admin", inst);

        var concepto = await AuthScalarAsync<Guid>(admin, """
            select public.rpc_crear_concepto_financiero('Matricula', 3000.00, 'Inscripcion', $1)
            """, inst);
        var cuotasOriginales = $$"""
            [{"orden":1,"concepto_id":"{{concepto:N}}","descripcion":"Cuota 1","monto":100,"vencimiento_dias":30},
             {"orden":2,"concepto_id":"{{concepto:N}}","descripcion":"Cuota 2","monto":200,"vencimiento_dias":60}]
            """;
        var plan = await AuthScalarAsync<Guid>(admin,
            "select public.rpc_crear_plan_pago('Plan Original', 'Original', $1::jsonb, $2)",
            cuotasOriginales, inst);

        // Intento de reemplazo con NUEVO nombre + cuotas con ORDEN DUPLICADO.
        // La 1a cuota (orden 1) se inserta; la 2a (orden 1 de nuevo) viola la
        // unicidad ux_plan_cuotas_orden (23505) DESPUES del update de nombre y
        // del delete de cuotas => la funcion debe revertir todo.
        var cuotasInvalidas = $$"""
            [{"orden":1,"concepto_id":"{{concepto:N}}","descripcion":"Duplicada A","monto":999,"vencimiento_dias":10},
             {"orden":1,"concepto_id":"{{concepto:N}}","descripcion":"Duplicada B","monto":888,"vencimiento_dias":20}]
            """;
        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin, "select public.rpc_actualizar_plan_pago($1, 'Plan Editado', 'Editado', $2::jsonb, $3)",
            plan, cuotasInvalidas, inst));
        Assert.Equal("23505", error.SqlState);

        // El plan debe seguir exactamente como antes: nombre y cuotas intactos.
        Assert.Equal("Plan Original", await AuthScalarAsync<string>(admin,
            "select min(nombre) from public.rpc_obtener_plan_pago($1, $2)", plan, inst));
        Assert.Equal(2L, await AuthScalarAsync<long>(admin,
            "select count(*) from public.rpc_obtener_plan_pago($1, $2)", plan, inst));
        var montos = await AuthScalarAsync<decimal[]>(admin,
            "select array_agg(cuota_monto order by cuota_orden) from public.rpc_obtener_plan_pago($1, $2)",
            plan, inst);
        Assert.Equal(new[] { 100m, 200m }, montos);
    }

    [Fact]
    public async Task Rechaza_plan_con_cuota_de_concepto_de_otra_institucion()
    {
        await ResetAsync();
        await SetMultiInstitucionAsync();
        var instA = await InsertInstitucionAsync();
        var instB = await InsertInstitucionAsync();
        var adminA = await InsertUsuarioAsync("admin", instA);
        var adminB = await InsertUsuarioAsync("admin", instB);

        // Concepto en la institucion B (solo adminB tiene permiso en B).
        var conceptoB = await AuthScalarAsync<Guid>(adminB, """
            select public.rpc_crear_concepto_financiero('Colegio B', 500.00, 'Solo B', $1)
            """, instB);

        // AdminA intenta crear un plan EN A cuya cuota referencia el concepto de B:
        // el trigger trg_plan_cuotas_concepto_institucion debe rechazarlo (23503) y
        // revertir la creacion del plan (ninguna fila residual).
        var cuotaAjena = $$"""
            [{"orden":1,"concepto_id":"{{conceptoB:N}}","descripcion":"Cuota ajena","monto":50,"vencimiento_dias":10}]
            """;
        var error = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminA, "select public.rpc_crear_plan_pago('Plan Con Cuota Ajena', 'Invalido', $1::jsonb, $2)",
            cuotaAjena, instA));
        Assert.Equal("23503", error.SqlState);

        // No debe quedar ningun plan creado ni cuota huérfana a medias.
        Assert.Equal(0L, await AuthScalarAsync<long>(adminA,
            "select count(*) from public.rpc_listar_planes_pago($1)", instA));

        // Un plan valido con el mismo concepto (dentro de A) SI se crea y se listan sus cuotas.
        var conceptoA = await AuthScalarAsync<Guid>(adminA, """
            select public.rpc_crear_concepto_financiero('Colegio A', 800.00, 'Solo A', $1)
            """, instA);
        var planValido = await AuthScalarAsync<Guid>(adminA,
            "select public.rpc_crear_plan_pago('Plan Valido', 'Ok', $1::jsonb, $2)",
            $$"""[{"orden":1,"concepto_id":"{{conceptoA:N}}","descripcion":"Cuota A","monto":60,"vencimiento_dias":10}]""",
            instA);
        Assert.Equal(1L, await AuthScalarAsync<long>(adminA,
            "select count(*) from public.rpc_obtener_plan_pago($1, $2)", planValido, instA));
    }

    private async Task ResetAsync()
    {
        await ExecuteAsync("delete from public.plan_cuotas");
        await ExecuteAsync("delete from public.planes_pago");
        await ExecuteAsync("delete from public.conceptos_financieros");
        await ExecuteAsync("delete from public.usuarios_roles");
        await ExecuteAsync("delete from public.usuarios");
        await ExecuteAsync("delete from public.personas");
        await ExecuteAsync("delete from public.instituciones");
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = false where id = 1");
    }

    private async Task SetMultiInstitucionAsync() => await ExecuteAsync(
        "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");

    private async Task<Guid> InsertInstitucionAsync() => await ScalarAsync<Guid>(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

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
}