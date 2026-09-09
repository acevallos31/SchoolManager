import { ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AlumnoListado, AlumnoService } from '../services/alumno.service';

export interface ContextoAlumnoFinanciero {
  alumnos: AlumnoListado[];
  alumnoId: string | null;
}

export interface VistaFinancieraContextual {
  alumnos: AlumnoListado[];
  alumnoId: string | null;
  cargar(): Promise<void>;
}

/**
 * Carga el catálogo usado por Cargos/Pagos y obtiene el alumno contextual de
 * la URL. Estas vistas funcionan en modo zoneless, por lo que el render debe
 * solicitarse después de aplicar el resultado a la vista, no antes.
 */
export async function inicializarContextoAlumnoFinanciero(
  alumnoService: Pick<AlumnoService, 'listar'>,
  route: ActivatedRoute,
  onError: (error: unknown) => void,
): Promise<ContextoAlumnoFinanciero> {
  let alumnos: AlumnoListado[] = [];
  try {
    alumnos = await alumnoService.listar();
  } catch (error: unknown) {
    onError(error);
  }

  return {
    alumnos,
    alumnoId: route.snapshot.queryParamMap.get('alumnoId'),
  };
}

/**
 * Inicialización común de las vistas financieras. Además de validar acceso,
 * aplica el catálogo y el alumno contextual directamente sobre la vista y
 * carga su detalle cuando la URL ya trae un alumno.
 */
export async function inicializarVistaFinanciera(
  vista: VistaFinancieraContextual,
  puedeVer: boolean,
  router: Router,
  route: ActivatedRoute,
  alumnoService: Pick<AlumnoService, 'listar'>,
  cdr: ChangeDetectorRef,
  onError: (error: unknown) => void,
): Promise<void> {
  if (!puedeVer) {
    await router.navigate(['/dashboard']);
    return;
  }

  const contexto = await inicializarContextoAlumnoFinanciero(
    alumnoService,
    route,
    onError,
  );
  vista.alumnos = contexto.alumnos;
  vista.alumnoId = contexto.alumnoId;

  // En modo zoneless el await anterior no agenda por sí solo un render.
  // El detectChanges debe ocurrir después de asignar alumnos/alumnoId;
  // hacerlo durante la carga dejaba el selector vacío de forma intermitente.
  cdr.detectChanges();

  if (vista.alumnoId) await vista.cargar();
}

/** Mantiene el alumno seleccionado en la URL para navegación y recarga. */
export async function sincronizarAlumnoFinancieroEnUrl(
  router: Router,
  route: ActivatedRoute,
  alumnoId: string | null,
): Promise<void> {
  await router.navigate([], {
    relativeTo: route,
    queryParams: { alumnoId: alumnoId || null },
    queryParamsHandling: 'merge',
    replaceUrl: true,
  });
}
