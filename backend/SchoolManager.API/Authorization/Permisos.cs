namespace SchoolManager.API.Authorization;

public static class Permisos
{
    public static class Configuracion
    {
        public const string EditarSistema = "configuracion.sistema.editar";
        public const string VerInstituciones = "configuracion.instituciones.ver";
        public const string EditarInstituciones = "configuracion.instituciones.editar";
    }

    public static class Alumnos
    {
        public const string Ver = "academico.alumnos.ver";
        public const string Crear = "academico.alumnos.crear";
        public const string Editar = "academico.alumnos.editar";
        public const string Desactivar = "academico.alumnos.desactivar";
    }

    public static class CiclosEscolares
    {
        public const string Ver = "academico.ciclos.ver";
        public const string Crear = "academico.ciclos.crear";
        public const string Editar = "academico.ciclos.editar";
        public const string Desactivar = "academico.ciclos.desactivar";
    }

    public static class EstructuraAcademica
    {
        public const string Ver = "academico.estructura.ver";
        public const string Editar = "academico.estructura.editar";
        public const string Desactivar = "academico.estructura.desactivar";
    }

    public static class Matriculas
    {
        public const string Ver = "academico.matriculas.ver";
        public const string Crear = "academico.matriculas.crear";
        public const string CambiarEstado = "academico.matriculas.cambiar_estado";
    }

    public static class Responsables
    {
        public const string Ver = "academico.responsables.ver";
        public const string Crear = "academico.responsables.crear";
        public const string Editar = "academico.responsables.editar";
    }

    public static class ConceptosFinancieros
    {
        public const string Ver = "configuracion.conceptos_financieros.ver";
        public const string Crear = "configuracion.conceptos_financieros.crear";
        public const string Editar = "configuracion.conceptos_financieros.editar";
        public const string Desactivar = "configuracion.conceptos_financieros.desactivar";
    }

    public static class PlanesPago
    {
        public const string Ver = "configuracion.planes_pago.ver";
        public const string Crear = "configuracion.planes_pago.crear";
        public const string Editar = "configuracion.planes_pago.editar";
        public const string Desactivar = "configuracion.planes_pago.desactivar";
    }

    public static class Cargos
    {
        public const string Ver = "academico.cargos.ver";
        public const string Generar = "academico.cargos.generar";
        public const string Anular = "academico.cargos.anular";
    }

    public static class Pagos
    {
        public const string Ver = "academico.pagos.ver";
        public const string Registrar = "academico.pagos.registrar";
        public const string Anular = "academico.pagos.anular";
    }

    public static IReadOnlyList<string> Todos { get; } =
    [
        Configuracion.EditarSistema,
        Configuracion.VerInstituciones,
        Configuracion.EditarInstituciones,
        Alumnos.Ver,
        Alumnos.Crear,
        Alumnos.Editar,
        Alumnos.Desactivar,
        CiclosEscolares.Ver,
        CiclosEscolares.Crear,
        CiclosEscolares.Editar,
        CiclosEscolares.Desactivar,
        EstructuraAcademica.Ver,
        EstructuraAcademica.Editar,
        EstructuraAcademica.Desactivar,
        Matriculas.Ver,
        Matriculas.Crear,
        Matriculas.CambiarEstado,
        Responsables.Ver,
        Responsables.Crear,
        Responsables.Editar,
        ConceptosFinancieros.Ver,
        ConceptosFinancieros.Crear,
        ConceptosFinancieros.Editar,
        ConceptosFinancieros.Desactivar,
        PlanesPago.Ver,
        PlanesPago.Crear,
        PlanesPago.Editar,
        PlanesPago.Desactivar,
        Cargos.Ver,
        Cargos.Generar,
        Cargos.Anular,
        Pagos.Ver,
        Pagos.Registrar,
        Pagos.Anular
    ];
}
