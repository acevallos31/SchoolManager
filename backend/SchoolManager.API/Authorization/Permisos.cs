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

    public static class Responsables
    {
        public const string Ver = "academico.responsables.ver";
        public const string Crear = "academico.responsables.crear";
        public const string Editar = "academico.responsables.editar";
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
        Responsables.Ver,
        Responsables.Crear,
        Responsables.Editar
    ];
}
