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
  return TestBed.runInInjectionContext(() => permissionGuard(snapshotConData(data), state));
}

describe('PermissionGuard', () => {
  let router: Router;
  let auth: {
    isLoggedIn: ReturnType<typeof vi.fn>;
    tienePermiso: ReturnType<typeof vi.fn>;
    esSuperadministrador: ReturnType<typeof vi.fn>;
    usuarioActual: ReturnType<typeof vi.fn>;
  };

  const perfilAdmin: UsuarioActual = {
    id: 'u-admin', personaId: 'p-admin', roles: ['secretaria'],
    permisos: ['academico.responsables.ver', 'academico.cargos.ver']
  };

  beforeEach(() => {
    auth = {
      isLoggedIn: vi.fn(),
      tienePermiso: vi.fn(),
      esSuperadministrador: vi.fn().mockReturnValue(false),
      usuarioActual: vi.fn().mockReturnValue(perfilAdmin)
    };
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }]
    });
    router = TestBed.inject(Router);
  });

  afterEach(() => vi.restoreAllMocks());

  it('permite el acceso cuando el usuario tiene el permiso', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockReturnValue(true);
    expect(ejecutar('academico.responsables.ver')).toBe(true);
  });

  it('permite una política OR cuando posee cualquiera de los permisos', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockImplementation((permiso: string) => permiso === 'identidad.usuarios.ver');
    const resultado = ejecutarData({
      permisosCualquiera: ['identidad.roles.ver', 'identidad.usuarios.ver']
    });
    expect(resultado).toBe(true);
  });

  it('permite Superadministrador solo cuando la ruta lo declara', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockReturnValue(false);
    auth.esSuperadministrador.mockReturnValue(true);

    expect(ejecutarData({
      permisosCualquiera: ['identidad.roles.ver'],
      permitirSuperadministrador: true
    })).toBe(true);
    expect(ejecutar('academico.matriculas.ver')).toBeInstanceOf(UrlTree);
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
    expect(ejecutar('academico.matriculas.ver')).toBeInstanceOf(UrlTree);
  });

  it('permite AppShell para un perfil con capacidades administrativas', () => {
    auth.isLoggedIn.mockReturnValue(true);
    expect(ejecutar(undefined)).toBe(true);
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

  it('distingue permisos distintos', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockImplementation((permiso: string) => permiso === 'academico.responsables.ver');
    expect(ejecutar('academico.responsables.ver')).toBe(true);
    expect(ejecutar('academico.matriculas.ver')).toBeInstanceOf(UrlTree);
  });

  it('no sustituye la autorización backend', () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tienePermiso.mockImplementation((permiso: string) => permiso === 'academico.cargos.ver');
    expect(ejecutar('academico.cargos.ver')).toBe(true);
    expect(auth.tienePermiso).toHaveBeenCalledWith('academico.cargos.ver');
  });
});
