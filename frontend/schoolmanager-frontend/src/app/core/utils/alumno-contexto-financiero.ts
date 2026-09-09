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

export interface InicializacionVistaFinanciera {
  puedeVer: boolean;
  router: Router;
  route: ActivatedRoute;
  alumnoService: Pick<AlumnoService, 'listar'>;
  cdr: ChangeDetectorRef;
  onError: (error: unknown) => void;
  aplicarContexto: (contexto: ContextoAlumnoFinanciero) => void;
  cargarDetalle: () => Promise<void>;
}

/**
 * Inicialización común de las vistas financieras: controla permiso, carga el
 * selector de alumnos, aplica el alumno de la URL y, si existe, carga detalle.
 */
export async function inicializarVistaFinanciera(
  opciones: InicializacionVistaFinanciera,
): Promise<void> {
  if (!opciones.puedeVer) {
    await opciones.router.navigate(['/dashboard']);
    return;
  }

  const contexto = await inicializarContextoAlumnoFinanciero(
    opciones.alumnoService,
    opciones.route,
    opciones.cdr,
    opciones.onError,
  );
  opciones.aplicarContexto(contexto);

  if (contexto.alumnoId) await opciones.cargarDetalle();
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
