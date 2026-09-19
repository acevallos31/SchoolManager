using System.Text.Json;
using Npgsql;
using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class DemoSandboxClonacionTests(PostgreSqlFixture fixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task Dos_visitantes_clonan_el_mismo_template_sin_compartir_datos_mutables()
    {
        var seed = await CrearTemplateCompletoAsync();
        var authA = Guid.NewGuid();
        var authB = Guid.NewGuid();

        var a = Parse(await ScalarTextAsync(
            "select public.rpc_crear_sandbox_demo($1,$2)::text", authA, seed.TemplateId));
        var b = Parse(await ScalarTextAsync(
            "select public.rpc_crear_sandbox_demo($1,$2)::text", authB, seed.TemplateId));

        Assert.False(a.Reused);
        Assert.False(b.Reused);
        Assert.NotEqual(a.InstitucionId, b.InstitucionId);
        Assert.NotEqual(a.SessionId, b.SessionId);

        Assert.Equal("demo_sandbox", await ScalarTextAsync(
            "select tipo from public.instituciones where id=$1", a.InstitucionId));
        Assert.Equal("demo_sandbox", await ScalarTextAsync(
            "select tipo from public.instituciones where id=$1", b.InstitucionId));

        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.alumnos where institucion_id=$1", a.InstitucionId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.responsables where institucion_id=$1", a.InstitucionId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.matriculas where institucion_id=$1", a.InstitucionId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.cargos where institucion_id=$1", a.InstitucionId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.pagos where institucion_id=$1", a.InstitucionId));
        Assert.Equal(1, await ScalarLongAsync(
            "select count(*) from public.pagos_aplicaciones where institucion_id=$1", a.InstitucionId));

        var rneA = await ScalarTextAsync(
            "select rne from public.alumnos where institucion_id=$1", a.InstitucionId);
        var rneB = await ScalarTextAsync(
            "select rne from public.alumnos where institucion_id=$1", b.InstitucionId);
        Assert.StartsWith("DEMO-", rneA);
        Assert.StartsWith("DEMO-", rneB);
        Assert.NotEqual(seed.TemplateRne, rneA);
        Assert.NotEqual(rneA, rneB);

        var documentoA = await ScalarTextAsync("""
            select p.numero_identificacion_normalizado
            from public.alumnos a
            join public.personas p on p.id=a.persona_id
            where a.institucion_id=$1
            """, a.InstitucionId);
        var documentoB = await ScalarTextAsync("""
            select p.numero_identificacion_normalizado
            from public.alumnos a
            join public.personas p on p.id=a.persona_id
            where a.institucion_id=$1
            """, b.InstitucionId);
        Assert.NotEqual(documentoA, documentoB);

        var reciboA = await ScalarLongAsync(
            "select numero_recibo from public.pagos where institucion_id=$1", a.InstitucionId);
        var reciboB = await ScalarLongAsync(
            "select numero_recibo from public.pagos where institucion_id=$1", b.InstitucionId);
        Assert.NotEqual(reciboA, reciboB);

        var usuarioA = await ScalarGuidAsync(
            "select id from public.usuarios where auth_user_id=$1", authA);
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*)
            from public.usuarios_roles ur
            join public.roles r on r.id=ur.rol_id
            where ur.usuario_id=$1
              and ur.institucion_id=$2
              and ur.activo
              and r.codigo='demo_operator'
              and r.tipo='institucional'
            """, usuarioA, a.InstitucionId));
        Assert.Equal(0, await ScalarLongAsync("""
            select count(*)
            from public.usuarios_roles
            where usuario_id=$1 and institucion_id=$2 and activo
            """, usuarioA, b.InstitucionId));
    }

    [Fact]
    public async Task Repetir_create_reutiliza_sesion_y_reset_entrega_sandbox_limpia()
    {
        var seed = await CrearTemplateCompletoAsync();
        var auth = Guid.NewGuid();

        var primera = Parse(await ScalarTextAsync(
            "select public.rpc_crear_sandbox_demo($1,$2)::text", auth, seed.TemplateId));
        var segunda = Parse(await ScalarTextAsync(
            "select public.rpc_crear_sandbox_demo($1,$2)::text", auth, seed.TemplateId));

        Assert.False(primera.Reused);
        Assert.True(segunda.Reused);
        Assert.Equal(primera.SessionId, segunda.SessionId);
        Assert.Equal(primera.InstitucionId, segunda.InstitucionId);

        await ExecuteAsync("""
            update public.alumnos
            set codigo_interno='CAMBIO-DEMO'
            where institucion_id=$1
            """, primera.InstitucionId);

        var reiniciada = Parse(await ScalarTextAsync(
            "select public.rpc_reset_sandbox_demo($1,$2)::text", auth, seed.TemplateId));

        Assert.False(reiniciada.Reused);
        Assert.NotEqual(primera.SessionId, reiniciada.SessionId);
        Assert.NotEqual(primera.InstitucionId, reiniciada.InstitucionId);

        Assert.Equal("reiniciada", await ScalarTextAsync(
            "select estado from public.demo_sessions where id=$1", primera.SessionId));
        Assert.False(await ScalarBoolAsync(
            "select activo from public.instituciones where id=$1", primera.InstitucionId));
        Assert.Equal("ALU-01", await ScalarTextAsync(
            "select codigo_interno from public.alumnos where institucion_id=$1",
            reiniciada.InstitucionId));

        var usuario = await ScalarGuidAsync(
            "select id from public.usuarios where auth_user_id=$1", auth);
        Assert.Equal(0, await ScalarLongAsync("""
            select count(*) from public.usuarios_roles
            where usuario_id=$1 and institucion_id=$2 and activo
            """, usuario, primera.InstitucionId));
        Assert.Equal(1, await ScalarLongAsync("""
            select count(*) from public.usuarios_roles
            where usuario_id=$1 and institucion_id=$2 and activo
            """, usuario, reiniciada.InstitucionId));
    }

    [Fact]
    public async Task Rpcs_demo_no_son_ejecutables_directamente_por_authenticated()
    {
        var seed = await CrearTemplateCompletoAsync();
        var auth = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            AuthenticatedScalarTextAsync(
                "select public.rpc_crear_sandbox_demo($1,$2)::text",
                auth, seed.TemplateId));

        Assert.Equal("42501", ex.SqlState);
    }

    private async Task<TemplateSeed> CrearTemplateCompletoAsync()
    {
        var template = await ScalarGuidAsync("""
            insert into public.instituciones(nombre,nombre_corto,tipo)
            values($1,'DT','demo_template') returning id
            """, $"Demo Template {Guid.NewGuid():N}");

        await ExecuteAsync("""
            insert into public.configuracion_identificadores(
              institucion_id,rne_requerido,identificacion_civil_requerida,
              codigo_interno_requerido,tipos_identificacion_permitidos
            ) values($1,true,true,true,array['DNI'])
            """, template);

        var ciclo = await ScalarGuidAsync("""
            insert into public.ciclos_escolares(
              institucion_id,nombre,fecha_inicio,fecha_fin,activo
            ) values($1,'2026','2026-01-01','2026-12-31',true)
            returning id
            """, template);
        var periodo = await ScalarGuidAsync("""
            insert into public.periodos_matricula(
              ciclo_id,nombre,fecha_inicio,fecha_fin,activo
            ) values($1,'Ordinario','2026-01-01','2026-12-31',true)
            returning id
            """, ciclo);
        var grado = await ScalarGuidAsync("""
            insert into public.grados(nombre,orden,activo,institucion_id)
            values('Séptimo',7,true,$1) returning id
            """, template);
        var jornada = await ScalarGuidAsync("""
            insert into public.jornadas(nombre,activo,institucion_id)
            values('Matutina',true,$1) returning id
            """, template);
        var seccion = await ScalarGuidAsync("""
            insert into public.secciones(
              grado_id,jornada_id,nombre,cupo,activo,institucion_id,ciclo_id
            ) values($1,$2,'A',30,true,$3,$4)
            returning id
            """, grado, jornada, template, ciclo);

        var docAlumno = $"DOC-{Guid.NewGuid():N}";
        var personaAlumno = await ScalarGuidAsync("""
            insert into public.personas(
              nombres,apellidos,tipo_identificacion,numero_identificacion,
              numero_identificacion_normalizado,pais_emisor,correo
            ) values('Ana','Demo','DNI',$1,$1,'HN',$2)
            returning id
            """, docAlumno, $"ana.{Guid.NewGuid():N}@example.invalid");
        var templateRne = $"RNE-{Guid.NewGuid():N}";
        var alumno = await ScalarGuidAsync("""
            insert into public.alumnos(
              persona_id,institucion_id,rne,codigo_interno,fecha_nacimiento
            ) values($1,$2,$3,'ALU-01','2012-01-01')
            returning id
            """, personaAlumno, template, templateRne);

        var docResponsable = $"DOC-{Guid.NewGuid():N}";
        var personaResponsable = await ScalarGuidAsync("""
            insert into public.personas(
              nombres,apellidos,tipo_identificacion,numero_identificacion,
              numero_identificacion_normalizado,pais_emisor,correo
            ) values('Rosa','Demo','DNI',$1,$1,'HN',$2)
            returning id
            """, docResponsable, $"rosa.{Guid.NewGuid():N}@example.invalid");
        var responsable = await ScalarGuidAsync("""
            insert into public.responsables(persona_id,institucion_id)
            values($1,$2) returning id
            """, personaResponsable, template);
        await ExecuteAsync("""
            insert into public.alumno_responsable(
              alumno_id,responsable_id,parentesco,es_principal,acceso_financiero
            ) values($1,$2,'Madre',true,true)
            """, alumno, responsable);

        var concepto = await ScalarGuidAsync("""
            insert into public.conceptos_financieros(
              institucion_id,nombre,descripcion,monto
            ) values($1,'Mensualidad','Mensualidad Demo',100)
            returning id
            """, template);
        var plan = await ScalarGuidAsync("""
            insert into public.planes_pago(institucion_id,nombre,descripcion)
            values($1,'Plan Demo','Plan para sandbox') returning id
            """, template);
        await ExecuteAsync("""
            insert into public.plan_cuotas(
              plan_id,orden,concepto_id,descripcion,monto,vencimiento_dias
            ) values($1,1,$2,'Cuota Demo',100,0)
            """, plan, concepto);

        var personaOperador = await ScalarGuidAsync("""
            insert into public.personas(nombres,apellidos)
            values('Operador','Template') returning id
            """);
        var operador = await ScalarGuidAsync("""
            insert into public.usuarios(persona_id,activo)
            values($1,true) returning id
            """, personaOperador);

        var matricula = await ScalarGuidAsync("""
            insert into public.matriculas(
              alumno_id,ciclo_id,seccion_id,periodo_matricula_id,
              registrado_por,fecha_matricula,estado,institucion_id,plan_pago_id
            ) values($1,$2,$3,$4,$5,'2026-01-15','activa',$6,$7)
            returning id
            """, alumno, ciclo, seccion, periodo, operador, template, plan);

        var cargo = await ScalarGuidAsync("""
            insert into public.cargos(
              institucion_id,matricula_id,alumno_id,plan_pago_id,concepto_id,
              orden,concepto_nombre,descripcion,monto_original,
              fecha_vencimiento,estado
            ) values($1,$2,$3,$4,$5,1,'Mensualidad','Cargo Demo',100,'2026-02-01','parcial')
            returning id
            """, template, matricula, alumno, plan, concepto);

        var pago = await ScalarGuidAsync("""
            insert into public.pagos(
              institucion_id,alumno_id,responsable_id,monto_total,
              fecha_pago,metodo_pago,referencia_externa,estado,registrado_por
            ) values($1,$2,$3,40,'2026-01-20 12:00:00+00','efectivo',$4,'registrado',$5)
            returning id
            """, template, alumno, responsable, $"REF-{Guid.NewGuid():N}", operador);

        await ExecuteAsync("""
            insert into public.pagos_aplicaciones(
              pago_id,cargo_id,institucion_id,monto_aplicado,estado
            ) values($1,$2,$3,40,'vigente')
            """, pago, cargo, template);

        return new TemplateSeed(template, templateRne);
    }

    private static DemoResult Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new DemoResult(
            root.GetProperty("sessionId").GetGuid(),
            root.GetProperty("institucionId").GetGuid(),
            root.GetProperty("reused").GetBoolean()
        );
    }

    private async Task<string> AuthenticatedScalarTextAsync(
        string sql, params object[] values)
    {
        await using var connection = await fixture.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using (var role = new NpgsqlCommand(
                "set local role authenticated", connection, transaction))
            {
                await role.ExecuteNonQueryAsync();
            }
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            AddParameters(command, values);
            var result = (string)(await command.ExecuteScalarAsync())!;
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
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
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task<string> ScalarTextAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> ScalarBoolAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        AddParameters(command, values);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static void AddParameters(NpgsqlCommand command, IEnumerable<object> values)
    {
        foreach (var value in values) command.Parameters.AddWithValue(value);
    }

    private sealed record TemplateSeed(Guid TemplateId, string TemplateRne);
    private sealed record DemoResult(Guid SessionId, Guid InstitucionId, bool Reused);
}
