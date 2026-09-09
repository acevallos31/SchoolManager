using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class MatriculaRematriculaTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Matricula_anulada_libera_alumno_y_ciclo_para_nueva_matricula()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var ciclo = await ScalarGuidAsync(
            "insert into public.ciclos_escolares (institucion_id, nombre) values ($1, $2) returning id",
            institucion, $"Ciclo {Guid.NewGuid():N}");
        var grado = await ScalarGuidAsync(
            "insert into public.grados (institucion_id, nombre) values ($1, $2) returning id",
            institucion, $"Grado {Guid.NewGuid():N}");
        var periodo = await ScalarGuidAsync(
            "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1, $2, current_date, current_date + 30) returning id",
            ciclo, $"Periodo {Guid.NewGuid():N}");
        var seccionA = await ScalarGuidAsync(
            "select public.crear_seccion($1, $2, $3, null, 'A')",
            institucion, ciclo, grado);
        var seccionB = await ScalarGuidAsync(
            "select public.crear_seccion($1, $2, $3, null, 'B')",
            institucion, ciclo, grado);
        var alumno = await ScalarGuidAsync(
            "select public.crear_alumno_nueva_persona($1, $2, $3)",
            institucion, "Alumno", Guid.NewGuid().ToString("N"));
        var usuario = await ScalarGuidAsync(
            "insert into public.usuarios (auth_user_id) values ($1) returning id",
            Guid.NewGuid());

        var primera = await ScalarGuidAsync(
            "select public.matricular_alumno($1, $2, $3, $4)",
            alumno, seccionA, periodo, usuario);

        await ExecuteAsync(
            "select public.cambiar_estado_matricula($1, 'anulada', $2, 'Registro anulado para corregir datos')",
            primera, usuario);

        var segunda = await ScalarGuidAsync(
            "select public.matricular_alumno($1, $2, $3, $4)",
            alumno, seccionB, periodo, usuario);

        Assert.NotEqual(primera, segunda);
        Assert.Equal(2, await ScalarLongAsync(
            "select count(*) from public.matriculas where alumno_id = $1 and ciclo_id = $2",
            alumno, ciclo));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.matriculas where alumno_id = $1 and ciclo_id = $2 and estado = 'anulada'",
            alumno, ciclo));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.matriculas where alumno_id = $1 and ciclo_id = $2 and estado = 'pendiente'",
            alumno, ciclo));
    }

    [Fact]
    public async Task Dos_matriculas_no_anuladas_del_mismo_alumno_y_ciclo_siguen_rechazadas()
    {
        var institucion = await ScalarGuidAsync(
            "insert into public.instituciones (nombre) values ($1) returning id",
            $"Institucion {Guid.NewGuid():N}");
        var ciclo = await ScalarGuidAsync(
            "insert into public.ciclos_escolares (institucion_id, nombre) values ($1, $2) returning id",
            institucion, $"Ciclo {Guid.NewGuid():N}");
        var grado = await ScalarGuidAsync(
            "insert into public.grados (institucion_id, nombre) values ($1, $2) returning id",
            institucion, $"Grado {Guid.NewGuid():N}");
        var periodo = await ScalarGuidAsync(
            "insert into public.periodos_matricula (ciclo_id, nombre, fecha_inicio, fecha_fin) values ($1, $2, current_date, current_date + 30) returning id",
            ciclo, $"Periodo {Guid.NewGuid():N}");
        var seccionA = await ScalarGuidAsync("select public.crear_seccion($1, $2, $3, null, 'A')", institucion, ciclo, grado);
        var seccionB = await ScalarGuidAsync("select public.crear_seccion($1, $2, $3, null, 'B')", institucion, ciclo, grado);
        var alumno = await ScalarGuidAsync(
            "select public.crear_alumno_nueva_persona($1, $2, $3)",
            institucion, "Alumno", Guid.NewGuid().ToString("N"));

        await ScalarGuidAsync("select public.matricular_alumno($1, $2, $3)", alumno, seccionA, periodo);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            ScalarGuidAsync("select public.matricular_alumno($1, $2, $3)", alumno, seccionB, periodo));

        Assert.Equal("23505", ex.SqlState);
        Assert.Equal("uq_matriculas_alumno_ciclo", ex.ConstraintName);
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
}
