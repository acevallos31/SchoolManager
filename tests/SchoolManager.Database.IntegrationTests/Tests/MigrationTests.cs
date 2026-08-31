using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class MigrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public void Migraciones_activas_estan_ordenadas_de_001_a_015()
    {
        var names = MigrationRunner.GetActiveMigrationPaths().Select(Path.GetFileName).ToArray();
        Assert.Equal(new[]
        {
            "001_establecer_convencion_migraciones.sql",
            "002_crear_instituciones_y_configuracion_identificadores.sql",
            "003_crear_personas_y_extender_alumnos_usuarios.sql",
            "004_crear_responsables_y_alumno_responsable.sql",
            "005_extender_ciclos_y_crear_periodos_matricula.sql",
            "006_extender_matriculas_contexto_canonico.sql",
            "007_crear_rbac_base.sql",
            "008_normalizar_modelo_academico.sql",
            "009_seguridad_rls_rpc.sql",
            "010_retirar_rol_legacy_y_consolidar_identidad.sql",
            "011_extender_creacion_alumno_con_documento.sql",
            "012_configuracion_implementacion.sql",
            "013_configuracion_centro_educativo.sql",
            "014_configurar_ciclos_y_periodos_matricula.sql",
            "015_permitir_periodos_matricula_anticipados.sql"
        }, names);
    }

    [Fact]
    public async Task Reejecucion_estructural_de_migraciones_es_idempotente()
    {
        await MigrationRunner.ApplyActiveAsync(fixture.DataSource);
    }
}
