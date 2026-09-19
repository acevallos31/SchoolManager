using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolManager.API.Controllers;
using SchoolManager.API.Services;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class DemoControllerBranchTests
{
    private static readonly Guid TemplateId = Guid.Parse("70000000-0000-0000-0000-000000000007");
    private static readonly Guid AuthUserId = Guid.Parse("80000000-0000-0000-0000-000000000008");

    [Fact]
    public async Task Demo_habilitada_sin_template_devuelve_503()
    {
        var controller = CrearController(
            new DemoOptions { Enabled = true, TemplateInstitutionId = null },
            new ServicioControlado());

        var result = await controller.CrearOReutilizar(default);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
    }

    [Fact]
    public async Task Demo_habilitada_con_template_vacio_devuelve_503()
    {
        var controller = CrearController(
            new DemoOptions { Enabled = true, TemplateInstitutionId = Guid.Empty },
            new ServicioControlado());

        var result = await controller.CrearOReutilizar(default);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
    }

    [Fact]
    public async Task Sub_invalido_devuelve_401()
    {
        var controller = CrearController(
            new DemoOptions { Enabled = true, TemplateInstitutionId = TemplateId },
            new ServicioControlado(),
            sub: "no-es-guid");

        var result = await controller.CrearOReutilizar(default);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Template_no_disponible_devuelve_503_seguro()
    {
        var controller = CrearController(
            new DemoOptions { Enabled = true, TemplateInstitutionId = TemplateId },
            new ServicioControlado(new PostgresException(
                "detalle interno", "ERROR", "ERROR", "P0002")));

        var result = await controller.CrearOReutilizar(default);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, error.StatusCode);
        Assert.DoesNotContain("detalle interno", System.Text.Json.JsonSerializer.Serialize(error.Value));
    }

    [Theory]
    [InlineData("22023")]
    [InlineData("23514")]
    [InlineData("42501")]
    [InlineData("SM001")]
    public async Task Rechazo_de_negocio_devuelve_409_seguro(string sqlState)
    {
        var controller = CrearController(
            new DemoOptions { Enabled = true, TemplateInstitutionId = TemplateId },
            new ServicioControlado(new PostgresException(
                "detalle interno", "ERROR", "ERROR", sqlState)));

        var result = await controller.Reiniciar(default);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, error.StatusCode);
        Assert.DoesNotContain("detalle interno", System.Text.Json.JsonSerializer.Serialize(error.Value));
    }

    private static DemoController CrearController(
        DemoOptions options,
        IDemoSandboxService service,
        string? sub = null)
    {
        var controller = new DemoController(
            service,
            Options.Create(options),
            NullLogger<DemoController>.Instance);

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", sub ?? AuthUserId.ToString()),
                new Claim("is_anonymous", "true")
            ],
            "test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    private sealed class ServicioControlado(PostgresException? error = null)
        : IDemoSandboxService
    {
        public Task<DemoSessionResult> CrearOReutilizarAsync(
            Guid authUserId,
            Guid templateInstitutionId,
            CancellationToken cancellationToken = default)
            => Ejecutar(authUserId, templateInstitutionId);

        public Task<DemoSessionResult> ReiniciarAsync(
            Guid authUserId,
            Guid templateInstitutionId,
            CancellationToken cancellationToken = default)
            => Ejecutar(authUserId, templateInstitutionId);

        private Task<DemoSessionResult> Ejecutar(Guid authUserId, Guid templateInstitutionId)
        {
            if (error is not null) throw error;
            return Task.FromResult(new DemoSessionResult(
                Guid.NewGuid(),
                Guid.NewGuid(),
                false));
        }
    }
}
