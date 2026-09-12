using SchoolManager.Database.IntegrationTests.Infrastructure;
using Xunit;

namespace SchoolManager.Database.IntegrationTests.Tests;

public sealed class MigrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static readonly string[] MigracionesEsperadas =
    [
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
            "015_permitir_periodos_matricula_anticipados.sql",
            "016_configurar_grados_jornadas_secciones.sql",
            "017_responsables_gestion_rpc.sql",
            "018_configuracion_financiera.sql",
            "019_cargos_mensualidades_obligaciones.sql",
            "020_grados_jornadas_multiinstitucion.sql",
            "021_pagos_cobranza.sql",
            "022_portal_responsable_lectura.sql",
            "023_rbac_permisos_aplicacion_ciclos.sql",
            "024_rbac_permisos_aplicacion_estructura.sql",
            "025_fix_unicidad_grados_jornadas_institucion.sql",
            "026_permitir_rematricula_tras_anulacion.sql",
            "027_vinculacion_identidad_oauth.sql"
    ];

    [Fact]
    public void Migraciones_activas_estan_ordenadas_de_001_a_027()
    {
        var names = MigrationRunner.GetActiveMigrationPaths().Select(Path.GetFileName).ToArray();
        Assert.Equal(MigracionesEsperadas, names);
    }

    [Fact]
    public void Cada_migracion_activa_tiene_rollback_y_validacion()
    {
        var migrationPaths = MigrationRunner.GetActiveMigrationPaths();
        var migrationDirectory = Path.GetDirectoryName(migrationPaths[0])!;

        var expectedValidationNames = migrationPaths
            .Select(path => Path.GetFileNameWithoutExtension(path) + ".validation.sql")
            .ToArray();
        var actualValidationNames = MigrationRunner.GetActiveValidationPaths()
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Equal(expectedValidationNames, actualValidationNames);

        var expectedRollbackNames = migrationPaths
            .Select(path => Path.GetFileNameWithoutExtension(path) + ".rollback.sql")
            .ToArray();
        var actualRollbackNames = Directory
            .EnumerateFiles(Path.Combine(migrationDirectory, "rollback"), "*.rollback.sql", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedRollbackNames, actualRollbackNames);
    }

    [Fact]
    public async Task Reejecucion_estructural_de_migraciones_es_idempotente()
    {
        await MigrationRunner.ApplyActiveAsync(fixture.DataSource);
    }
}
