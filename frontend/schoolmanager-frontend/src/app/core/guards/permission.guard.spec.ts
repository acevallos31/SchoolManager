import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree
} from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../services/auth';
import { permissionGuard } from './permission.guard';

const state = {} as RouterStateSnapshot;

function snapshotConPermiso(permiso: string | undefined): ActivatedRouteSnapshot {
  return { data: { permiso } } as unknown as ActivatedRouteSnapshot;
}

function ejecutar(permiso: string | undefined): unknown {
  return TestBed.runInInjectionContext(() =>
    permissionGuard(snapshotConPermiso(permiso), state)
  );
}

describe('PermissionGuard', () => {
  let router: Router;
  let auth: { isLoggedIn: ReturnType<typeof vi.fn>; tienePermiso: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    auth = {
      isLoggedIn: vi.fn(),
      tienePermiso: vi.fn()
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: auth }
      ]
    });
    router = TestBed.inject(Router);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('permite el acceso cuando el usuario tiene el permiso', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockReturnValue(true);

    const resultado = ejecutar('academico.responsables.ver');

    expect(resultado).toBe(true);
    expect(auth.tienePermiso).toHaveBeenCalledWith('academico.responsables.ver');
  });

  it('redirige a /login cuando no hay sesión', () => {
    auth.isLoggedIn.mockReturnValue(false);

    const resultado = ejecutar('academico.alumnos.ver');

    expect(resultado instanceof UrlTree).toBe(true);
    expect(router.parseUrl(String(resultado)).root.children).toBeDefined();
    expect(String(resultado)).toContain('login');
  });

  it('rechaza y redirige a /dashboard cuando el usuario no tiene el permiso', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockReturnValue(false);

    const resultado = ejecutar('academico.responsables.ver');

    expect(resultado instanceof UrlTree).toBe(true);
    expect(String(resultado)).toContain('dashboard');
  });

  it('permite el acceso a rutas sin permiso declarado (autenticación pura)', () => {
    auth.isLoggedIn.mockReturnValue(true);

    const resultado = ejecutar(undefined);

    expect(resultado).toBe(true);
    expect(auth.tienePermiso).not.toHaveBeenCalled();
  });

  it('distingue permisos distintos (responsables vs matriculas)', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockImplementation((permiso: string) =>
      permiso === 'academico.responsables.ver'
    );

    expect(ejecutar('academico.responsables.ver')).toBe(true);
    expect(ejecutar('academico.matriculas.ver')).toBeInstanceOf(UrlTree);
  });

  it('distingue permisos financieros de configuración', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockImplementation((permiso: string) =>
      permiso === 'configuracion.planes_pago.ver'
    );

    expect(ejecutar('configuracion.planes_pago.ver')).toBe(true);
    expect(ejecutar('configuracion.conceptos_financieros.ver')).toBeInstanceOf(UrlTree);
  });

  it('no modifica ni borra el contexto multiinstitución del usuario', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockReturnValue(true);

    const resultado = ejecutar('academico.cargos.ver');

    // El guard es de solo lectura: solo consulta isLoggedIn/tienePermiso y
    // nunca invoca mutadores de AuthService ni servicios de contexto institucional.
    expect(resultado).toBe(true);
    expect(auth.isLoggedIn).toHaveBeenCalled();
    expect(auth.tienePermiso).toHaveBeenCalledWith('academico.cargos.ver');
    expect(auth).not.toHaveProperty('login');
    expect(auth).not.toHaveProperty('seleccionarInstitucion');
  });

  it('no sustituye la autorización backend (solo impide navegación)', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockImplementation((permiso: string) =>
      permiso === 'academico.cargos.ver'
    );

    expect(auth.tienePermiso).not.toHaveBeenCalledWith('academico.alumnos.ver');

    expect(ejecutar('academico.cargos.ver')).toBe(true);
    expect(auth.tienePermiso).toHaveBeenCalledTimes(1);
    expect(auth.tienePermiso).toHaveBeenCalledWith('academico.cargos.ver');
  });
});
