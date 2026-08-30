import { HttpHandler, HttpRequest } from '@angular/common/http';
import { of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { AuthService } from '../services/auth';
import { JwtInterceptor } from './jwt.interceptor';

describe('JwtInterceptor', () => {
  it('agrega Bearer solamente a la API configurada', () => {
    const interceptor = new JwtInterceptor({
      getToken: () => 'token-prueba'
    } as AuthService);
    let enviada: HttpRequest<unknown> | undefined;
    const next = {
      handle: (request: HttpRequest<unknown>) => {
        enviada = request;
        return of({});
      }
    } as HttpHandler;

    interceptor.intercept(new HttpRequest('GET', `${environment.apiUrl}/auth/me`), next).subscribe();

    expect(enviada?.headers.get('Authorization')).toBe('Bearer token-prueba');
  });

  it('no agrega Bearer a URLs externas', () => {
    const interceptor = new JwtInterceptor({
      getToken: () => 'token-prueba'
    } as AuthService);
    let enviada: HttpRequest<unknown> | undefined;
    const next = {
      handle: (request: HttpRequest<unknown>) => {
        enviada = request;
        return of({});
      }
    } as HttpHandler;

    interceptor.intercept(new HttpRequest('GET', 'https://example.com/recurso'), next).subscribe();

    expect(enviada?.headers.has('Authorization')).toBe(false);
  });
});
