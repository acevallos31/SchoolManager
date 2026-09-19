using Npgsql;
using SchoolManager.API.Services;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class DemoSandboxServiceIntegrationTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Servicio_real_crea_reutiliza_y_reinicia_sandbox()
    {
        var templateId = await CrearTemplateMinimoAsync();
        var authUserId = Guid.NewGuid();
        var service = new DemoSandboxService(fixture.DataSource);

        var primera = await service.CrearOReutilizarAsync(authUserId, templateId);
        var reutilizada = await service.CrearOReutilizarAsync(authUserId, templateId);
        var reiniciada = await service.ReiniciarAsync(authUserId, templateId);

        Assert.False(primera.Reused);
        Assert.True(reutilizada.Reused);
        Assert.Equal(primera.SessionId, reutilizada.SessionId);
        Assert.Equal(primera.InstitucionId, reutilizada.InstitucionId);

        Assert.False(reiniciada.Reused);
        Assert.NotEqual(primera.SessionId, reiniciada.SessionId);
        Assert.NotEqual(primera.InstitucionId, reiniciada.InstitucionId);

        Assert.Equal("demo_sandbox", await ScalarTextAsync(
            "select tipo from public.instituciones where id=$1",
            reiniciada.InstitucionId));
    }

    private async Task<Guid> CrearTemplateMinimoAsync()
    {
        await using var command = fixture.DataSource.CreateCommand("""
            insert into public.instituciones(nombre,nombre_corto,tipo,activo)
            values($1,'DT','demo_template',true)
            returning id
            """);
        command.Parameters.AddWithValue($"Demo Service {Guid.NewGuid():N}");
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private async Task<string> ScalarTextAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
