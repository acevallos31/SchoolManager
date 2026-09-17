import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { SeguridadAccesoService } from './seguridad-acceso.service';

describe('SeguridadAccesoService', () => {
  let service: SeguridadAccesoService;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/configuracion/seguridad`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(SeguridadAccesoService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('obtiene el snapshot de la institución seleccionada', async () => {
    const snapshot = {
      institucionId: 'inst 1',
      capacidades: {
        rolesVer: true, rolesCrear: true, rolesEditar: true,
        rolesAsignarPermisos: true, usuariosVer: true, usuariosAsignarRoles: true
      },
      roles: [], plantillas: [], permisosDelegables: [], asignaciones: []
    };
    const result = service.obtener('inst 1');
    const request = http.expectOne(`${baseUrl}?institucionId=inst%201`);
    expect(request.request.method).toBe('GET');
    request.flush(snapshot);
    await expect(result).resolves.toEqual(snapshot);
  });

  it('obtiene el directorio de usuarios administrable', async () => {
    const usuarios = [{
      id: 'u1', nombre: 'Ana Pérez', nombres: 'Ana', apellidos: 'Pérez',
      correo: 'ana@example.com', activo: true, identidadVinculada: true, puedeEditar: true,
      roles: [{ asignacionId: 'a1', rolId: 'r1', codigo: 'secretaria', nombre: 'Secretaría' }]
    }];
    const result = service.obtenerUsuarios('inst 1');
    const request = http.expectOne(`${baseUrl}/usuarios?institucionId=inst%201`);
    expect(request.request.method).toBe('GET');
    request.flush(usuarios);
    await expect(result).resolves.toEqual(usuarios);
  });

  it('edita la ficha interna del usuario sin enviar metadata OAuth', async () => {
    const result = service.editarUsuario('u/1', {
      institucionId: 'i1', nombres: 'Ana', apellidos: 'Pérez', correo: 'ana@example.com'
    });
    const request = http.expectOne(`${baseUrl}/usuarios/u%2F1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      institucionId: 'i1', nombres: 'Ana', apellidos: 'Pérez', correo: 'ana@example.com'
    });
    request.flush(null);
    await expect(result).resolves.toBeUndefined();
  });

  it('prepara una invitación administrativa sin crear identidad externa', async () => {
    const result = service.prepararInvitacionUsuario({
      institucionId: 'i1', nombres: 'Ana', apellidos: 'Pérez',
      correo: 'ana@example.com', rolId: 'r1'
    });
    const request = http.expectOne(`${baseUrl}/usuarios/invitaciones/preparar`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      institucionId: 'i1', nombres: 'Ana', apellidos: 'Pérez',
      correo: 'ana@example.com', rolId: 'r1', origen: 'administracion'
    });
    request.flush({
      personaId: 'p1', usuarioId: 'u1', rolId: 'r1', invitacionId: 'inv1', estado: 'pendiente',
      personaCreada: true, usuarioCreado: true, asignacionCreada: true, invitacionCreada: true
    });
    await expect(result).resolves.toMatchObject({ invitacionId: 'inv1', estado: 'pendiente' });
  });

  it('envía una invitación preparada sin exponer el token al frontend', async () => {
    const result = service.enviarInvitacion('inv/1');
    const request = http.expectOne(`${environment.apiUrl}/invitaciones/inv%2F1/enviar`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({});
    request.flush({
      invitationId: 'inv/1', estado: 'enviada',
      expiresAt: '2026-09-18T02:00:00Z', emissionVersion: 1
    });
    await expect(result).resolves.toMatchObject({
      invitationId: 'inv/1', estado: 'enviada', emissionVersion: 1
    });
  });

  it('crea y clona roles devolviendo el identificador', async () => {
    const crear = service.crearRol({ institucionId: 'i1', codigo: 'caja', nombre: 'Caja' });
    let request = http.expectOne(`${baseUrl}/roles`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({ institucionId: 'i1', codigo: 'caja' });
    request.flush({ id: 'rol-1' });
    await expect(crear).resolves.toBe('rol-1');

    const clonar = service.clonarPlantilla({
      institucionId: 'i1', plantillaCodigo: 'school_staff', codigo: 'secretaria', nombre: 'Secretaría'
    });
    request = http.expectOne(`${baseUrl}/roles/clonar`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toMatchObject({ plantillaCodigo: 'school_staff', codigo: 'secretaria' });
    request.flush({ id: 'rol-2' });
    await expect(clonar).resolves.toBe('rol-2');
  });

  it('edita rol y reemplaza permisos sin mutar el arreglo recibido', async () => {
    const editar = service.editarRol('rol/1', 'Caja', null);
    let request = http.expectOne(`${baseUrl}/roles/rol%2F1`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ nombre: 'Caja', descripcion: null });
    request.flush(null);
    await editar;

    const permisos = ['academico.pagos.ver'];
    const reemplazar = service.reemplazarPermisos('rol/1', permisos);
    request = http.expectOne(`${baseUrl}/roles/rol%2F1/permisos`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ permisos });
    expect(request.request.body.permisos).not.toBe(permisos);
    request.flush(null);
    await reemplazar;
  });

  it('asigna rol y desactiva rol y asignación', async () => {
    const asignar = service.asignarRol('rol 1', 'usuario-1');
    let request = http.expectOne(`${baseUrl}/roles/rol%201/asignaciones`);
    expect(request.request.body).toEqual({ usuarioId: 'usuario-1' });
    request.flush({ id: 'asignacion-1' });
    await expect(asignar).resolves.toBe('asignacion-1');

    const desactivarRol = service.desactivarRol('rol 1', 'prueba');
    request = http.expectOne(`${baseUrl}/roles/rol%201/desactivar`);
    expect(request.request.body).toEqual({ motivo: 'prueba' });
    request.flush(null);
    await desactivarRol;

    const desactivarAsignacion = service.desactivarAsignacion('asig 1', 'prueba');
    request = http.expectOne(`${baseUrl}/asignaciones/asig%201/desactivar`);
    expect(request.request.body).toEqual({ motivo: 'prueba' });
    request.flush(null);
    await desactivarAsignacion;
  });

  it('propaga el mensaje funcional del backend', async () => {
    const result = service.crearRol({ institucionId: 'i1', codigo: 'x', nombre: 'X' });
    const assertion = expect(result).rejects.toMatchObject({
      name: 'SeguridadAccesoError', status: 409, message: 'El código ya existe.'
    });
    http.expectOne(`${baseUrl}/roles`).flush(
      { error: 'El código ya existe.' }, { status: 409, statusText: 'Conflict' }
    );
    await assertion;
  });

  it('traduce 403 sin payload y errores de red', async () => {
    const forbidden = service.editarRol('r1', 'Rol', null);
    const forbiddenAssertion = expect(forbidden).rejects.toMatchObject({
      status: 403,
      message: 'No tienes permiso para administrar la seguridad de esta institución.'
    });
    http.expectOne(`${baseUrl}/roles/r1`).flush(null, { status: 403, statusText: 'Forbidden' });
    await forbiddenAssertion;

    const network = service.desactivarRol('r1', 'motivo');
    const networkAssertion = expect(network).rejects.toMatchObject({
      status: 0, message: 'No se pudo completar la operación de seguridad.'
    });
    http.expectOne(`${baseUrl}/roles/r1/desactivar`).error(new ProgressEvent('error'));
    await networkAssertion;
  });

  it('explica cuando el frontend preview apunta a una API sin el módulo de seguridad', async () => {
    const result = service.obtener('i1');
    const assertion = expect(result).rejects.toMatchObject({
      status: 404,
      message: 'La API desplegada todavía no incluye esta operación de Seguridad y acceso.'
    });
    http.expectOne(`${baseUrl}?institucionId=i1`).flush(
      'Not Found', { status: 404, statusText: 'Not Found' }
    );
    await assertion;
  });
});
