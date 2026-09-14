using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RbacOperacionesInstitucionalesTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    private static readonly string[] PermisosAdmin =
    [
        "identidad.roles.crear",
        "identidad.roles.editar",
        "identidad.roles.asignar_permisos",
        "identidad.usuarios.asignar_roles",
        "academico.alumnos.ver"
    ];

    [Fact]
    public async Task Admin_institucional_crea_edita_configura_y_asigna_rol()
    {
        var institucion = await InsertInstitucionAsync();
        var admin = await CrearActorAsync(institucion, PermisosAdmin);
        var destino = await InsertUsuarioAsync();
        var codigo = $"secretaria_{Guid.NewGuid():N}";

        var rolId = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_crear_rol_institucional($1,$2,$3,$4)",
            institucion, codigo, "Secretaria", "Rol de prueba");

        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_reemplazar_permisos_rol_institucional($1,$2)",
            rolId, new[] { "academico.alumnos.ver" });
        await AuthExecuteAsync(admin.AuthUserId,
            "select public.rpc_editar_rol_institucional($1,$2,$3)",
            rolId, "Secretaria Academica", "Actualizado");
        var asignacion = await AuthScalarGuidAsync(admin.AuthUserId,
            "select public.rpc_asignar_rol_institucional($1,$2)", destino.UsuarioId, rolId);

        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.roles where id=$1 and institucion_id=$2 and nombre='Secretaria Academica'",
            rolId, institucion));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.roles_permisos rp
            join public.permisos p on p.id=rp.permiso_id
            where rp.rol_id=$1 and p.codigo='academico.alumnos.ver'
            """, rolId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where id=$1 and institucion_id=$2 and activo",
            asignacion, institucion));
        Assert.True(await ScalarLongAsync(
            "select count(*) from public.seguridad_auditoria where entidad_id=$1", rolId) >= 3);
    }

    [Fact]
    public async Task Admin_institucional_no_delega_permiso_que_no_posee()
    {
        var institucion = await InsertInstitucionAsync();
        var admin = await CrearActorAsync(institucion,
            ["identidad.roles.asignar_permisos", "identidad.roles.editar", "identidad.usuarios.asignar_roles"]);
        var rolId = await InsertRolInstitucionalAsync(institucion);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin.AuthUserId,
            "select public.rpc_reemplazar_permisos_rol_institucional($1,$2)",
            rolId, new[] { "academico.pagos.registrar" }));

        Assert.Equal("42501", ex.SqlState);
        Assert.Equal(0, await ScalarLongAsync(
            "select count(*) from public.roles_permisos where rol_id=$1", rolId));
    }

    [Fact]
    public async Task Invariante_DB_rechaza_permiso_de_plataforma_en_rol_institucional()
    {
        var institucion = await InsertInstitucionAsync();
        var rolId = await InsertRolInstitucionalAsync(institucion);
        var permisoId = await ScalarGuidAsync(
            "select id from public.permisos where codigo='platform.roles.ver'");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "insert into public.roles_permisos(rol_id,permiso_id) values($1,$2)", rolId, permisoId));

        Assert.Equal("23514", ex.SqlState);
    }

    [Fact]
    public async Task Institucion_A_no_puede_editar_rol_de_institucion_B()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        var adminA = await CrearActorAsync(institucionA, PermisosAdmin);
        var rolB = await InsertRolInstitucionalAsync(institucionB);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            adminA.AuthUserId,
            "select public.rpc_editar_rol_institucional($1,$2,null)",
            rolB, "Intento cruzado"));

        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task Ultimo_admin_institucional_no_puede_retirarse()
    {
        var institucion = await InsertInstitucionAsync();
        var admin = await CrearActorAsync(institucion, PermisosAdmin);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => AuthExecuteAsync(
            admin.AuthUserId,
            "select public.rpc_desactivar_rol_usuario($1,$2)",
            admin.AsignacionId, "prueba ultimo admin"));

        Assert.Equal("23514", ex.SqlState);
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where id=$1 and activo", admin.AsignacionId));
    }

    [Fact]
    public async Task Con_dos_admins_se_puede_retirar_uno()
    {
        var institucion = await InsertInstitucionAsync();
        var adminA = await CrearActorAsync(institucion, PermisosAdmin);
        var usuarioB = await InsertUsuarioAsync();
        var asignacionB = await ScalarGuidAsync("""
            insert into public.usuarios_roles(usuario_id,rol_id,institucion_id)
            values($1,$2,$3) returning id
            """, usuarioB.UsuarioId, adminA.RolId, institucion);

        await AuthExecuteAsync(adminA.AuthUserId,
            "select public.rpc_desactivar_rol_usuario($1,$2)", asignacionB, "rotacion de admin");

        Assert.Equal(0, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where id=$1 and activo", asignacionB));
        Assert.Equal(1, await ScalarLongAsync(
            "select public.contar_admins_institucionales($1)", institucion));
    }

    private async Task<Actor> CrearActorAsync(Guid institucion, IEnumerable<string> permisos)
    {
        var usuario = await InsertUsuarioAsync();
        var rolId = await InsertRolInstitucionalAsync(institucion);
        foreach (var codigo in permisos)
        {
            await ExecuteAsync("""
                insert into public.roles_permisos(rol_id,permiso_id)
                select $1,id from public.permisos where codigo=$2
                """, rolId, codigo);
        }
        var asignacion = await ScalarGuidAsync("""
            insert into public.usuarios_roles(usuario_id,rol_id,institucion_id)
            values($1,$2,$3) returning id
            """, usuario.UsuarioId, rolId, institucion);
        return new Actor(usuario.UsuarioId, usuario.AuthUserId, rolId, asignacion);
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones(nombre) values($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private Task<Guid> InsertRolInstitucionalAsync(Guid institucion) => ScalarGuidAsync("""
        insert into public.roles(codigo,nombre,tipo,institucion_id,activo)
        values($1,$2,'institucional',$3,true) returning id
        """, $"rol_{Guid.NewGuid():N}", "Rol prueba", institucion);

    private async Task<Usuario> InsertUsuarioAsync()
    {
        var authId = Guid.NewGuid();
        var id = await ScalarGuidAsync(
            "insert into public.usuarios(auth_user_id,activo) values($1,true) returning id", authId);
        return new Usuario(id, authId);
    }

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

    private static async Task SetAuthenticatedAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,Guid authUserId)
    {
        await using var role = new NpgsqlCommand("set local role authenticated", connection, transaction);
        await role.ExecuteNonQueryAsync();
        await using var auth = new NpgsqlCommand("select set_config('request.jwt.claim.sub',$1,true)", connection, transaction);
        auth.Parameters.AddWithValue(authUserId.ToString());
        await auth.ExecuteNonQueryAsync();
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
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value);
    }

    private sealed record Usuario(Guid UsuarioId, Guid AuthUserId);
    private sealed record Actor(Guid UsuarioId, Guid AuthUserId, Guid RolId, Guid AsignacionId);
}
