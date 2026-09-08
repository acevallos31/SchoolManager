import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { AlumnoService, AlumnoServiceError } from './alumno.service';

const BASE = `${environment.apiUrl}/alumnos`;

const ALUMNO = {
  id: 'alumno-id',
  personaId: 'persona-id',
  institucionId: 'i1',
  nombreCompleto: 'Ana López',
  identidad: '0801',
  rne: null,
  codigoInterno: 'CI-2026-0001',
  estado: 'activo',
  matriculaActual: null
};

describe('AlumnoService', () => {
  let service: AlumnoService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AlumnoService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('crear hace POST a /alumnos con los campos limpios (sin acceso directo Supabase)', async () => {
    const promesa = service.crear({
      institucionId: 'institucion-id', nombres: ' Ana ', apellidos: ' López ',
      tipoIdentificacion: 'identidad', numeroIdentificacion: '0801-2008',
      fechaNacimiento: '2008-01-01', rne: null, codigoInterno: null
    });

    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      institucionId: 'institucion-id',
      nombres: 'Ana',
      apellidos: 'López',
      tipoIdentificacion: 'identidad',
      numeroIdentificacion: '0801-2008',
      fechaNacimiento: '2008-01-01',
      rne: null,
      codigoInterno: null
    });
    req.flush({ id: 'alumno-id' });

    await expect(promesa).resolves.toBe('alumno-id');
  });

  it('crear normaliza rne/codigoInterno en blanco a null', async () => {
    const promesa = service.crear({
      institucionId: 'i1', nombres: 'Ana', apellidos: 'López',
      tipoIdentificacion: 'identidad', numeroIdentificacion: '0801',
      fechaNacimiento: null, rne: '  ', codigoInterno: ' '
    });
    const req = http.expectOne(BASE);
    expect(req.request.body.rne).toBeNull();
    expect(req.request.body.codigoInterno).toBeNull();
    req.flush({ id: 'alumno-id' });
    await promesa;
  });

  it('desactivar hace POST /{id}/desactivar con motivo limpio', async () => {
    const promesa = service.desactivar('alumno-id', ' Retiro solicitado ');
    const req = http.expectOne(`${BASE}/alumno-id/desactivar`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ motivo: 'Retiro solicitado' });
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('reactivar hace POST /{id}/reactivar', async () => {
    const promesa = service.reactivar('alumno-id');
    const req = http.expectOne(`${BASE}/alumno-id/reactivar`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('listar hace GET a /alumnos y devuelve el listado tal cual llega', async () => {
    const promesa = service.listar();
    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('GET');
    req.flush([ALUMNO]);

    const alumnos = await promesa;
    expect(alumnos).toHaveLength(1);
    expect(alumnos[0]).toMatchObject({ nombreCompleto: 'Ana López', identidad: '0801', matriculaActual: null });
  });

  it('listar preserva matriculaActual y codigoInterno de la respuesta', async () => {
    const promesa = service.listar();
    http.expectOne(BASE).flush([{
      ...ALUMNO,
      codigoInterno: 'CI-2026-0001',
      matriculaActual: { id: 'm1', ciclo: '2026', grado: 'Primero', seccion: 'A' }
    }]);

    const alumnos = await promesa;
    expect(alumnos[0].codigoInterno).toBe('CI-2026-0001');
    expect(alumnos[0].matriculaActual).toEqual({ id: 'm1', ciclo: '2026', grado: 'Primero', seccion: 'A' });
  });

  it('obtenerPorId hace GET /{id} y devuelve el alumno', async () => {
    const promesa = service.obtenerPorId('alumno-id');
    const req = http.expectOne(`${BASE}/alumno-id`);
    expect(req.request.method).toBe('GET');
    req.flush(ALUMNO);

    await expect(promesa).resolves.toMatchObject({ codigoInterno: 'CI-2026-0001' });
  });

  it('obtenerPorId devuelve null ante un 404', async () => {
    const promesa = service.obtenerPorId('no-existe');
    http.expectOne(`${BASE}/no-existe`).flush({}, { status: 404, statusText: 'Not Found' });
    await expect(promesa).resolves.toBeNull();
  });

  it('buscarPaginado envía termino/estado/page/pageSize como query params', async () => {
    const promesa = service.buscarPaginado({ termino: 'CI-2026', estado: 'activo', page: 1, pageSize: 10 });
    const req = http.expectOne(`${BASE}?termino=CI-2026&estado=activo&page=1&pageSize=10`);
    expect(req.request.method).toBe('GET');
    req.flush({ items: [ALUMNO], page: 1, pageSize: 10, totalItems: 41, totalPages: 5 });

    const r = await promesa;
    expect(r.items).toHaveLength(1);
    expect(r.items[0].codigoInterno).toBe('CI-2026-0001');
    expect(r.totalItems).toBe(41);
    expect(r.totalPages).toBe(5);
  });

  it('buscarPaginado sin filtros no envía query params', async () => {
    const promesa = service.buscarPaginado({ page: 1, pageSize: 10 });
    const req = http.expectOne(`${BASE}?page=1&pageSize=10`);
    expect(req.request.params.has('termino')).toBe(false);
    expect(req.request.params.has('estado')).toBe(false);
    req.flush({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 });
    await promesa;
  });

  it('mapea un 409 con mensaje del cuerpo a AlumnoServiceError', async () => {
    const rechazo = service.crear({
      institucionId: 'i1', nombres: 'Ana', apellidos: 'López',
      tipoIdentificacion: 'identidad', numeroIdentificacion: '0801',
      fechaNacimiento: null, rne: null, codigoInterno: null
    });
    http.expectOne(BASE).flush({ error: 'Ya existe una persona o alumno con esos identificadores.' }, {
      status: 409,
      statusText: 'Conflict'
    });
    await expect(rechazo).rejects.toBeInstanceOf(AlumnoServiceError);
    await expect(rechazo).rejects.toMatchObject({ status: 409, message: expect.stringContaining('Ya existe') });
  });

  it('listado y detalle vacíos conservan sus contratos', async () => {
    const lista = service.listar();
    http.expectOne(BASE).flush(null);
    await expect(lista).resolves.toEqual([]);
    const detalle = service.obtenerPorId('a1');
    http.expectOne(`${BASE}/a1`).flush(null);
    await expect(detalle).resolves.toBeNull();
  });

  it('paginación sin respuesta válida rechaza la operación', async () => {
    const result = service.buscarPaginado();
    const assertion = expect(result).rejects.toMatchObject({ status: 0, message: expect.stringContaining('búsqueda') });
    http.expectOne(BASE).flush(null);
    await assertion;
  });

  it('alta sin id no comunica éxito y conserva identificadores no vacíos', async () => {
    const result = service.crear({
      institucionId: 'i1', nombres: 'Ana', apellidos: 'López', tipoIdentificacion: 'identidad',
      numeroIdentificacion: '0801', fechaNacimiento: null, rne: ' RNE ', codigoInterno: ' CI '
    });
    const assertion = expect(result).rejects.toMatchObject({ status: 0, message: expect.stringContaining('creación') });
    const request = http.expectOne(BASE);
    expect(request.request.body).toMatchObject({ rne: 'RNE', codigoInterno: 'CI' });
    request.flush(null);
    await assertion;
  });

  it.each([
    ['listado', (s: AlumnoService) => s.listar()],
    ['paginación', (s: AlumnoService) => s.buscarPaginado()],
    ['detalle', (s: AlumnoService) => s.obtenerPorId('a1')],
    ['reactivación', (s: AlumnoService) => s.reactivar('a1')]
  ] as const)('%s propaga fallos de autorización', async (_name, call) => {
    const result = call(service);
    const assertion = expect(result).rejects.toMatchObject({ status: 403 });
    http.expectOne(request => request.url.startsWith(BASE)).flush(null, { status: 403, statusText: 'Forbidden' });
    await assertion;
  });

  it.each([400, 409, 500])('HTTP %s sin payload usa error estable', async status => {
    const result = service.listar();
    const assertion = expect(result).rejects.toMatchObject({ status, message: expect.any(String) });
    http.expectOne(BASE).flush(null, { status, statusText: 'Error' });
    await assertion;
  });

  it('mapea un 403 a un mensaje de permiso', async () => {
    const rechazo = service.desactivar('alumno-id', 'motivo');
    http.expectOne(`${BASE}/alumno-id/desactivar`).flush({}, { status: 403, statusText: 'Forbidden' });
    await expect(rechazo).rejects.toBeInstanceOf(AlumnoServiceError);
    await expect(rechazo).rejects.toMatchObject({ message: expect.stringContaining('permiso') });
  });
});
