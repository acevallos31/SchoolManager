using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class DemoSandboxTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Institucion_normal_es_el_default_y_sesion_valida_exige_tipos_demo()
    {
        var normal = await CrearInstitucionAsync("normal");
        Assert.Equal("normal", await ScalarTextAsync(
            "select tipo from public.instituciones where id=$1", normal));

        var plantilla = await CrearInstitucionAsync("demo_template");
        var sandbox = await CrearInstitucionAsync("demo_sandbox");
        var authUserId = Guid.NewGuid();

        var sesion = await ScalarGuidAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            returning id
            """, authUserId, sandbox, plantilla);

        Assert.NotEqual(Guid.Empty, sesion);

        var exSandboxNormal = await Assert.ThrowsAsync<PostgresException>(() => ScalarGuidAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            returning id
            """, Guid.NewGuid(), normal, plantilla));
        Assert.Equal("23514", exSandboxNormal.SqlState);

        var otroSandbox = await CrearInstitucionAsync("demo_sandbox");
        var exPlantillaNormal = await Assert.ThrowsAsync<PostgresException>(() => ScalarGuidAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            returning id
            """, Guid.NewGuid(), otroSandbox, normal));
        Assert.Equal("23514", exPlantillaNormal.SqlState);
    }

    [Fact]
    public async Task Identidad_no_puede_tener_dos_sesiones_demo_activas()
    {
        var plantilla = await CrearInstitucionAsync("demo_template");
        var sandboxA = await CrearInstitucionAsync("demo_sandbox");
        var sandboxB = await CrearInstitucionAsync("demo_sandbox");
        var authUserId = Guid.NewGuid();

        await ExecuteAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            """, authUserId, sandboxA, plantilla);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            """, authUserId, sandboxB, plantilla));

        Assert.Equal("23505", ex.SqlState);
    }

    [Fact]
    public async Task Cierre_de_sesion_libera_identidad_para_una_nueva_sandbox()
    {
        var plantilla = await CrearInstitucionAsync("demo_template");
        var sandboxA = await CrearInstitucionAsync("demo_sandbox");
        var sandboxB = await CrearInstitucionAsync("demo_sandbox");
        var authUserId = Guid.NewGuid();

        var sesionA = await ScalarGuidAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            returning id
            """, authUserId, sandboxA, plantilla);

        await ExecuteAsync("""
            update public.demo_sessions
            set estado='reiniciada', closed_at=now()
            where id=$1
            """, sesionA);

        var sesionB = await ScalarGuidAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            returning id
            """, authUserId, sandboxB, plantilla);

        Assert.NotEqual(sesionA, sesionB);
    }

    [Fact]
    public async Task Tipo_demo_y_activacion_quedan_protegidos_mientras_hay_sesion()
    {
        var plantilla = await CrearInstitucionAsync("demo_template");
        var sandbox = await CrearInstitucionAsync("demo_sandbox");

        await ExecuteAsync("""
            insert into public.demo_sessions(
              auth_user_id, institucion_id, plantilla_institucion_id
            ) values($1,$2,$3)
            """, Guid.NewGuid(), sandbox, plantilla);

        var exTipo = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "update public.instituciones set tipo='normal' where id=$1", sandbox));
        Assert.Equal("23514", exTipo.SqlState);

        var exDesactivar = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "update public.instituciones set activo=false where id=$1", plantilla));
        Assert.Equal("23514", exDesactivar.SqlState);
    }

    [Fact]
    public async Task Demo_sessions_no_es_accesible_directamente_por_authenticated()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthenticatedExecuteAsync("select count(*) from public.demo_sessions"));

        Assert.Equal("42501", ex.SqlState);
    }

    private async Task<Guid> CrearInstitucionAsync(string tipo)
    {
        if (tipo == "normal")
        {
            return await ScalarGuidAsync(
                "insert into public.instituciones(nombre) values($1) returning id",
                $"Institucion {Guid.NewGuid():N}");
        }

        return await ScalarGuidAsync(
            "insert into public.instituciones(nombre,tipo) values($1,$2) returning id",
            $"Institucion {Guid.NewGuid():N}", tipo);
    }

    private async Task AuthenticatedExecuteAsync(string sql)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var role = new NpgsqlCommand(
                "set local role authenticated", connection, transaction))
            {
                await role.ExecuteNonQueryAsync();
            }

            await using var command = new NpgsqlCommand(sql, connection, transaction);
            await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
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

    private async Task<string> ScalarTextAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value);
    }
}
