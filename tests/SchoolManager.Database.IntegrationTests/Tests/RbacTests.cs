using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class RbacTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Rol_valido_puede_crearse()
    {
        var rolId = await InsertRolAsync(Codigo("rol"));
        Assert.NotEqual(Guid.Empty, rolId);
    }

    [Fact]
    public async Task Codigo_de_rol_es_unico()
    {
        var codigo = Codigo("rol_unico");
        await InsertRolAsync(codigo);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertRolAsync(codigo));
        Assert.Equal("23505", exception.SqlState);
        Assert.Equal("uq_roles_codigo", exception.ConstraintName);
    }

    [Fact]
    public async Task Permiso_valido_puede_crearse()
    {
        var permisoId = await InsertPermisoAsync(CodigoPermiso("crear"));
        Assert.NotEqual(Guid.Empty, permisoId);
    }

    [Fact]
    public async Task Codigo_de_permiso_es_unico()
    {
        var codigo = CodigoPermiso("unico");
        await InsertPermisoAsync(codigo);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertPermisoAsync(codigo));
        Assert.Equal("23505", exception.SqlState);
        Assert.Equal("uq_permisos_codigo", exception.ConstraintName);
    }

    [Fact]
    public async Task Rol_puede_tener_multiples_permisos()
    {
        var rolId = await InsertRolAsync(Codigo("multipermiso"));
        await AssignPermisoAsync(rolId, await InsertPermisoAsync(CodigoPermiso("ver")));
        await AssignPermisoAsync(rolId, await InsertPermisoAsync(CodigoPermiso("editar")));

        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.roles_permisos where rol_id = $1", rolId));
    }

    [Fact]
    public async Task Usuario_puede_tener_multiples_roles()
    {
        var usuario = await InsertUsuarioAsync();
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("admin"));
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("padre"));

        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where usuario_id = $1 and activo", usuario.Id));
    }

    [Fact]
    public async Task Usuario_puede_tener_roles_distintos_por_institucion()
    {
        var usuario = await InsertUsuarioAsync();
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();

        await AssignRolAsync(usuario.Id, await GetRolIdAsync("admin"), institucionA);
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("docente"), institucionB);

        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where usuario_id = $1 and activo", usuario.Id));
    }

    [Fact]
    public async Task Asignacion_activa_exacta_duplicada_es_rechazada()
    {
        var usuario = await InsertUsuarioAsync();
        var rolId = await GetRolIdAsync("operador");
        var institucionId = await InsertInstitucionAsync();
        await AssignRolAsync(usuario.Id, rolId, institucionId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AssignRolAsync(usuario.Id, rolId, institucionId));
        Assert.Equal("23505", exception.SqlState);
        Assert.Equal("ux_usuarios_roles_institucion_activo", exception.ConstraintName);
    }

    [Fact]
    public async Task Mismo_rol_global_e_institucional_pueden_coexistir()
    {
        var usuario = await InsertUsuarioAsync();
        var rolId = await GetRolIdAsync("consulta");
        await AssignRolAsync(usuario.Id, rolId);
        await AssignRolAsync(usuario.Id, rolId, await InsertInstitucionAsync());

        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.usuarios_roles where usuario_id = $1 and activo", usuario.Id));
    }

    [Fact]
    public async Task Usuario_tiene_permiso_devuelve_true()
    {
        var usuario = await InsertUsuarioAsync();
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("admin"));

        Assert.True(await TienePermisoAsync(usuario.AuthUserId, "academico.alumnos.ver"));
    }

    [Fact]
    public async Task Usuario_tiene_permiso_devuelve_false()
    {
        var usuario = await InsertUsuarioAsync();
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("usuario"));

        Assert.False(await TienePermisoAsync(usuario.AuthUserId, "academico.alumnos.ver"));
        Assert.False(await TienePermisoAsync(Guid.NewGuid(), "academico.alumnos.ver"));
    }

    [Fact]
    public async Task Rol_institucional_solo_aplica_al_ambito_solicitado()
    {
        var usuario = await InsertUsuarioAsync();
        var institucionPermitida = await InsertInstitucionAsync();
        var otraInstitucion = await InsertInstitucionAsync();
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("admin"), institucionPermitida);

        Assert.True(await TienePermisoAsync(
            usuario.AuthUserId, "academico.alumnos.ver", institucionPermitida));
        Assert.False(await TienePermisoAsync(
            usuario.AuthUserId, "academico.alumnos.ver", otraInstitucion));
        Assert.False(await TienePermisoAsync(usuario.AuthUserId, "academico.alumnos.ver"));
    }

    [Fact]
    public async Task Usuario_inactivo_no_tiene_permiso()
    {
        var usuario = await InsertUsuarioAsync(activo: false);
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("admin"));

        Assert.False(await TienePermisoAsync(usuario.AuthUserId, "academico.alumnos.ver"));
    }

    [Fact]
    public async Task Rol_inactivo_no_otorga_permiso()
    {
        var usuario = await InsertUsuarioAsync();
        var rolId = await InsertRolAsync(Codigo("inactivo"), activo: false);
        var permisoCodigo = CodigoPermiso("inactivo");
        await AssignPermisoAsync(rolId, await InsertPermisoAsync(permisoCodigo));
        await AssignRolAsync(usuario.Id, rolId);

        Assert.False(await TienePermisoAsync(usuario.AuthUserId, permisoCodigo));
    }

    [Fact]
    public async Task Auth_user_id_sigue_siendo_unico()
    {
        var authUserId = Guid.NewGuid();
        await InsertUsuarioAsync(authUserId: authUserId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertUsuarioAsync(authUserId: authUserId));
        Assert.Equal("23505", exception.SqlState);
        Assert.Equal("ux_usuarios_auth_user_id", exception.ConstraintName);
    }

    [Fact]
    public async Task Migracion_010_elimina_rol_legacy_sin_afectar_asignaciones()
    {
        var usuario = await InsertUsuarioAsync();
        await AssignRolAsync(usuario.Id, await GetRolIdAsync("padre"));

        Assert.Equal(1, await ScalarLongAsync("""
            select count(*)
            from public.usuarios_roles ur
            join public.roles r on r.id = ur.rol_id
            where ur.usuario_id = $1
              and ur.institucion_id is null
              and ur.activo
              and r.codigo = 'padre'
            """, usuario.Id));
        Assert.Equal(0, await ScalarLongAsync("""
            select count(*) from information_schema.columns
            where table_schema = 'public' and table_name = 'usuarios' and column_name = 'rol'
            """));
    }

    private async Task<(Guid Id, Guid AuthUserId)> InsertUsuarioAsync(
        bool activo = true,
        Guid? authUserId = null)
    {
        var id = Guid.NewGuid();
        var authId = authUserId ?? Guid.NewGuid();
        await ExecuteAsync(
            "insert into public.usuarios (id, auth_user_id, activo) values ($1, $2, $3)",
            id, authId, activo);
        return (id, authId);
    }

    private Task<Guid> InsertRolAsync(string codigo, bool activo = true) => ScalarGuidAsync(
        "insert into public.roles (codigo, nombre, activo) values ($1, $2, $3) returning id",
        codigo, $"Rol {codigo}", activo);

    private Task<Guid> InsertPermisoAsync(string codigo) => ScalarGuidAsync(
        "insert into public.permisos (codigo, modulo, nombre) values ($1, 'pruebas', $2) returning id",
        codigo, $"Permiso {codigo}");

    private Task<Guid> InsertInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private Task<Guid> GetRolIdAsync(string codigo) => ScalarGuidAsync(
        "select id from public.roles where codigo = $1", codigo);

    private Task AssignRolAsync(Guid usuarioId, Guid rolId, Guid? institucionId = null) =>
        institucionId.HasValue
            ? ExecuteAsync(
                "insert into public.usuarios_roles (usuario_id, rol_id, institucion_id) values ($1, $2, $3)",
                usuarioId, rolId, institucionId.Value)
            : ExecuteAsync(
                "insert into public.usuarios_roles (usuario_id, rol_id) values ($1, $2)",
                usuarioId, rolId);

    private Task AssignPermisoAsync(Guid rolId, Guid permisoId) => ExecuteAsync(
        "insert into public.roles_permisos (rol_id, permiso_id) values ($1, $2)", rolId, permisoId);

    private async Task<bool> TienePermisoAsync(
        Guid authUserId,
        string permiso,
        Guid? institucionId = null)
    {
        var sql = institucionId.HasValue
            ? "select public.usuario_tiene_permiso($1, $2, $3)"
            : "select public.usuario_tiene_permiso($1, $2)";
        await using var command = fixture.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(authUserId);
        command.Parameters.AddWithValue(permiso);
        if (institucionId.HasValue)
        {
            command.Parameters.AddWithValue(institucionId.Value);
        }
        return (bool)(await command.ExecuteScalarAsync())!;
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
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    private static string Codigo(string prefijo) => $"{prefijo}_{Guid.NewGuid():N}";
    private static string CodigoPermiso(string accion) => $"pruebas.recurso_{Guid.NewGuid():N}.{accion}";
}
