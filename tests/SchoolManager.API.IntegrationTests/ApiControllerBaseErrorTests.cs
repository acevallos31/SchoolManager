using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SchoolManager.API.Controllers;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class ApiControllerBaseErrorTests
{
    [Fact]
    public void ConstraintConocida_ConservaMensajeDeNegocio()
    {
        var result = ExposedApiController.Map(Error(
            "23505",
            "duplicate key value violates unique constraint",
            "uq_matriculas_alumno_ciclo"));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "El alumno ya tiene una matrícula vigente en este ciclo escolar.",
            Mensaje(result));
    }

    [Fact]
    public void ConstraintDesconocida_NoExponeMensajeTecnico()
    {
        const string tecnico = "duplicate key value violates unique constraint \"ux_regla_interna\"";
        var result = ExposedApiController.Map(Error("23505", tecnico, "ux_regla_interna"));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(
            "Ya existe un registro que entra en conflicto con los datos ingresados.",
            Mensaje(result));
        Assert.DoesNotContain("ux_regla_interna", Mensaje(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("42501", 403, "No tienes permiso para realizar esta operación.")]
    [InlineData("P0002", 404, "No se encontró el recurso solicitado.")]
    [InlineData("23514", 409, "Los datos ingresados no cumplen una regla de negocio.")]
    [InlineData("22023", 400, "Los datos enviados no son válidos para esta operación.")]
    [InlineData("23503", 400, "La operación hace referencia a datos inexistentes o que no están disponibles.")]
    [InlineData("SM001", 400, "Falta la configuración requerida para completar la operación.")]
    [InlineData("SM002", 400, "La configuración institucional actual es inconsistente.")]
    [InlineData("SM003", 400, "Debes seleccionar una institución válida para continuar.")]
    [InlineData("SM004", 400, "La operación no es válida para la configuración institucional actual.")]
    public void SqlStateConocido_UsaMensajeSeguro(string sqlState, int status, string esperado)
    {
        var result = ExposedApiController.Map(Error(sqlState, "detalle técnico de PostgreSQL"));

        Assert.Equal(status, result.StatusCode);
        Assert.Equal(esperado, Mensaje(result));
        Assert.DoesNotContain("detalle técnico", Mensaje(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RaiseExceptionControlado_ConservaMensajeDeNegocioRpc()
    {
        const string negocio = "La matrícula no permite esta transición de estado.";
        var result = ExposedApiController.Map(Error("P0001", negocio));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(negocio, Mensaje(result));
    }

    [Fact]
    public void SqlStateDesconocido_UsaFallbackSeguro()
    {
        const string tecnico = "relation \"tabla_interna\" does not exist";
        var result = ExposedApiController.Map(Error("42P01", tecnico));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("No se pudo completar la operación solicitada.", Mensaje(result));
        Assert.DoesNotContain("tabla_interna", Mensaje(result), StringComparison.Ordinal);
    }

    private static PostgresException Error(string sqlState, string message, string? constraint = null) =>
        new(message, "ERROR", "ERROR", sqlState, constraintName: constraint);

    private static string Mensaje(ObjectResult result)
    {
        var json = JsonSerializer.SerializeToElement(result.Value);
        return json.GetProperty("error").GetString()!;
    }

    private sealed class ExposedApiController(NpgsqlDataSource dataSource) : ApiControllerBase(dataSource)
    {
        public static ObjectResult Map(PostgresException ex) => ToError(ex);
    }
}
