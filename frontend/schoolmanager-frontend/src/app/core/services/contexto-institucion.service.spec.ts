import { TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService, UsuarioActual } from './auth';
import { ContextoInstitucionService } from './contexto-institucion.service';

describe('ContextoInstitucionService', () => {
  let usuario$: BehaviorSubject<UsuarioActual | null>;
  let service: ContextoInstitucionService;

  const institucionA = {
    id: 'inst-a',
    nombre: 'Institución A',
    nombreCorto: 'A',
    roles: ['secretaria'],
    permisos: ['academico.alumnos.ver']
  };

  const institucionB = {
    id: 'inst-b',
    nombre: 'Institución B',
    nombreCorto: 'B',
    roles: ['caja'],
    permisos: ['academico.pagos.ver']
  };

  const usuarioBase: UsuarioActual = {
    id: 'usuario-1',
    personaId: 'persona-1',
    roles: ['secretaria'],
    permisos: ['academico.alumnos.ver'],
    ambitoGlobal: { roles: [], permisos: [] },
    instituciones: []
  };

  beforeEach(() => {
    window.localStorage.clear();
    usuario$ = new BehaviorSubject<UsuarioActual | null>(null);
    const auth = {
      usuarioActual$: usuario$.asObservable(),
      usuarioActual: () => usuario$.value,
      institucionesDisponibles: () => usuario$.value?.instituciones ?? []
    };

    TestBed.configureTestingModule({
      providers: [
        ContextoInstitucionService,
        { provide: AuthService, useValue: auth }
      ]
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
    expect(service.institucionActual()).toBeNull();

    expect(service.seleccionar('inst-b')).toBe(true);
    expect(service.institucionActual()?.id).toBe('inst-b');
    expect(service.tienePermiso('academico.pagos.ver')).toBe(true);
    expect(service.tienePermiso('academico.alumnos.ver')).toBe(false);
  });

  it('limpia una selección persistida al cambiar de usuario o perder acceso', () => {
    usuario$.next({ ...usuarioBase, instituciones: [institucionA, institucionB] });
    expect(service.seleccionar('inst-a')).toBe(true);
    expect(window.localStorage.getItem('schoolmanager-institucion-contexto')).toContain('inst-a');

    usuario$.next({
      ...usuarioBase,
      id: 'usuario-2',
      personaId: 'persona-2',
      instituciones: [institucionB]
    });

    expect(service.institucionActual()?.id).toBe('inst-b');
    expect(window.localStorage.getItem('schoolmanager-institucion-contexto')).toContain('usuario-2');
    expect(window.localStorage.getItem('schoolmanager-institucion-contexto')).not.toContain('inst-a');

    usuario$.next(null);
    expect(service.institucionActual()).toBeNull();
    expect(window.localStorage.getItem('schoolmanager-institucion-contexto')).toBeNull();
  });
});
