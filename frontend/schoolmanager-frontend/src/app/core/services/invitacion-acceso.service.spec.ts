import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import {
  InvitacionAccesoError,
  InvitacionAccesoService
} from './invitacion-acceso.service';

describe('InvitacionAccesoService', () => {
  let service: InvitacionAccesoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(InvitacionAccesoService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('envía únicamente el token al endpoint de aceptación', async () => {
    const promise = service.aceptar('token-seguro');
    const request = http.expectOne(`${environment.apiUrl}/invitaciones/aceptar`);

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'token-seguro' });
    expect(JSON.stringify(request.request.body)).not.toContain('authUserId');

    request.flush({
      invitacionId: 'inv-1',
      usuarioId: 'u-1',
      institucionId: 'i-1',
      estado: 'aceptada'
    });

    await expect(promise).resolves.toMatchObject({ estado: 'aceptada' });
  });

  it('propaga un error funcional seguro del backend', async () => {
    const promise = service.aceptar('token-expirado');
    const assertion = expect(promise).rejects.toEqual(
      expect.objectContaining({
        name: 'InvitacionAccesoError',
        status: 400,
        message: 'La invitación expiró.'
      })
    );

    http.expectOne(`${environment.apiUrl}/invitaciones/aceptar`).flush(
      { error: 'La invitación expiró.' },
      { status: 400, statusText: 'Bad Request' }
    );

    await assertion;
  });

  it('usa mensaje de autenticación en 401 sin detalles internos', async () => {
    const promise = service.aceptar('token');
    const assertion = expect(promise).rejects.toBeInstanceOf(InvitacionAccesoError);

    http.expectOne(`${environment.apiUrl}/invitaciones/aceptar`).flush(
      null,
      { status: 401, statusText: 'Unauthorized' }
    );

    await assertion;
  });
});
