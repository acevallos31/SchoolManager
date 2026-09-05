namespace SchoolManager.API.Authorization;

public static class Permisos
{
    public static class Alumnos
    {
        public const string Ver = "academico.alumnos.ver";
        public const string Crear = "academico.alumnos.crear";
        public const string Editar = "academico.alumnos.editar";
        public const string Desactivar = "academico.alumnos.desactivar";
    }

    public static class Matriculas
    {
        public const string Ver = "academico.matriculas.ver";
        public const string Crear = "academico.matriculas.crear";
        public const string CambiarEstado = "academico.matriculas.cambiar_estado";
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

    public static IReadOnlyList<string> Todos { get; } =
    [
        Alumnos.Ver,
        Alumnos.Crear,
        Alumnos.Editar,
        Alumnos.Desactivar,
        Matriculas.Ver,
        Matriculas.Crear,
        Matriculas.CambiarEstado,
        ConceptosFinancieros.Ver,
        ConceptosFinancieros.Crear,
        ConceptosFinancieros.Editar,
        ConceptosFinancieros.Desactivar,
        PlanesPago.Ver,
        PlanesPago.Crear,
        PlanesPago.Editar,
        PlanesPago.Desactivar
    ];
}
