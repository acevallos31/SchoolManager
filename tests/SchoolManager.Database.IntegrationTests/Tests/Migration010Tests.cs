using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class Migration010Tests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Backfill_007_sobrevive_y_010_elimina_columna_legacy()
    {
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*)
            from public.usuarios u
            join public.usuarios_roles ur on ur.usuario_id = u.id and ur.activo
            join public.roles r on r.id = ur.rol_id and r.activo
            where u.usuario = 'fixture-backfill-007'
              and ur.institucion_id is null
              and r.codigo = 'padre'
            """));
        Assert.Equal(0, await ScalarLongAsync("""
            select count(*) from information_schema.columns
            where table_schema = 'public' and table_name = 'usuarios' and column_name = 'rol'
            """));
    }

    [Fact]
    public async Task Migracion_010_esta_registrada_y_RLS_permanece_activa()
    {
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.schema_migrations where version = '010'"));
        Assert.Equal(4, await ScalarLongAsync("""
            select count(*) from pg_class
            where oid in (
              'public.usuarios'::regclass, 'public.usuarios_roles'::regclass,
              'public.roles'::regclass, 'public.permisos'::regclass
            ) and relrowsecurity
            """));
    }

    [Fact]
    public async Task Usuario_multirol_y_permisos_siguen_validos()
    {
        var usuarioId = await ScalarGuidAsync("""
            insert into public.usuarios (usuario, auth_user_id)
            values ($1, $2) returning id
            """, $"multi-{Guid.NewGuid():N}", Guid.NewGuid());
        await ExecuteAsync("""
            insert into public.usuarios_roles (usuario_id, rol_id)
            select $1, id from public.roles where codigo in ('admin', 'padre')
            """, usuarioId);

        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where usuario_id = $1 and activo", usuarioId));
        Assert.True(await ScalarBoolAsync("""
            select public.usuario_tiene_permiso(u.auth_user_id, 'academico.alumnos.ver')
            from public.usuarios u where u.id = $1
            """, usuarioId));
    }

    private async Task ExecuteAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> ScalarLongAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (long)(await command.ExecuteScalarAsync())!;
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
