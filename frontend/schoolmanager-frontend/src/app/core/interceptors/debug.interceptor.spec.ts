import { HttpErrorResponse, HttpHandler, HttpRequest, HttpResponse } from '@angular/common/http';
import { firstValueFrom, of, throwError } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { DebugStateService } from '../services/debug-state.service';
import { DebugInterceptor } from './debug.interceptor';

describe('DebugInterceptor', () => {
  const request = new HttpRequest('GET', '/api/prueba');

  it('deja pasar respuestas exitosas', async () => {
    const state = new DebugStateService();
    const interceptor = new DebugInterceptor(state);
    const next: HttpHandler = { handle: () => of(new HttpResponse({ status: 200 })) };
    await expect(firstValueFrom(interceptor.intercept(request, next))).resolves.toBeInstanceOf(HttpResponse);
    expect(state.diagnostic).toBeNull();
  });

  it('captura el bloque debug de un error HTTP y propaga el error', async () => {
    const state = new DebugStateService();
    const interceptor = new DebugInterceptor(state);
    const error = new HttpErrorResponse({
      status: 409,
      error: { debug: { requestId: 'req-1', status: 409, sqlState: '23505' } }
    });
    const next: HttpHandler = { handle: () => throwError(() => error) };

    await expect(firstValueFrom(interceptor.intercept(request, next))).rejects.toBe(error);
    expect(state.diagnostic).toMatchObject({ requestId: 'req-1', status: 409, sqlState: '23505' });
  });

  it.each([
    new HttpErrorResponse({ status: 400, error: 'texto' }),
    new Error('fallo no HTTP')
  ])('no inventa diagnóstico si el error no trae bloque debug', async error => {
    const state = new DebugStateService();
    const interceptor = new DebugInterceptor(state);
    const next: HttpHandler = { handle: () => throwError(() => error) };
    await expect(firstValueFrom(interceptor.intercept(request, next))).rejects.toBe(error);
    expect(state.diagnostic).toBeNull();
  });
});
