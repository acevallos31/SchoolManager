import { TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService, InstitucionAcceso, UsuarioActual } from './auth';
import { ContextoInstitucionService } from './contexto-institucion.service';

type UsuarioExtendido = UsuarioActual & { institucionesAdministrables?: InstitucionAcceso[] };

describe('ContextoInstitucionService', () => {
  let usuario$: BehaviorSubject<UsuarioExtendido | null>;
  let service: ContextoInstitucionService;

  const institucionA: InstitucionAcceso = {
    id: 'inst-a', nombre: 'Institución A', nombreCorto: 'A',
    roles: ['secretaria'], permisos: ['academico.alumnos.ver']
  };
  const institucionB: InstitucionAcceso = {
    id: 'inst-b', nombre: 'Institución B', nombreCorto: 'B',
    roles: ['caja'], permisos: ['academico.pagos.ver']
  };
  const usuarioBase: UsuarioExtendido = {
    id: 'usuario-1', personaId: 'persona-1', roles: ['secretaria'],
    permisos: ['academico.alumnos.ver'], ambitoGlobal: { roles: [], permisos: [] },
    instituciones: []
  };

  const paraContexto = (usuario: UsuarioExtendido | null) => {
    if (!usuario) return [];
    const administrables = usuario.ambitoGlobal?.roles.includes('platform_admin')
      ? usuario.institucionesAdministrables ?? [] : [];
    const porId = new Map<string, InstitucionAcceso>();
    for (const item of administrables) porId.set(item.id, item);
    for (const item of usuario.instituciones ?? []) porId.set(item.id, item);
    return [...porId.values()];
  };

  beforeEach(() => {
    window.localStorage.clear();
    usuario$ = new BehaviorSubject<UsuarioExtendido | null>(null);
    const auth = {
      usuarioActual$: usuario$.asObservable(),
      usuarioActual: () => usuario$.value,
      institucionesDisponibles: () => usuario$.value?.instituciones ?? []
    };

    TestBed.configureTestingModule({
      providers: [ContextoInstitucionService, { provide: AuthService, useValue: auth }]
    });
    service = TestBed.inject(ContextoInstitucionService);
  });

  it('selecciona automáticamente cuando el usuario solo tiene una institución', () => {
    usuario$.next({ ...usuarioBase, instituciones: [institucionA] });
    expect(service.institucionActual()?.id).toBe('inst-a');
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(true);
    expect(service.tieneRol('secretaria')).toBe(true);
  });

  it('con varias instituciones exige elección explícita y rechaza ids ajenos', () => {
    usuario$.next({ ...usuarioBase, instituciones: [institucionA, institucionB] });
    expect(service.institucionActual()).toBeNull();
    expect(service.seleccionar('inst-inexistente')).toBe(false);
    expect(service.seleccionar('inst-b')).toBe(true);
    expect(service.institucionActual()?.id).toBe('inst-b');
    expect(service.tienePermiso('academico.pagos.ver')).toBe(true);
  });

  it('platform_admin puede seleccionar una institución administrable sin convertirla en membresía', () => {
    usuario$.next({
      ...usuarioBase,
      roles: ['platform_admin'],
      permisos: [],
      ambitoGlobal: { roles: ['platform_admin'], permisos: [] },
      instituciones: [],
      institucionesAdministrables: [{ ...institucionA, roles: [], permisos: [] }]
    });

    expect(service.institucionActual()?.id).toBe('inst-a');
    expect(service.institucionesDisponibles().map(item => item.id)).toEqual(['inst-a']);
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(false);
    expect(service.tieneRol('secretaria')).toBe(false);
  });

  it('una membresía explícita prevalece sobre el contexto administrable del mismo id', () => {
    usuario$.next({
      ...usuarioBase,
      roles: ['platform_admin', 'secretaria'],
      ambitoGlobal: { roles: ['platform_admin'], permisos: [] },
      instituciones: [institucionA],
      institucionesAdministrables: [{ ...institucionA, roles: [], permisos: [] }, institucionB]
    });

    expect(service.seleccionar('inst-a')).toBe(true);
    expect(service.tieneRol('secretaria')).toBe(true);
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(true);
  });

  it('limpia una selección persistida al cambiar de usuario o perder acceso', () => {
    usuario$.next({ ...usuarioBase, instituciones: [institucionA, institucionB] });
    expect(service.seleccionar('inst-a')).toBe(true);
    usuario$.next({
      ...usuarioBase, id: 'usuario-2', personaId: 'persona-2', instituciones: [institucionB]
    });
    expect(service.institucionActual()?.id).toBe('inst-b');
    expect(window.localStorage.getItem('schoolmanager-institucion-contexto')).toContain('usuario-2');
    usuario$.next(null);
    expect(service.institucionActual()).toBeNull();
  });
});
