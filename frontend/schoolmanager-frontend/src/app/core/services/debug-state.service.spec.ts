import { describe, expect, it } from 'vitest';
import { DebugStateService } from './debug-state.service';

describe('DebugStateService', () => {
  it('inicia deshabilitado y sin diagnóstico', () => {
    const service = new DebugStateService();
    expect(service.status).toEqual({ habilitado: false, expiraEn: null });
    expect(service.diagnostic).toBeNull();
  });

  it('actualiza el estado de debug', () => {
    const service = new DebugStateService();
    service.setStatus({ habilitado: true, expiraEn: '2026-09-10T05:00:00Z', requestId: 'req-1' });
    expect(service.status).toMatchObject({ habilitado: true, requestId: 'req-1' });
  });

  it('captura diagnósticos válidos y limpia el último', () => {
    const service = new DebugStateService();
    service.captureDiagnostic({ requestId: 'req-2', status: 409, sqlState: '23505' });
    expect(service.diagnostic).toMatchObject({ requestId: 'req-2', status: 409, sqlState: '23505' });
    service.clearDiagnostic();
    expect(service.diagnostic).toBeNull();
  });

  it.each([
    null,
    [],
    {},
    { requestId: 7, status: 409 },
    { requestId: 'req-3', status: '409' }
  ])('ignora diagnósticos inválidos %j', value => {
    const service = new DebugStateService();
    service.captureDiagnostic(value);
    expect(service.diagnostic).toBeNull();
  });
});
