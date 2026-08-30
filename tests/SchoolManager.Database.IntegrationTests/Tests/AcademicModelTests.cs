using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class AcademicModelTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Secciones_son_dinamicas_por_ciclo_y_grado()
    {
        var institucion = await InsertInstitucionAsync();
        var ciclo2026 = await InsertCicloAsync(institucion, "2026");
        var ciclo2027 = await InsertCicloAsync(institucion, "2027");
        var grado7 = await InsertGradoAsync("7mo");
        var grado8 = await InsertGradoAsync("8vo");

        await CrearSeccionAsync(institucion, ciclo2026, grado7, "A");
        await CrearSeccionAsync(institucion, ciclo2026, grado7, "B");
        await CrearSeccionAsync(institucion, ciclo2027, grado7, "A");
        await CrearSeccionAsync(institucion, ciclo2026, grado8, "A");

        Assert.Equal(4, await ScalarLongAsync(
            "select count(*) from public.secciones where institucion_id = $1", institucion));
    }

    [Fact]
    public async Task Seccion_duplicada_en_mismo_contexto_es_rechazada_incluso_sin_jornada()
    {
        var contexto = await CreateContextAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            CrearSeccionAsync(contexto.InstitucionId, contexto.CicloId, contexto.GradoId, "a"));
        Assert.Equal("23505", exception.SqlState);
        Assert.Equal("ux_secciones_contexto_sin_jornada_nombre", exception.ConstraintName);
    }

    [Fact]
    public async Task Seccion_no_puede_combinar_ciclo_e_institucion_distintos()
    {
        var institucionA = await InsertInstitucionAsync();
        var institucionB = await InsertInstitucionAsync();
        var cicloA = await InsertCicloAsync(institucionA);
        var grado = await InsertGradoAsync();

        await Assert.ThrowsAsync<PostgresException>(() =>
            CrearSeccionAsync(institucionB, cicloA, grado, "A"));
    }

    [Fact]
    public async Task Matricula_valida_genera_historial_inicial()
    {
        var contexto = await CreateContextAsync();
        var alumno = await CrearAlumnoAsync(contexto.InstitucionId);
        var matricula = await MatricularAsync(alumno, contexto.SeccionId, contexto.PeriodoId);

        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.matricula_estado_historial where matricula_id = $1 and estado_anterior is null and estado_nuevo = 'pendiente'",
            matricula));
    }

    [Fact]
    public async Task Segunda_matricula_del_alumno_en_mismo_ciclo_falla()
    {
        var contexto = await CreateContextAsync();
        var otraSeccion = await CrearSeccionAsync(
            contexto.InstitucionId, contexto.CicloId, contexto.GradoId, "B");
        var alumno = await CrearAlumnoAsync(contexto.InstitucionId);
        await MatricularAsync(alumno, contexto.SeccionId, contexto.PeriodoId);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            MatricularAsync(alumno, otraSeccion, contexto.PeriodoId));
        Assert.Equal("23505", exception.SqlState);
        Assert.Equal("uq_matriculas_alumno_ciclo", exception.ConstraintName);
    }

    [Fact]
    public async Task Mismo_alumno_puede_matricularse_en_ciclos_diferentes()
    {
        var contexto = await CreateContextAsync();
        var alumno = await CrearAlumnoAsync(contexto.InstitucionId);
        await MatricularAsync(alumno, contexto.SeccionId, contexto.PeriodoId);
        var ciclo2 = await InsertCicloAsync(contexto.InstitucionId);
        var periodo2 = await InsertPeriodoAsync(ciclo2);
        var seccion2 = await CrearSeccionAsync(
            contexto.InstitucionId, ciclo2, contexto.GradoId, "A");

        await MatricularAsync(alumno, seccion2, periodo2);
        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.matriculas where alumno_id = $1", alumno));
    }

    [Fact]
    public async Task Periodo_de_otro_ciclo_es_rechazado()
    {
        var contexto = await CreateContextAsync();
        var otroCiclo = await InsertCicloAsync(contexto.InstitucionId);
        var periodoIncorrecto = await InsertPeriodoAsync(otroCiclo);
        var alumno = await CrearAlumnoAsync(contexto.InstitucionId);

        await Assert.ThrowsAsync<PostgresException>(() =>
            MatricularAsync(alumno, contexto.SeccionId, periodoIncorrecto));
        Assert.Equal(0, await ScalarLongAsync(
            "select count(*) from public.matriculas where alumno_id = $1", alumno));
    }

    [Fact]
    public async Task Alumno_inactivo_seccion_inactiva_y_otra_institucion_son_rechazados()
    {
        var contexto = await CreateContextAsync();
        var alumnoInactivo = await CrearAlumnoAsync(contexto.InstitucionId);
        await ExecuteAsync("update public.alumnos set estado = 'inactivo' where id = $1", alumnoInactivo);
        await Assert.ThrowsAsync<PostgresException>(() =>
            MatricularAsync(alumnoInactivo, contexto.SeccionId, contexto.PeriodoId));

        var alumnoActivo = await CrearAlumnoAsync(contexto.InstitucionId);
        await ExecuteAsync("update public.secciones set activo = false where id = $1", contexto.SeccionId);
        await Assert.ThrowsAsync<PostgresException>(() =>
            MatricularAsync(alumnoActivo, contexto.SeccionId, contexto.PeriodoId));

        var otroContexto = await CreateContextAsync();
        await Assert.ThrowsAsync<PostgresException>(() =>
            MatricularAsync(alumnoActivo, otroContexto.SeccionId, otroContexto.PeriodoId));
    }

    [Fact]
    public async Task Entidades_academicas_usadas_no_pueden_eliminarse()
    {
        var contexto = await CreateContextAsync();
        var alumno = await CrearAlumnoAsync(contexto.InstitucionId);
        await MatricularAsync(alumno, contexto.SeccionId, contexto.PeriodoId);

        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("delete from public.secciones where id = $1", contexto.SeccionId));
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("delete from public.alumnos where id = $1", alumno));
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("delete from public.ciclos_escolares where id = $1", contexto.CicloId));
        await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync("delete from public.periodos_matricula where id = $1", contexto.PeriodoId));
    }

    [Fact]
    public async Task Cupo_uno_admite_primero_y_rechaza_segundo()
    {
        var contexto = await CreateContextAsync(cupo: 1);
        await MatricularAsync(
            await CrearAlumnoAsync(contexto.InstitucionId), contexto.SeccionId, contexto.PeriodoId);

        var segundoAlumno = await CrearAlumnoAsync(contexto.InstitucionId);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => MatricularAsync(
            segundoAlumno, contexto.SeccionId, contexto.PeriodoId));
        Assert.Equal("23514", exception.SqlState);
    }

    [Fact]
    public async Task Cupo_se_protege_con_dos_solicitudes_concurrentes()
    {
        var contexto = await CreateContextAsync(cupo: 1);
        var alumno1 = await CrearAlumnoAsync(contexto.InstitucionId);
        var alumno2 = await CrearAlumnoAsync(contexto.InstitucionId);

        var resultados = await Task.WhenAll(
            IntentarMatricularAsync(alumno1, contexto.SeccionId, contexto.PeriodoId),
            IntentarMatricularAsync(alumno2, contexto.SeccionId, contexto.PeriodoId));

        Assert.Single(resultados, resultado => resultado);
        Assert.Single(resultados, resultado => !resultado);
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.matriculas where seccion_id = $1", contexto.SeccionId));
    }

    [Fact]
    public async Task Cambio_de_estado_es_trazable_y_no_duplica_historial_si_no_cambia()
    {
        var contexto = await CreateContextAsync();
        var usuario = await InsertUsuarioAsync();
        var matricula = await MatricularAsync(
            await CrearAlumnoAsync(contexto.InstitucionId), contexto.SeccionId, contexto.PeriodoId, usuario);

        await ExecuteAsync(
            "select public.cambiar_estado_matricula($1, 'activa', $2, null)", matricula, usuario);
        await ExecuteAsync(
            "select public.cambiar_estado_matricula($1, 'activa', $2, null)", matricula, usuario);

        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.matricula_estado_historial where matricula_id = $1", matricula));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.matricula_estado_historial where matricula_id = $1 and estado_anterior = 'pendiente' and estado_nuevo = 'activa' and usuario_id = $2",
            matricula, usuario));
    }

    [Fact]
    public async Task Alumno_con_matricula_vigente_no_se_desactiva()
    {
        var contexto = await CreateContextAsync();
        var usuario = await InsertUsuarioAsync();
        var alumno = await CrearAlumnoAsync(contexto.InstitucionId);
        await MatricularAsync(alumno, contexto.SeccionId, contexto.PeriodoId, usuario);

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "select public.desactivar_alumno($1, $2, 'Solicitud')", alumno, usuario));
        Assert.Equal("activo", await ScalarStringAsync(
            "select estado from public.alumnos where id = $1", alumno));
    }

    [Fact]
    public async Task Desactivar_y_reactivar_alumno_no_modifica_matricula_historica()
    {
        var contexto = await CreateContextAsync();
        var usuario = await InsertUsuarioAsync();
        var alumno = await CrearAlumnoAsync(contexto.InstitucionId);
        var matricula = await MatricularAsync(alumno, contexto.SeccionId, contexto.PeriodoId, usuario);
        await ExecuteAsync("select public.cambiar_estado_matricula($1, 'activa', $2, null)", matricula, usuario);
        await ExecuteAsync("select public.cambiar_estado_matricula($1, 'finalizada', $2, null)", matricula, usuario);

        await ExecuteAsync("select public.desactivar_alumno($1, $2, 'Egreso')", alumno, usuario);
        await ExecuteAsync("select public.reactivar_alumno($1, $2)", alumno, usuario);

        Assert.Equal("activo", await ScalarStringAsync("select estado from public.alumnos where id = $1", alumno));
        Assert.Equal("finalizada", await ScalarStringAsync(
            "select estado from public.matriculas where id = $1", matricula));
    }

    [Fact]
    public async Task Crear_persona_y_alumno_es_atomico_si_alumno_falla()
    {
        var institucion = await InsertInstitucionAsync();
        var codigo = $"DUP-{Guid.NewGuid():N}";
        await ExecuteAsync(
            "select public.crear_alumno_nueva_persona($1, 'Primero', 'Valido', null, null, $2)",
            institucion, codigo);

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            "select public.crear_alumno_nueva_persona($1, 'NoDebe', 'Persistir', null, null, $2)",
            institucion, codigo));

        Assert.Equal(0, await ScalarLongAsync(
            "select count(*) from public.personas where nombres = 'NoDebe' and apellidos = 'Persistir'"));
    }

    private async Task<ContextoAcademico> CreateContextAsync(int? cupo = null)
    {
        var institucion = await InsertInstitucionAsync();
        var ciclo = await InsertCicloAsync(institucion);
        var grado = await InsertGradoAsync();
        var periodo = await InsertPeriodoAsync(ciclo);
        var seccion = await CrearSeccionAsync(institucion, ciclo, grado, "A", cupo);
        return new ContextoAcademico(institucion, ciclo, grado, seccion, periodo);
    }

    private Task<Guid> InsertInstitucionAsync() => ScalarGuidAsync(
        "insert into public.instituciones (nombre) values ($1) returning id",
        $"Institucion {Guid.NewGuid():N}");

    private Task<Guid> InsertCicloAsync(Guid institucionId, string? nombre = null) => ScalarGuidAsync(
        "insert into public.ciclos_escolares (institucion_id, nombre) values ($1, $2) returning id",
        institucionId, nombre ?? $"Ciclo {Guid.NewGuid():N}");

    private Task<Guid> InsertGradoAsync(string? nombre = null) => ScalarGuidAsync(
        "insert into public.grados (nombre) values ($1) returning id",
        $"{nombre ?? "Grado"} {Guid.NewGuid():N}");

    private Task<Guid> InsertPeriodoAsync(Guid cicloId) => ScalarGuidAsync(
        "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1, $2, current_date, current_date + 30) returning id",
        cicloId, $"Periodo {Guid.NewGuid():N}");

    private Task<Guid> CrearSeccionAsync(
        Guid institucionId,
        Guid cicloId,
        Guid gradoId,
        string nombre,
        int? cupo = null) => cupo.HasValue
            ? ScalarGuidAsync(
                "select public.crear_seccion($1, $2, $3, null, $4, $5)",
                institucionId, cicloId, gradoId, nombre, cupo.Value)
            : ScalarGuidAsync(
                "select public.crear_seccion($1, $2, $3, null, $4)",
                institucionId, cicloId, gradoId, nombre);

    private Task<Guid> CrearAlumnoAsync(Guid institucionId) => ScalarGuidAsync(
        "select public.crear_alumno_nueva_persona($1, $2, $3)",
        institucionId, "Alumno", Guid.NewGuid().ToString("N"));

    private Task<Guid> InsertUsuarioAsync() => ScalarGuidAsync(
        "insert into public.usuarios (auth_user_id) values ($1) returning id",
        Guid.NewGuid());

    private Task<Guid> MatricularAsync(
        Guid alumnoId,
        Guid seccionId,
        Guid periodoId,
        Guid? usuarioId = null) => usuarioId.HasValue
            ? ScalarGuidAsync(
                "select public.matricular_alumno($1, $2, $3, $4)",
                alumnoId, seccionId, periodoId, usuarioId.Value)
            : ScalarGuidAsync(
                "select public.matricular_alumno($1, $2, $3)",
                alumnoId, seccionId, periodoId);

    private async Task<bool> IntentarMatricularAsync(Guid alumnoId, Guid seccionId, Guid periodoId)
    {
        try
        {
            await MatricularAsync(alumnoId, seccionId, periodoId);
            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == "23514")
        {
            return false;
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

    private async Task<long> ScalarLongAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> ScalarStringAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            command.Parameters.AddWithValue(value);
        }
    }

    private sealed record ContextoAcademico(
        Guid InstitucionId,
        Guid CicloId,
        Guid GradoId,
        Guid SeccionId,
        Guid PeriodoId);
}
