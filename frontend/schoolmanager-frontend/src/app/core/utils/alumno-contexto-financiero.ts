import { ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AlumnoListado, AlumnoService } from '../services/alumno.service';

export interface ContextoAlumnoFinanciero {
  alumnos: AlumnoListado[];
  alumnoId: string | null;
}

/**
 * Carga el catálogo usado por Cargos/Pagos y obtiene el alumno contextual de
 * la URL. El detectChanges explícito es intencional: estas vistas funcionan
 * en modo zoneless y la continuación posterior al await no agenda un render.
 */
export async function inicializarContextoAlumnoFinanciero(
  alumnoService: Pick<AlumnoService, 'listar'>,
  route: ActivatedRoute,
  cdr: ChangeDetectorRef,
  onError: (error: unknown) => void,
): Promise<ContextoAlumnoFinanciero> {
  let alumnos: AlumnoListado[] = [];
  try {
    alumnos = await alumnoService.listar();
  } catch (error: unknown) {
    onError(error);
  } finally {
    cdr.detectChanges();
  }

  return {
    alumnos,
    alumnoId: route.snapshot.queryParamMap.get('alumnoId'),
  };
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
