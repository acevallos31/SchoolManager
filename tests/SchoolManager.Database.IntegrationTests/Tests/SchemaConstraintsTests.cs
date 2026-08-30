using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class SchemaConstraintsTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Persona_sin_documento_es_valida()
    {
        var id = await InsertPersonaAsync("Sin", "Documento");
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Documento_completo_es_valido_y_documento_parcial_es_rechazado()
    {
        await InsertPersonaAsync("Ana", "Documento", "pasaporte", "AB-123", "AB123");

        await Assert.ThrowsAsync<PostgresException>(() => InsertPersonaAsync(
            "Documento", "Incompleto", numero: "XYZ-1"));
    }

    [Fact]
    public async Task Documento_normalizado_duplicado_es_rechazado()
    {
        var documentoNormalizado = $"DOC-{Guid.NewGuid():N}";
        await InsertPersonaAsync("Ana", "Uno", "pasaporte", documentoNormalizado, documentoNormalizado);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => InsertPersonaAsync(
            "Ana", "Dos", "pasaporte", documentoNormalizado, documentoNormalizado));

        Assert.Equal("23505", exception.SqlState);
        Assert.Equal("ux_personas_documento_normalizado", exception.ConstraintName);
    }

    [Fact]
    public async Task Rne_duplicado_es_rechazado_y_nulos_se_permiten()
    {
        var context = await CreateContextAsync();
        await InsertAlumnoAsync(context.InstitucionId, rne: "RNE-001");

        await Assert.ThrowsAsync<PostgresException>(() => InsertAlumnoAsync(context.InstitucionId, rne: "RNE-001"));
        await InsertAlumnoAsync(context.InstitucionId);
        await InsertAlumnoAsync(context.InstitucionId);
    }

    [Fact]
    public async Task Codigo_interno_es_unico_por_institucion()
    {
        var context = await CreateContextAsync();
        await InsertAlumnoAsync(context.InstitucionId, codigoInterno: "A-01");

        await Assert.ThrowsAsync<PostgresException>(() => InsertAlumnoAsync(context.InstitucionId, codigoInterno: "A-01"));

        var otraInstitucion = await InsertInstitucionAsync();
        await InsertAlumnoAsync(otraInstitucion, codigoInterno: "A-01");
    }

    [Fact]
    public async Task Persona_no_puede_tener_dos_perfiles_de_alumno_en_una_institucion()
    {
        var context = await CreateContextAsync();
        var personaId = await InsertPersonaAsync("Mismo", "Alumno");
        await InsertAlumnoAsync(context.InstitucionId, personaId);

        await Assert.ThrowsAsync<PostgresException>(() => InsertAlumnoAsync(context.InstitucionId, personaId));
    }

    [Fact]
    public async Task Persona_no_puede_tener_dos_usuarios()
    {
        var personaId = await InsertPersonaAsync("Un", "Usuario");
        await ExecuteAsync($"insert into public.usuarios (id, persona_id) values ('{Guid.NewGuid()}', '{personaId}')");

        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            $"insert into public.usuarios (id, persona_id) values ('{Guid.NewGuid()}', '{personaId}')"));
    }

    [Fact]
    public async Task Relacion_alumno_responsable_duplicada_es_rechazada()
    {
        var context = await CreateContextAsync();
        var alumnoId = await InsertAlumnoAsync(context.InstitucionId);
        var responsableId = await InsertResponsableAsync(context.InstitucionId);
        await InsertRelacionAsync(alumnoId, responsableId);

        await Assert.ThrowsAsync<PostgresException>(() => InsertRelacionAsync(alumnoId, responsableId));
    }

    [Fact]
    public async Task Solo_un_responsable_principal_activo_es_permitido()
    {
        var context = await CreateContextAsync();
        var alumnoId = await InsertAlumnoAsync(context.InstitucionId);
        await InsertRelacionAsync(alumnoId, await InsertResponsableAsync(context.InstitucionId), principal: true);
        var segundoResponsableId = await InsertResponsableAsync(context.InstitucionId);

        await Assert.ThrowsAsync<PostgresException>(() => InsertRelacionAsync(
            alumnoId, segundoResponsableId, principal: true));
    }

    [Fact]
    public async Task Periodo_con_fechas_invertidas_es_rechazado()
    {
        var context = await CreateContextAsync();
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync($"""
            insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin)
            values ('{context.CicloId}', 'Invalido', date '2027-02-01', date '2027-01-01')
            """));
    }

    [Fact]
    public async Task Matricula_duplicada_por_alumno_y_ciclo_es_rechazada()
    {
        var context = await CreateContextAsync();
        var alumnoId = await InsertAlumnoAsync(context.InstitucionId);
        await InsertMatriculaAsync(context, alumnoId);

        await Assert.ThrowsAsync<PostgresException>(() => InsertMatriculaAsync(context, alumnoId));
    }

    [Fact]
    public async Task Anulacion_sin_motivo_es_rechazada()
    {
        var context = await CreateContextAsync();
        var alumnoId = await InsertAlumnoAsync(context.InstitucionId);
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync($"""
            insert into public.matriculas (alumno_id, ciclo_id, grado_id, seccion_id, fecha_anulacion)
            values ('{alumnoId}', '{context.CicloId}', '{context.GradoId}', '{context.SeccionId}', now())
            """));
    }

    [Fact]
    public async Task Foreign_keys_principales_impiden_referencias_inexistentes()
    {
        await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync($"""
            insert into public.responsables (persona_id, institucion_id)
            values ('{Guid.NewGuid()}', '{Guid.NewGuid()}')
            """));
    }

    [Fact]
    public async Task Primary_keys_uuid_se_generan_automaticamente()
    {
        var id = await InsertPersonaAsync("UUID", "Generado");
        Assert.NotEqual(Guid.Empty, id);
    }

    private async Task<Guid> InsertInstitucionAsync()
    {
        return await ScalarGuidAsync($"insert into public.instituciones (nombre) values ('Institucion {Guid.NewGuid()}') returning id");
    }

    private async Task<Guid> InsertPersonaAsync(string nombres, string apellidos, string? tipo = null, string? numero = null, string? normalizado = null)
    {
        var sql = $"insert into public.personas (nombres, apellidos, tipo_identificacion, numero_identificacion, numero_identificacion_normalizado) values ('{nombres}', '{apellidos}', {Literal(tipo)}, {Literal(numero)}, {Literal(normalizado)}) returning id";
        return await ScalarGuidAsync(sql);
    }

    private async Task<Guid> InsertAlumnoAsync(Guid institucionId, Guid? personaId = null, string? rne = null, string? codigoInterno = null)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync($"insert into public.alumnos (id, nombres, apellidos, dni, institucion_id, persona_id, rne, codigo_interno) values ('{id}', 'Alumno', 'Prueba', '{Guid.NewGuid()}', '{institucionId}', {Literal(personaId)}, {Literal(rne)}, {Literal(codigoInterno)})");
        return id;
    }

    private async Task<Guid> InsertResponsableAsync(Guid institucionId)
    {
        var personaId = await InsertPersonaAsync("Responsable", Guid.NewGuid().ToString());
        return await ScalarGuidAsync($"insert into public.responsables (persona_id, institucion_id) values ('{personaId}', '{institucionId}') returning id");
    }

    private Task InsertRelacionAsync(Guid alumnoId, Guid responsableId, bool principal = false) => ExecuteAsync(
        $"insert into public.alumno_responsable (alumno_id, responsable_id, es_principal) values ('{alumnoId}', '{responsableId}', {principal.ToString().ToLowerInvariant()})");

    private async Task<(Guid InstitucionId, Guid CicloId, Guid GradoId, Guid SeccionId)> CreateContextAsync()
    {
        var institucionId = await InsertInstitucionAsync();
        var cicloId = await ScalarGuidAsync($"insert into public.ciclos_escolares (nombre, institucion_id) values ('Ciclo {Guid.NewGuid()}', '{institucionId}') returning id");
        var gradoId = await ScalarGuidAsync($"insert into public.grados (nombre) values ('Grado {Guid.NewGuid()}') returning id");
        var seccionId = await ScalarGuidAsync($"insert into public.secciones (nombre, grado_id) values ('Seccion {Guid.NewGuid()}', '{gradoId}') returning id");
        return (institucionId, cicloId, gradoId, seccionId);
    }

    private async Task InsertMatriculaAsync((Guid InstitucionId, Guid CicloId, Guid GradoId, Guid SeccionId) context, Guid alumnoId)
    {
        await ExecuteAsync($"insert into public.matriculas (alumno_id, ciclo_id, grado_id, seccion_id) values ('{alumnoId}', '{context.CicloId}', '{context.GradoId}', '{context.SeccionId}')");
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid> ScalarGuidAsync(string sql)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static string Literal(Guid? value) => value.HasValue ? $"'{value.Value}'" : "null";
    private static string Literal(string? value) => value is null ? "null" : $"'{value}'";
}
