import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { ConfiguracionService } from './configuracion.service';
import { DebugStateService } from './debug-state.service';

describe('ConfiguracionService debug', () => {
  let service: ConfiguracionService;
  let http: HttpTestingController;
  let debugState: DebugStateService;
  const url = `${environment.apiUrl}/configuracion/debug`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ConfiguracionService);
    http = TestBed.inject(HttpTestingController);
    debugState = TestBed.inject(DebugStateService);
  });

  afterEach(() => http.verify());

  it('consulta debug y sincroniza el estado local', async () => {
    const promise = service.obtenerDebug();
    const request = http.expectOne(url);
    expect(request.request.method).toBe('GET');
    request.flush({ habilitado: true, expiraEn: '2026-09-10T05:00:00Z', requestId: 'req-1' });

    await expect(promise).resolves.toMatchObject({ habilitado: true, requestId: 'req-1' });
    expect(debugState.status).toMatchObject({ habilitado: true, requestId: 'req-1' });
  });

  it('activa debug por PUT con duración explícita', async () => {
    const promise = service.actualizarDebug(true, 30);
    const request = http.expectOne(url);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ habilitado: true, minutos: 30 });
    request.flush({ habilitado: true, expiraEn: '2026-09-10T05:00:00Z' });

    await expect(promise).resolves.toMatchObject({ habilitado: true });
    expect(debugState.status.habilitado).toBe(true);
  });

  it('desactiva debug y limpia el último diagnóstico', async () => {
    debugState.captureDiagnostic({ requestId: 'req-previo', status: 400 });
    const promise = service.actualizarDebug(false);
    http.expectOne(url).flush({ habilitado: false, expiraEn: null });

    await promise;
    expect(debugState.status.habilitado).toBe(false);
    expect(debugState.diagnostic).toBeNull();
  });

  it.each([
    {},
    { habilitado: 'si', expiraEn: null },
    { habilitado: true, expiraEn: 123 }
  ])('rechaza respuestas debug inválidas %j', async payload => {
    const promise = service.obtenerDebug();
    const assertion = expect(promise).rejects.toMatchObject({ code: 'INVALID_RESPONSE' });
    http.expectOne(url).flush(payload);
    await assertion;
  });

  it('acepta estado sin requestId', async () => {
    const promise = service.obtenerDebug();
    http.expectOne(url).flush({ habilitado: false, expiraEn: null });
    await expect(promise).resolves.toEqual({ habilitado: false, expiraEn: null, requestId: undefined });
  });
});
