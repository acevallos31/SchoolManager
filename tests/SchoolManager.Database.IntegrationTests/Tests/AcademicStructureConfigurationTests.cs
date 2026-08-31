using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class AcademicStructureConfigurationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Grados_admin_crea_lista_edita_desactiva_y_reactiva_sin_borrado()
    {
        var institutionId = await InstitutionAsync();
        await MultiAsync();
        var adminAuthId = await UserAsync("admin");
        var gradeId = await AuthScalarAsync<Guid>(adminAuthId,
            "select public.rpc_crear_grado($1, $2, $3)", "Basico", 1, institutionId);

        Assert.Equal(1L, await AuthScalarAsync<long>(adminAuthId,
            "select count(*) from public.rpc_listar_grados($1) where id = $2", institutionId, gradeId));
        await AuthExecuteAsync(adminAuthId,
            "select public.rpc_actualizar_grado($1, $2, $3, $4)", gradeId, "Basico actualizado", 2, institutionId);
        await AuthExecuteAsync(adminAuthId, "select public.rpc_desactivar_grado($1, $2)", gradeId, institutionId);
        Assert.False(await ScalarAsync<bool>("select activo from public.grados where id = $1", gradeId));
        await AuthExecuteAsync(adminAuthId, "select public.rpc_reactivar_grado($1, $2)", gradeId, institutionId);
        Assert.True(await ScalarAsync<bool>("select activo from public.grados where id = $1", gradeId));
    }

    [Fact]
    public async Task Grados_rechazan_datos_invalidos_duplicados_y_sin_permiso()
    {
        var institutionId = await InstitutionAsync();
        await MultiAsync();
        var adminAuthId = await UserAsync("admin");
        await AuthScalarAsync<Guid>(adminAuthId, "select public.rpc_crear_grado($1, $2, $3)", "Unico", 0, institutionId);

        await AssertStateAsync("22023", () => AuthScalarAsync<Guid>(adminAuthId, "select public.rpc_crear_grado($1, $2, $3)", " ", 0, institutionId));
        await AssertStateAsync("22023", () => AuthScalarAsync<Guid>(adminAuthId, "select public.rpc_crear_grado($1, $2, $3)", "Negativo", -1, institutionId));
        await AssertStateAsync("23505", () => AuthScalarAsync<Guid>(adminAuthId, "select public.rpc_crear_grado($1, $2, $3)", "unico", 0, institutionId));
        var parentAuthId = await UserAsync("padre");
        await AssertStateAsync("42501", () => AuthScalarAsync<Guid>(parentAuthId, "select public.rpc_crear_grado($1, $2, $3)", "No autorizado", 0, institutionId));
    }

    [Fact]
    public async Task Jornadas_se_administran_sin_borrado_y_validan_permiso()
    {
        var institutionId = await InstitutionAsync();
        await MultiAsync();
        var adminAuthId = await UserAsync("admin");
        var jornadaId = await AuthScalarAsync<Guid>(adminAuthId, "select public.rpc_crear_jornada($1, $2)", "Matutina", institutionId);
        await AuthExecuteAsync(adminAuthId, "select public.rpc_actualizar_jornada($1, $2, $3)", jornadaId, "Diurna", institutionId);
        await AuthExecuteAsync(adminAuthId, "select public.rpc_desactivar_jornada($1, $2)", jornadaId, institutionId);
        await AuthExecuteAsync(adminAuthId, "select public.rpc_reactivar_jornada($1, $2)", jornadaId, institutionId);

        await AssertStateAsync("22023", () => AuthScalarAsync<Guid>(adminAuthId, "select public.rpc_crear_jornada($1, $2)", "", institutionId));
        await AssertStateAsync("23505", () => AuthScalarAsync<Guid>(adminAuthId, "select public.rpc_crear_jornada($1, $2)", "diurna", institutionId));
        var parentAuthId = await UserAsync("padre");
        await AssertStateAsync("42501", () => AuthScalarAsync<Guid>(parentAuthId, "select public.rpc_crear_jornada($1, $2)", "No autorizada", institutionId));
        Assert.Equal(1L, await ScalarAsync<long>("select count(*) from public.jornadas where id = $1", jornadaId));
    }

    [Fact]
    public async Task Secciones_validan_contexto_cupo_catalogos_y_duplicados()
    {
        var context = await ContextAsync();
        var sectionId = await CreateSectionAsync(context, "A", null);
        await CreateSectionAsync(context, "B", 10);
        await AssertStateAsync("22023", () => CreateSectionAsync(context, "C", 0));
        await AssertStateAsync("22023", () => CreateSectionAsync(context, "D", -1));
        await AssertStateAsync("23505", () => CreateSectionAsync(context, "a", null));

        var otherInstitutionId = await InstitutionAsync();
        await AssertStateAsync("23503", () => AuthScalarAsync<Guid>(context.AdminAuthId,
            "select public.rpc_crear_seccion($1, $2, $3, null, $4, null)", otherInstitutionId, context.CycleId, context.GradeId, "Otra"));
        await SetAsync("update public.ciclos_escolares set activo = false where id = $1", context.CycleId);
        await AssertStateAsync("23503", () => CreateSectionAsync(context, "Ciclo inactivo", null));
        await SetAsync("update public.ciclos_escolares set activo = true where id = $1", context.CycleId);
        await SetAsync("update public.grados set activo = false where id = $1", context.GradeId);
        await AssertStateAsync("23503", () => CreateSectionAsync(context, "Grado inactivo", null));
        await SetAsync("update public.grados set activo = true where id = $1", context.GradeId);
        await SetAsync("update public.jornadas set activo = false where id = $1", context.JornadaId);
        await AssertStateAsync("23503", () => AuthScalarAsync<Guid>(context.AdminAuthId,
            "select public.rpc_crear_seccion($1, $2, $3, $4, $5, null)", context.InstitutionId, context.CycleId, context.GradeId, context.JornadaId, "Jornada inactiva"));
        Assert.NotEqual(Guid.Empty, sectionId);
    }

    [Fact]
    public async Task Secciones_nombre_es_unico_por_institucion_ciclo_y_grado_sin_importar_jornada()
    {
        var context = await ContextAsync();

        await CreateSectionAsync(context, "Primero B", 20);

        await AssertStateAsync("23505", () =>
            AuthScalarAsync<Guid>(
                context.AdminAuthId,
                "select public.rpc_crear_seccion($1,$2,$3,$4,$5,$6)",
                context.InstitutionId,
                context.CycleId,
                context.GradeId,
                context.JornadaId,
                "Primero B",
                25));

        await AssertStateAsync("23505", () =>
            AuthScalarAsync<Guid>(
                context.AdminAuthId,
                "select public.rpc_crear_seccion($1,$2,$3,$4,$5,$6)",
                context.InstitutionId,
                context.CycleId,
                context.GradeId,
                context.JornadaId,
                "primero b",
                25));

        await AssertStateAsync("23505", () =>
            AuthScalarAsync<Guid>(
                context.AdminAuthId,
                "select public.rpc_crear_seccion($1,$2,$3,$4,$5,$6)",
                context.InstitutionId,
                context.CycleId,
                context.GradeId,
                context.JornadaId,
                "  Primero B  ",
                25));
    }

    [Fact]
    public async Task Secciones_respetan_contexto_multi_reactivacion_e_historial()
    {
        var context = await ContextAsync();
        var sectionId = await CreateSectionAsync(context, "A", null);
        await SetAsync("update public.configuracion_implementacion set multiples_instituciones = true where id = 1");
        await AssertStateAsync("SM003", () => AuthExecuteAsync(context.AdminAuthId,
            "select public.rpc_actualizar_seccion($1, $2, $3, null, $4, $5)", sectionId, context.CycleId, context.GradeId, "A", 5));
        await AuthExecuteAsync(context.AdminAuthId,
            "select public.rpc_actualizar_seccion($1, $2, $3, null, $4, $5, $6)", sectionId, context.CycleId, context.GradeId, "A editada", 5, context.InstitutionId);
        await AuthExecuteAsync(context.AdminAuthId,
            "select public.rpc_desactivar_seccion($1, $2, $3)", sectionId, "Cierre", context.InstitutionId);
        await SetAsync("update public.ciclos_escolares set activo = false where id = $1", context.CycleId);
        await AssertStateAsync("23503", () => AuthExecuteAsync(context.AdminAuthId,
            "select public.rpc_reactivar_seccion($1, $2)", sectionId, context.InstitutionId));

        await SetAsync("update public.ciclos_escolares set activo = true where id = $1", context.CycleId);
        await AuthExecuteAsync(context.AdminAuthId,
            "select public.rpc_reactivar_seccion($1, $2)", sectionId, context.InstitutionId);
        await InsertMatriculaAsync(context, sectionId);
        var otherCycleId = await CycleAsync(context.InstitutionId);
        await AssertStateAsync("23514", () => AuthExecuteAsync(context.AdminAuthId,
            "select public.rpc_actualizar_seccion($1, $2, $3, null, $4, $5, $6)", sectionId, otherCycleId, context.GradeId, "A", 5, context.InstitutionId));
        await AuthExecuteAsync(context.AdminAuthId,
            "select public.rpc_actualizar_seccion($1, $2, $3, null, $4, $5, $6)", sectionId, context.CycleId, context.GradeId, "A historica", 6, context.InstitutionId);
    }

    [Fact]
    public async Task Listar_secciones_por_rpc_autenticada_devuelve_contexto_y_jornadas()
    {
        var context = await ContextAsync();
        var sinJornadaId = await CreateSectionAsync(context, "Sin jornada", null);
        var conJornadaId = await AuthScalarAsync<Guid>(context.AdminAuthId,
            "select public.rpc_crear_seccion($1,$2,$3,$4,$5,$6)", context.InstitutionId, context.CycleId, context.GradeId, context.JornadaId, "Con jornada", 25);

        var rows = await AuthRowsAsync(context.AdminAuthId,
            "select id, institucion_id, ciclo_id, grado_id, jornada_id, nombre from public.rpc_listar_secciones($1,$2)", context.CycleId, context.InstitutionId);

        Assert.Contains(rows, row => row.Id == sinJornadaId && row.InstitutionId == context.InstitutionId && row.CycleId == context.CycleId && row.GradeId == context.GradeId && row.JornadaId is null && row.Name == "Sin jornada");
        Assert.Contains(rows, row => row.Id == conJornadaId && row.InstitutionId == context.InstitutionId && row.CycleId == context.CycleId && row.GradeId == context.GradeId && row.JornadaId == context.JornadaId && row.Name == "Con jornada");
    }

    private async Task<Context> ContextAsync()
    {
        var institutionId = await InstitutionAsync();
        await MultiAsync();
        var adminAuthId = await UserAsync("admin");
        var cycleId = await CycleAsync(institutionId);
        var gradeId = await ScalarGuidAsync("insert into public.grados(nombre, orden) values($1, 0) returning id", $"Grado {Guid.NewGuid():N}");
        var jornadaId = await ScalarGuidAsync("insert into public.jornadas(nombre) values($1) returning id", $"Jornada {Guid.NewGuid():N}");
        return new Context(institutionId, adminAuthId, cycleId, gradeId, jornadaId);
    }

    private Task<Guid> InstitutionAsync() => ScalarGuidAsync("insert into public.instituciones(nombre) values($1) returning id", $"Institucion {Guid.NewGuid():N}");
    private Task MultiAsync() => SetAsync("update public.configuracion_implementacion set multiples_instituciones = true where id = 1");
    private Task<Guid> CycleAsync(Guid institutionId) => ScalarGuidAsync("insert into public.ciclos_escolares(institucion_id,nombre,fecha_inicio,fecha_fin) values($1,$2,current_date,current_date + 90) returning id", institutionId, $"Ciclo {Guid.NewGuid():N}");
    private async Task<Guid> UserAsync(string role)
    {
        var personId = await ScalarGuidAsync("insert into public.personas(nombres,apellidos) values('Usuario',$1) returning id", Guid.NewGuid().ToString("N"));
        var authId = Guid.NewGuid();
        var userId = await ScalarGuidAsync("insert into public.usuarios(persona_id,auth_user_id) values($1,$2) returning id", personId, authId);
        var roleId = await ScalarGuidAsync("select id from public.roles where codigo=$1", role);
        await SetAsync("insert into public.usuarios_roles(usuario_id,rol_id) values($1,$2)", userId, roleId);
        return authId;
    }

    private Task<Guid> CreateSectionAsync(Context context, string name, int? capacity) => capacity.HasValue
        ? AuthScalarAsync<Guid>(context.AdminAuthId, "select public.rpc_crear_seccion($1,$2,$3,null,$4,$5)", context.InstitutionId, context.CycleId, context.GradeId, name, capacity.Value)
        : AuthScalarAsync<Guid>(context.AdminAuthId, "select public.rpc_crear_seccion($1,$2,$3,null,$4,null)", context.InstitutionId, context.CycleId, context.GradeId, name);

    private async Task InsertMatriculaAsync(Context context, Guid sectionId)
    {
        var personId = await ScalarGuidAsync("insert into public.personas(nombres,apellidos) values('Alumno',$1) returning id", Guid.NewGuid().ToString("N"));
        var studentId = await ScalarGuidAsync("insert into public.alumnos(persona_id,institucion_id) values($1,$2) returning id", personId, context.InstitutionId);
        var periodId = await ScalarGuidAsync("insert into public.periodos_matricula(ciclo_id,nombre,fecha_inicio,fecha_fin) values($1,$2,current_date,current_date + 1) returning id", context.CycleId, $"Periodo {Guid.NewGuid():N}");
        await SetAsync("insert into public.matriculas(alumno_id,institucion_id,ciclo_id,seccion_id,periodo_matricula_id) values($1,$2,$3,$4,$5)", studentId, context.InstitutionId, context.CycleId, sectionId, periodId);
    }

    private async Task AuthExecuteAsync(Guid authId, string sql, params object[] values) => _ = await AuthScalarAsync<object?>(authId, sql, values);
    private async Task<T> AuthScalarAsync<T>(Guid authId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var role = new NpgsqlCommand("set local role authenticated", connection, transaction)) await role.ExecuteNonQueryAsync();
            await using (var claim = new NpgsqlCommand("select set_config('request.jwt.claim.sub',$1,true)", connection, transaction)) { claim.Parameters.AddWithValue(authId.ToString()); await claim.ExecuteNonQueryAsync(); }
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            var result = await command.ExecuteScalarAsync();
            await transaction.CommitAsync();
            return result is null or DBNull ? default! : (T)result;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private async Task<List<SectionRow>> AuthRowsAsync(Guid authId, string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var role = new NpgsqlCommand("set local role authenticated", connection, transaction)) await role.ExecuteNonQueryAsync();
            await using (var claim = new NpgsqlCommand("select set_config('request.jwt.claim.sub',$1,true)", connection, transaction)) { claim.Parameters.AddWithValue(authId.ToString()); await claim.ExecuteNonQueryAsync(); }
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<SectionRow>();
            while (await reader.ReadAsync()) rows.Add(new SectionRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetString(5)));
            await reader.CloseAsync();
            await transaction.CommitAsync();
            return rows;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private static async Task AssertStateAsync(string state, Func<Task> action)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(action);
        Assert.Equal(state, exception.SqlState);
    }

    private async Task SetAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql); AddParameters(command, values); await command.ExecuteNonQueryAsync();
    }
    private async Task<T> ScalarAsync<T>(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql); AddParameters(command, values); return (T)(await command.ExecuteScalarAsync())!;
    }
    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values) => await ScalarAsync<Guid>(sql, values);
    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values) { foreach (var value in values) command.Parameters.AddWithValue(value); }
    private sealed record Context(Guid InstitutionId, Guid AdminAuthId, Guid CycleId, Guid GradeId, Guid JornadaId);
    private sealed record SectionRow(Guid Id, Guid InstitutionId, Guid CycleId, Guid GradeId, Guid? JornadaId, string Name);
}