using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

/// <summary>
/// Migracion 027: vinculacion explicita identidad OAuth &lt;-&gt; usuario de aplicacion.
/// La RPC es el unico eslabon que escribe public.usuarios.auth_user_id; estas
/// pruebas fijan sus guardas para que no derive en vinculacion automatica.
/// </summary>
public sealed class Migration027Tests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Migracion_027_esta_registrada_y_expone_la_funcion()
    {
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.schema_migrations where version = '027'"));
        Assert.NotNull(await ScalarStringAsync(
            "select to_regprocedure('public.vincular_identidad_usuario(uuid, uuid)')::text"));
    }

    [Fact]
    public async Task Vincula_la_identidad_de_un_usuario_activo()
    {
        var usuarioId = await CrearUsuarioAsync();
        var authUserId = Guid.NewGuid();

        var resultado = await VincularAsync(usuarioId, authUserId);

        Assert.Equal("vinculado", resultado);
        Assert.Equal(authUserId, await ScalarGuidAsync(
            "select auth_user_id from public.usuarios where id = $1", usuarioId));
    }

    [Fact]
    public async Task Repetir_la_vinculacion_es_idempotente_y_no_duplica()
    {
        var usuarioId = await CrearUsuarioAsync();
        var authUserId = Guid.NewGuid();

        Assert.Equal("vinculado", await VincularAsync(usuarioId, authUserId));
        Assert.Equal("ya_vinculado", await VincularAsync(usuarioId, authUserId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios where auth_user_id = $1", authUserId));
    }

    [Fact]
    public async Task Rechaza_una_identidad_ya_vinculada_a_otro_usuario()
    {
        var primerUsuario = await CrearUsuarioAsync();
        var segundoUsuario = await CrearUsuarioAsync();
        var authUserId = Guid.NewGuid();
        Assert.Equal("vinculado", await VincularAsync(primerUsuario, authUserId));

        var excepcion = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => VincularAsync(segundoUsuario, authUserId));

        Assert.Equal("23505", excepcion.SqlState);
    }

    [Fact]
    public async Task Rechaza_usuario_inactivo()
    {
        var usuarioId = await CrearUsuarioAsync(activo: false);

        var excepcion = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => VincularAsync(usuarioId, Guid.NewGuid()));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    [Fact]
    public async Task Rechaza_usuario_inexistente()
    {
        var excepcion = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => VincularAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("P0002", excepcion.SqlState);
    }

    [Fact]
    public async Task Exige_desvincular_antes_de_reasignar_una_identidad_distinta()
    {
        var usuarioId = await CrearUsuarioAsync();
        Assert.Equal("vinculado", await VincularAsync(usuarioId, Guid.NewGuid()));

        var excepcion = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => VincularAsync(usuarioId, Guid.NewGuid()));

        Assert.Equal("P0001", excepcion.SqlState);
    }

    [Fact]
    public async Task La_vinculacion_no_asigna_roles_ni_permisos()
    {
        var usuarioId = await CrearUsuarioAsync();
        Assert.Equal("vinculado", await VincularAsync(usuarioId, Guid.NewGuid()));

        Assert.Equal(0, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where usuario_id = $1", usuarioId));
    }

    [Fact]
    public async Task La_funcion_no_es_ejecutable_por_anon_ni_authenticated()
    {
        Assert.False(await ScalarBoolAsync("""
            select has_function_privilege(
              'anon', 'public.vincular_identidad_usuario(uuid, uuid)', 'execute')
            """));
        Assert.False(await ScalarBoolAsync("""
            select has_function_privilege(
              'authenticated', 'public.vincular_identidad_usuario(uuid, uuid)', 'execute')
            """));
    }

    private async Task<string> VincularAsync(Guid usuarioId, Guid authUserId)
    {
        await using var command = fixture.DataSource.CreateCommand(
            "select public.vincular_identidad_usuario($1, $2)");
        command.Parameters.AddWithValue(usuarioId);
        command.Parameters.AddWithValue(authUserId);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CrearUsuarioAsync(bool activo = true)
    {
        await using var command = fixture.DataSource.CreateCommand("""
            insert into public.usuarios (usuario, activo)
            values ($1, $2) returning id
            """);
        command.Parameters.AddWithValue($"oauth-027-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue(activo);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> ScalarLongAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string?> ScalarStringAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        var resultado = await command.ExecuteScalarAsync();
        return resultado is null or DBNull ? null : (string)resultado;
    }

    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> ScalarBoolAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(Npgsql.NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }
}
