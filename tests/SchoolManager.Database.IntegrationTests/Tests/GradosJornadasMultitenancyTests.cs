using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Pruebas de la migracion 020: aislamiento multitenant de los catalogos de
/// grados y jornadas. Tras la migracion, grados/jornadas llevan institucion_id
/// (NOT NULL), unicidad por-institucion y las secciones solo pueden referenciar
/// grado/jornada de su misma institucion (FK compuesta de contexto).
///
/// Se prueban las RPC SECURITY DEFINER directamente (su bypass de RLS deja la
/// aislacion en manos de los cheques internos) y el RLS de lectura para el
/// acceso por UUID conocido. El patrón de autenticacion (set local role
/// authenticated + set_config request.jwt.claim.sub) es el mismo de
/// CargosMultitenancyTests.
/// </summary>
public sealed class GradosJornadasMultitenancyTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task A_lista_solo_sus_grados()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");

        var gA1 = await InsertGradoAsync(a.Id, "7A");
        var gA2 = await InsertGradoAsync(a.Id, "8A");
        var gB1 = await InsertGradoAsync(b.Id, "7B");

        var ids = await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_grados($1)", a.Id);
        Assert.Equal(2, ids);

        // Ningun grado de B aparece en el listado de A.
        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_grados($1) where id=$2", a.Id, gB1));
        Assert.Equal(1L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_grados($1) where id=$2", a.Id, gA1));
        _ = gA2;
    }

    [Fact]
    public async Task A_lista_solo_sus_jornadas()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");

        await InsertJornadaAsync(a.Id, "Matutina");
        await InsertJornadaAsync(a.Id, "Vespertina");
        var jB = await InsertJornadaAsync(b.Id, "Nocturna");

        Assert.Equal(2L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_jornadas($1)", a.Id));
        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_jornadas($1) where id=$2", a.Id, jB));
        Assert.Equal(1L, await AuthScalarAsync<long>(b.Admin,
            "select count(*) from public.rpc_listar_jornadas($1) where id=$2", b.Id, jB));
    }

    [Fact]
    public async Task A_no_obtiene_por_uuid_un_grado_de_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var gB = await InsertGradoAsync(b.Id, "SoloB");

        // RLS de lectura oculta el grado ajeno incluso conociendo su UUID.
        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.grados where id=$1", gB));
        Assert.Equal(1L, await AuthScalarAsync<long>(b.Admin,
            "select count(*) from public.grados where id=$1", gB));
    }

    [Fact]
    public async Task A_no_obtiene_por_uuid_una_jornada_de_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var jB = await InsertJornadaAsync(b.Id, "SoloB");

        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.jornadas where id=$1", jB));
        Assert.Equal(1L, await AuthScalarAsync<long>(b.Admin,
            "select count(*) from public.jornadas where id=$1", jB));
    }

    [Fact]
    public async Task A_no_actualiza_un_grado_de_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var gB = await InsertGradoAsync(b.Id, "Original");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<object>(a.Admin,
                "select public.rpc_actualizar_grado($1,$2,$3,$4)", gB, "Renombrado", 2, a.Id));
        Assert.Equal("P0002", error.SqlState); // no existe dentro del contexto de A
    }

    [Fact]
    public async Task A_no_actualiza_una_jornada_de_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var jB = await InsertJornadaAsync(b.Id, "Original");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<object>(a.Admin,
                "select public.rpc_actualizar_jornada($1,$2,$3)", jB, "Renombrada", a.Id));
        Assert.Equal("P0002", error.SqlState);
    }

    [Fact]
    public async Task A_no_cambia_estado_de_grado_o_jornada_de_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var gB = await InsertGradoAsync(b.Id, "GradoB");
        var jB = await InsertJornadaAsync(b.Id, "JornadaB");

        var eGrado = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<object>(a.Admin,
                "select public.rpc_cambiar_estado_grado($1,$2,$3)", gB, false, a.Id));
        Assert.Equal("P0002", eGrado.SqlState);

        var eJornada = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<object>(a.Admin,
                "select public.rpc_cambiar_estado_jornada($1,$2,$3)", jB, false, a.Id));
        Assert.Equal("P0002", eJornada.SqlState);
    }

    [Fact]
    public async Task Mismo_nombre_de_grado_permitido_en_A_y_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");

        var gA = await AuthScalarAsync<Guid>(a.Admin,
            "select public.rpc_crear_grado($1,$2,$3)", "7mo", 0, a.Id);
        var gB = await AuthScalarAsync<Guid>(b.Admin,
            "select public.rpc_crear_grado($1,$2,$3)", "7mo", 0, b.Id);

        Assert.NotEqual(gA, gB);
        Assert.Equal(1L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_grados($1) where nombre='7mo'", a.Id));
        Assert.Equal(1L, await AuthScalarAsync<long>(b.Admin,
            "select count(*) from public.rpc_listar_grados($1) where nombre='7mo'", b.Id));
    }

    [Fact]
    public async Task Mismo_nombre_de_jornada_permitido_en_A_y_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");

        var jA = await AuthScalarAsync<Guid>(a.Admin,
            "select public.rpc_crear_jornada($1,$2)", "Matutina", a.Id);
        var jB = await AuthScalarAsync<Guid>(b.Admin,
            "select public.rpc_crear_jornada($1,$2)", "Matutina", b.Id);

        Assert.NotEqual(jA, jB);
        Assert.Equal(1L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_jornadas($1) where nombre='Matutina'", a.Id));
    }

    [Fact]
    public async Task Duplicado_dentro_de_la_misma_institucion_rechazado()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");

        await AuthScalarAsync<Guid>(a.Admin,
            "select public.rpc_crear_grado($1,$2,$3)", "5to", 0, a.Id);
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<Guid>(a.Admin,
                "select public.rpc_crear_grado($1,$2,$3)", "5to", 0, a.Id));
        Assert.Equal("23505", error.SqlState); // ux_grados_institucion_nombre
    }

    [Fact]
    public async Task Seccion_no_puede_combinar_grado_de_A_con_institucion_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var gA = await InsertGradoAsync(a.Id, "GradoA");
        var cicloB = await InsertCicloAsync(b.Id);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(
                "insert into public.secciones (nombre, institucion_id, ciclo_id, grado_id) values ($1,$2,$3,$4) returning id",
                "S", b.Id, cicloB, gA));
        Assert.Equal("23503", error.SqlState); // fk_secciones_grado_contexto
    }

    [Fact]
    public async Task Seccion_no_puede_combinar_jornada_de_A_con_institucion_B()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var jA = await InsertJornadaAsync(a.Id, "JornadaA");
        var cicloB = await InsertCicloAsync(b.Id);
        var gB = await InsertGradoAsync(b.Id, "GradoB");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(
                "insert into public.secciones (nombre, institucion_id, ciclo_id, grado_id, jornada_id) values ($1,$2,$3,$4,$5) returning id",
                "S", b.Id, cicloB, gB, jA));
        Assert.Equal("23503", error.SqlState); // fk_secciones_jornada_contexto
    }

    [Fact]
    public async Task Usuario_multi_institucion_ve_solo_el_contexto_autorizado()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        await InsertInstitucionConAdminAsync("B");

        // Admin de A conoce el UUID de B, pero no tiene rol/permiso en B.
        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_grados($1)", a.Id));

        var c = await InsertInstitucionConAdminAsync("C");

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<long>(a.Admin,
                "select count(*) from public.rpc_listar_grados($1)", c.Id));
        Assert.Equal("42501", error.SqlState); // sin permiso en C -> denegado
    }

    [Fact]
    public async Task Uuid_conocido_de_otra_institucion_no_permite_bypass()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");
        var gB = await InsertGradoAsync(b.Id, "Secreto");

        // a) La RPC no lo resuelve porque el update filtra por contexto (A).
        var eRpc = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<object>(a.Admin,
                "select public.rpc_actualizar_grado($1,$2,$3,$4)", gB, "Cambiado", 9, a.Id));
        Assert.Equal("P0002", eRpc.SqlState);

        // b) El SELECT directo lo oculta por RLS.
        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.grados where id=$1", gB));

        // El dato intacto en B.
        Assert.Equal("Secreto", await AuthScalarAsync<string>(b.Admin,
            "select nombre from public.grados where id=$1", gB));
    }

    [Fact]
    public async Task RPC_gestion_de_grados_respeta_institucion_id()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");
        var b = await InsertInstitucionConAdminAsync("B");

        // crear: queda en el contexto pasado (solo visible listando de A).
        var g = await AuthScalarAsync<Guid>(a.Admin,
            "select public.rpc_crear_grado($1,$2,$3)", "2do", 2, a.Id);
        Assert.Equal(1L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_grados($1) where id=$2", a.Id, g));

        // actualizar/cambiar_estado: operan en A.
        await AuthScalarAsync<object>(a.Admin,
            "select public.rpc_actualizar_grado($1,$2,$3,$4)", g, "2do A", 3, a.Id);
        await AuthScalarAsync<object>(a.Admin,
            "select public.rpc_cambiar_estado_grado($1,$2,$3)", g, false, a.Id);
        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_grados($1) where id=$2 and activo=true", a.Id, g));

        // crear sobre institucion sin permiso -> denegado (424 `Permiso denegado.`).
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => AuthScalarAsync<Guid>(a.Admin,
                "select public.rpc_crear_grado($1,$2,$3)", "3ro", 0, b.Id));
        Assert.Equal("42501", error.SqlState);
    }

    [Fact]
    public async Task RPC_gestion_de_jornadas_respeta_institucion_id()
    {
        await ResetAsync();
        var a = await InsertInstitucionConAdminAsync("A");

        var j = await AuthScalarAsync<Guid>(a.Admin,
            "select public.rpc_crear_jornada($1,$2)", "Continuada", a.Id);
        Assert.Equal(1L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_jornadas($1) where id=$2", a.Id, j));

        await AuthScalarAsync<object>(a.Admin,
            "select public.rpc_actualizar_jornada($1,$2,$3)", j, "Continuada 2", a.Id);
        await AuthScalarAsync<object>(a.Admin,
            "select public.rpc_cambiar_estado_jornada($1,$2,$3)", j, false, a.Id);
        Assert.Equal(0L, await AuthScalarAsync<long>(a.Admin,
            "select count(*) from public.rpc_listar_jornadas($1) where id=$2 and activo=true", a.Id, j));
    }

    // ==================== SETUP ====================

    private async Task ResetAsync()
    {
        await ExecuteAsync("delete from public.matricula_estado_historial");
        await ExecuteAsync("delete from public.matriculas");
        await ExecuteAsync("delete from public.periodos_matricula");
        await ExecuteAsync("delete from public.secciones");
        await ExecuteAsync("delete from public.grados");
        await ExecuteAsync("delete from public.jornadas");
        await ExecuteAsync("delete from public.ciclos_escolares");
        await ExecuteAsync("delete from public.usuarios_roles");
        await ExecuteAsync("delete from public.usuarios");
        await ExecuteAsync("delete from public.personas");
        await ExecuteAsync("delete from public.instituciones");
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = false where id = 1");
    }

    private async Task<InstitucionAdmin> InsertInstitucionConAdminAsync(string tag)
    {
        await ExecuteAsync(
            "update public.configuracion_implementacion set multiples_instituciones = true where id = 1");
        var institucion = await ScalarAsync<Guid>(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Institucion {tag}-{Guid.NewGuid():N}");
        var admin = await InsertUsuarioAsync("admin", institucion);
        return new InstitucionAdmin(institucion, admin);
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

    private Task<Guid> InsertGradoAsync(Guid institucionId, string nombre) =>
        ScalarAsync<Guid>(
            "insert into public.grados (nombre, orden, institucion_id) values ($1, 0, $2) returning id",
            nombre, institucionId);

    private Task<Guid> InsertJornadaAsync(Guid institucionId, string nombre) =>
        ScalarAsync<Guid>(
            "insert into public.jornadas (nombre, institucion_id) values ($1, $2) returning id",
            nombre, institucionId);

    private Task<Guid> InsertCicloAsync(Guid institucionId) =>
        ScalarAsync<Guid>(
            "insert into public.ciclos_escolares (institucion_id, nombre, fecha_inicio, fecha_fin) values ($1,$2,current_date,current_date+interval '90 days') returning id",
            institucionId, $"Ciclo {Guid.NewGuid():N}");

    // ==================== AUTH (mismo patron que CargosMultitenancyTests) ====================

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

    private sealed record InstitucionAdmin(Guid Id, Guid Admin);
}