import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree
} from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService, UsuarioActual } from '../services/auth';
import { permissionGuard } from './permission.guard';

const state = {} as RouterStateSnapshot;

function snapshotConData(data: Record<string, unknown>): ActivatedRouteSnapshot {
  return { data } as unknown as ActivatedRouteSnapshot;
}

function ejecutar(permiso: string | undefined): unknown {
  return ejecutarData({ permiso });
}

function ejecutarData(data: Record<string, unknown>): unknown {
  return TestBed.runInInjectionContext(() =>
    permissionGuard(snapshotConData(data), state)
  );
}

describe('PermissionGuard', () => {
  let router: Router;
  let auth: {
    isLoggedIn: ReturnType<typeof vi.fn>;
    tienePermiso: ReturnType<typeof vi.fn>;
    usuarioActual: ReturnType<typeof vi.fn>;
  };

  const perfilAdmin: UsuarioActual = {
    id: 'u-admin',
    personaId: 'p-admin',
    roles: ['secretaria'],
    permisos: ['academico.responsables.ver', 'academico.cargos.ver']
  };

  beforeEach(() => {
    auth = {
      isLoggedIn: vi.fn(),
      tienePermiso: vi.fn(),
      usuarioActual: vi.fn().mockReturnValue(perfilAdmin)
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

  it('permite una política OR cuando posee cualquiera de los permisos', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockImplementation((permiso: string) =>
      permiso === 'identidad.usuarios.ver'
    );

    const resultado = ejecutarData({
      permisosCualquiera: ['identidad.roles.ver', 'identidad.usuarios.ver']
    });

    expect(resultado).toBe(true);
    expect(auth.tienePermiso).toHaveBeenCalledWith('identidad.roles.ver');
    expect(auth.tienePermiso).toHaveBeenCalledWith('identidad.usuarios.ver');
  });

  it('rechaza una política OR cuando no posee ninguno de los permisos', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockReturnValue(false);

    const resultado = ejecutarData({
      permisosCualquiera: ['identidad.roles.ver', 'identidad.usuarios.ver']
    });

    expect(resultado).toBeInstanceOf(UrlTree);
    expect(String(resultado)).toContain('dashboard');
  });

  it('redirige a /login cuando no hay sesión', () => {
    auth.isLoggedIn.mockReturnValue(false);

    const resultado = ejecutar('academico.alumnos.ver');

    expect(resultado instanceof UrlTree).toBe(true);
    expect(router.parseUrl(String(resultado)).root.children).toBeDefined();
    expect(String(resultado)).toContain('login');
  });

  it('redirige al dashboard cuando falta un permiso pero existe otra capacidad administrativa', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockReturnValue(false);

    const resultado = ejecutar('academico.matriculas.ver');

    expect(resultado instanceof UrlTree).toBe(true);
    expect(String(resultado)).toContain('dashboard');
  });

  it('permite AppShell para un perfil con capacidades administrativas', () => {
    auth.isLoggedIn.mockReturnValue(true);

    const resultado = ejecutar(undefined);

    expect(resultado).toBe(true);
    expect(auth.tienePermiso).not.toHaveBeenCalled();
  });

  it('evita que un responsable caiga dentro del AppShell', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.usuarioActual.mockReturnValue({
      id: 'u-padre', personaId: 'p-padre', roles: ['parent'], permisos: []
    });

    const resultado = ejecutar(undefined);

    expect(resultado).toBeInstanceOf(UrlTree);
    expect(String(resultado)).toContain('portal-padre');
  });

  it('envía a acceso pendiente un perfil válido sin módulo disponible', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.usuarioActual.mockReturnValue({
      id: 'u-alumno', personaId: 'p-alumno', roles: ['student'], permisos: []
    });

    const resultado = ejecutar(undefined);

    expect(resultado).toBeInstanceOf(UrlTree);
    expect(String(resultado)).toContain('acceso-pendiente');
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

    expect(resultado).toBe(true);
    expect(auth.isLoggedIn).toHaveBeenCalled();
    expect(auth.usuarioActual).toHaveBeenCalled();
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
