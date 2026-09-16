import { describe, expect, it } from 'vitest';
import { resolverRutaInicial } from './landing-route';

describe('resolverRutaInicial', () => {
  it('envia un rol institucional dinamico al dashboard por sus permisos', () => {
    expect(resolverRutaInicial({
      id: 'u1',
      personaId: 'p1',
      roles: ['secretaria'],
      permisos: ['academico.alumnos.ver']
    })).toBe('/dashboard');
  });

  it('mantiene compatibilidad con el portal del responsable', () => {
    expect(resolverRutaInicial({
      id: 'u2',
      personaId: 'p2',
      roles: ['parent'],
      permisos: []
    })).toBe('/portal-padre');
  });

  it('no inventa una ruta para un perfil sin capacidades disponibles', () => {
    expect(resolverRutaInicial({
      id: 'u3',
      personaId: 'p3',
      roles: ['student'],
      permisos: []
    })).toBeNull();
  });
});
