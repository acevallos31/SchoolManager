import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { InvitacionResponsablePreparada, ResponsablesError, ResponsablesService } from './responsables.service';

const BASE = `${environment.apiUrl}/responsables`;

describe('ResponsablesService', () => {
  let service: ResponsablesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ResponsablesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('prepara la invitación de acceso del responsable con POST vacío', () => {
    const respuesta: InvitacionResponsablePreparada = {
      responsableId: 'r1', personaId: 'p1', usuarioId: 'u1', rolId: 'rol1',
      asignacionId: 'ur1', invitacionId: 'i1', correo: 'padre@test.com',
      estado: 'pendiente', rolCreado: false, usuarioCreado: true,
      asignacionCreada: true, invitacionCreada: true
    };

    let recibido: InvitacionResponsablePreparada | undefined;
    service.prepararInvitacionAcceso('r1').subscribe(value => { recibido = value; });

    const req = http.expectOne(`${BASE}/r1/invitacion-acceso/preparar`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(respuesta);

    expect(recibido).toEqual(respuesta);
  });

  it('conserva el mensaje de negocio para un 400 al preparar acceso', () => {
    let recibido: unknown;
    service.prepararInvitacionAcceso('r1').subscribe({ error: error => { recibido = error; } });

    const req = http.expectOne(`${BASE}/r1/invitacion-acceso/preparar`);
    req.flush(
      { error: 'El responsable debe tener un correo antes de preparar el acceso.' },
      { status: 400, statusText: 'Bad Request' }
    );

    expect(recibido).toBeInstanceOf(ResponsablesError);
    expect((recibido as ResponsablesError).status).toBe(400);
    expect((recibido as ResponsablesError).message).toContain('correo');
  });

  it('mapea 403 a un mensaje de permisos', () => {
    let recibido: unknown;
    service.prepararInvitacionAcceso('r1').subscribe({ error: error => { recibido = error; } });

    const req = http.expectOne(`${BASE}/r1/invitacion-acceso/preparar`);
    req.flush({}, { status: 403, statusText: 'Forbidden' });

    expect(recibido).toBeInstanceOf(ResponsablesError);
    expect((recibido as ResponsablesError).status).toBe(403);
    expect((recibido as ResponsablesError).message).toContain('permiso');
  });

  it('mapea 404 a recurso fuera del alcance institucional', () => {
    let recibido: unknown;
    service.prepararInvitacionAcceso('r1').subscribe({ error: error => { recibido = error; } });

    const req = http.expectOne(`${BASE}/r1/invitacion-acceso/preparar`);
    req.flush({}, { status: 404, statusText: 'Not Found' });

    expect(recibido).toBeInstanceOf(ResponsablesError);
    expect((recibido as ResponsablesError).status).toBe(404);
    expect((recibido as ResponsablesError).message).toContain('institución');
  });

  it('conserva el mensaje de negocio para un 409', () => {
    let recibido: unknown;
    service.prepararInvitacionAcceso('r1').subscribe({ error: error => { recibido = error; } });

    const req = http.expectOne(`${BASE}/r1/invitacion-acceso/preparar`);
    req.flush(
      { error: 'El responsable ya tiene una invitación abierta con otro rol.' },
      { status: 409, statusText: 'Conflict' }
    );

    expect(recibido).toBeInstanceOf(ResponsablesError);
    expect((recibido as ResponsablesError).status).toBe(409);
    expect((recibido as ResponsablesError).message).toContain('otro rol');
  });
});
