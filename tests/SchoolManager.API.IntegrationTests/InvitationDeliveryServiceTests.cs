using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolManager.API.Identity;
using SchoolManager.API.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.API.IntegrationTests;

public sealed class InvitationDeliveryServiceTests : IClassFixture<MatriculasApiFactory>
{
    private readonly MatriculasApiFactory _factory;

    public InvitationDeliveryServiceTests(MatriculasApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Envio_exitoso_confirma_proveedor_y_hash_corresponde_al_token_en_memoria()
    {
        var invitacionId = await PrepararInvitacionAsync();
        var sender = new FakeSender();
        var service = CrearServicio(sender);

        var result = await service.SendAsync(
            invitacionId,
            MatriculasApiFactory.AdminA.ToString(),
            CancellationToken.None);

        Assert.Equal("enviada", result.Estado);
        Assert.Equal(1, result.EmissionVersion);
        Assert.NotNull(sender.LastMessage);
        Assert.Equal(invitacionId, sender.LastMessage!.InvitationId);
        Assert.DoesNotContain("token=", JsonSerializer.Serialize(result));

        var uri = new Uri(sender.LastMessage.AcceptanceUrl);
        Assert.Equal(string.Empty, uri.Query);
        Assert.StartsWith("#token=", uri.Fragment, StringComparison.Ordinal);
        var token = Uri.UnescapeDataString(uri.Fragment["#token=".Length..]);
        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        await using var command = _factory.DatosAcademicos.CreateCommand("""
            select token_hash, estado, proveedor_envio, proveedor_mensaje_id,
                   emision_version, enviado_at is not null
            from public.invitaciones_acceso where id=$1
            """);
        command.Parameters.AddWithValue(invitacionId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(expectedHash, reader.GetString(0));
        Assert.Equal("enviada", reader.GetString(1));
        Assert.Equal("fake", reader.GetString(2));
        Assert.Equal("message-1", reader.GetString(3));
        Assert.Equal(1L, reader.GetInt64(4));
        Assert.True(reader.GetBoolean(5));
    }

    [Fact]
    public async Task Endpoint_aceptar_deriva_identidad_del_JWT_y_deja_solicitud_pendiente()
    {
        var invitacionId = await PrepararInvitacionAsync();
        var sender = new FakeSender();
        var service = CrearServicio(sender);

        await service.SendAsync(
            invitacionId,
            MatriculasApiFactory.AdminA.ToString(),
            CancellationToken.None);

        var uri = new Uri(sender.LastMessage!.AcceptanceUrl);
        var token = Uri.UnescapeDataString(uri.Fragment["#token=".Length..]);
        var authUserId = Guid.NewGuid();
        using var client = _factory.CrearCliente(authUserId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/invitaciones/aceptar",
            new { token });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("aceptada", body.GetProperty("estado").GetString());

        await using var command = _factory.DatosAcademicos.CreateCommand("""
            select auth_user_id_solicitado, estado
            from public.invitaciones_acceso
            where id=$1
            """);
        command.Parameters.AddWithValue(invitacionId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(authUserId, reader.GetGuid(0));
        Assert.Equal("aceptada", reader.GetString(1));
    }

    [Fact]
    public async Task Aceptacion_rechaza_sub_invalido_antes_de_consultar_DB()
    {
        var service = new InvitationAcceptanceService(_factory.DatosAcademicos);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AcceptAsync(
                new string('a', 64),
                "sub-no-es-uuid",
                CancellationToken.None));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(513)]
    public async Task Aceptacion_rechaza_token_fuera_de_longitud_permitida(int longitud)
    {
        var service = new InvitationAcceptanceService(_factory.DatosAcademicos);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AcceptAsync(
                new string('a', longitud),
                Guid.NewGuid().ToString(),
                CancellationToken.None));
    }

    [Fact]
    public async Task Endpoint_aceptar_devuelve400_para_token_invalido()
    {
        using var client = _factory.CrearCliente(Guid.NewGuid().ToString());

        var response = await client.PostAsJsonAsync(
            "/api/invitaciones/aceptar",
            new { token = "corto" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("token de invitacion", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Endpoint_aceptar_mapea_invitacion_inexistente_sin_exponer_SQL()
    {
        using var client = _factory.CrearCliente(Guid.NewGuid().ToString());
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "A", StringComparison.Ordinal)
            .Replace("/", "B", StringComparison.Ordinal);

        var response = await client.PostAsJsonAsync(
            "/api/invitaciones/aceptar",
            new { token });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("select ", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Fallo_del_proveedor_persiste_error_y_elimina_hash_no_entregado()
    {
        var invitacionId = await PrepararInvitacionAsync();
        var sender = new FakeSender { FailureCode = "fake_unavailable" };
        var service = CrearServicio(sender);

        var ex = await Assert.ThrowsAsync<InvitationEmailDeliveryException>(() => service.SendAsync(
            invitacionId,
            MatriculasApiFactory.AdminA.ToString(),
            CancellationToken.None));
        Assert.Equal("fake_unavailable", ex.Code);

        await using var command = _factory.DatosAcademicos.CreateCommand("""
            select estado, token_hash is null, expira_at is null,
                   ultimo_error_envio, intentos_envio, emision_version
            from public.invitaciones_acceso where id=$1
            """);
        command.Parameters.AddWithValue(invitacionId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("pendiente", reader.GetString(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.Equal("fake_unavailable", reader.GetString(3));
        Assert.Equal(1, reader.GetInt32(4));
        Assert.Equal(1L, reader.GetInt64(5));
    }

    [Fact]
    public async Task Endpoint_no_muta_DB_si_correo_no_esta_configurado()
    {
        var client = _factory.CrearCliente(MatriculasApiFactory.AdminA.ToString());
        var response = await client.PostAsJsonAsync(
            $"/api/invitaciones/{Guid.NewGuid()}/enviar",
            new { });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("no está configurado", body);
    }

    private InvitationDeliveryService CrearServicio(IInvitationEmailSender sender) =>
        new(
            _factory.DatosAcademicos,
            sender,
            Options.Create(new InvitationEmailOptions
            {
                Provider = "fake",
                From = "SchoolManager <acceso@schoolmanager.test>",
                FrontendBaseUrl = "https://schoolmanager.test",
                TtlHours = 24
            }),
            NullLogger<InvitationDeliveryService>.Instance);

    private async Task<Guid> PrepararInvitacionAsync()
    {
        var roleId = await ScalarGuidAsync("""
            insert into public.roles(codigo,nombre,tipo,institucion_id,activo)
            values($1,'Portal prueba','institucional',$2,true)
            returning id
            """, $"portal_{Guid.NewGuid():N}", _factory.InstitucionA);

        var client = _factory.CrearCliente(MatriculasApiFactory.AdminA.ToString());
        var response = await client.PostAsJsonAsync(
            "/api/configuracion/seguridad/usuarios/invitaciones/preparar",
            new
            {
                institucionId = _factory.InstitucionA,
                nombres = "Invitado",
                apellidos = "Prueba",
                correo = $"invite.{Guid.NewGuid():N}@schoolmanager.test",
                rolId = roleId,
                origen = "administracion"
            });
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("invitacionId").GetGuid();
    }

    private async Task<Guid> ScalarGuidAsync(string sql, params object[] values)
    {
        await using var command = _factory.DatosAcademicos.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private sealed class FakeSender : IInvitationEmailSender
    {
        public bool IsConfigured => true;
        public string? FailureCode { get; init; }
        public InvitationEmailMessage? LastMessage { get; private set; }

        public Task<InvitationEmailSendResult> SendAsync(
            InvitationEmailMessage message,
            CancellationToken ct)
        {
            LastMessage = message;
            if (FailureCode is not null)
                throw new InvitationEmailDeliveryException(FailureCode);
            return Task.FromResult(new InvitationEmailSendResult("fake", "message-1"));
        }
    }
}
