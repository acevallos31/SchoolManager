import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { CicloEscolarService, CicloEscolarError } from './ciclo-escolar.service';

const BASE = `${environment.apiUrl}/ciclos-escolares`;

const CICLO = {
  id: 'c1', institucionId: 'i1', nombre: '2026', fechaInicio: '2026-01-01',
  fechaFin: '2026-12-31', activo: true, motivoDesactivacion: null
};
const PERIODO = {
  id: 'p1', cicloId: 'c1', nombre: 'Ordinaria', tipo: 'regular',
  fechaInicio: '2026-03-01', fechaFin: '2026-06-30', activo: true
};

describe('CicloEscolarService', () => {
  let service: CicloEscolarService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CicloEscolarService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('listar hace GET a /ciclos-escolares (sin acceso directo Supabase)', async () => {
    const promesa = service.listar();
    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('GET');
    req.flush([CICLO]);

    const ciclos = await promesa;
    expect(ciclos).toHaveLength(1);
    expect(ciclos[0]).toMatchObject({ nombre: '2026', activo: true });
  });

  it('crear hace POST a /ciclos-escolares con nombre limpio', async () => {
    const promesa = service.crear({ nombre: ' 2027 ', fechaInicio: '2027-01-01', fechaFin: '2027-12-31' });
    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ nombre: '2027', fechaInicio: '2027-01-01', fechaFin: '2027-12-31' });
    req.flush({ id: 'c9' });
    await expect(promesa).resolves.toBe('c9');
  });

  it('actualizar hace PUT a /{id}', async () => {
    const promesa = service.actualizar(CICLO, { nombre: '2026b', fechaInicio: '2026-01-01', fechaFin: '2026-12-31' });
    const req = http.expectOne(`${BASE}/c1`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('desactivar hace POST /{id}/desactivar con motivo limpio', async () => {
    const promesa = service.desactivar('c1', ' Cierre ');
    const req = http.expectOne(`${BASE}/c1/desactivar`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ motivo: 'Cierre' });
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('reactivar hace POST /{id}/reactivar', async () => {
    const promesa = service.reactivar('c1');
    const req = http.expectOne(`${BASE}/c1/reactivar`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('listarPeriodos hace GET /{cicloId}/periodos', async () => {
    const promesa = service.listarPeriodos('c1');
    const req = http.expectOne(`${BASE}/c1/periodos`);
    expect(req.request.method).toBe('GET');
    req.flush([PERIODO]);

    const periodos = await promesa;
    expect(periodos).toHaveLength(1);
    expect(periodos[0]).toMatchObject({ cicloId: 'c1', nombre: 'Ordinaria' });
  });

  it('crearPeriodo hace POST /{cicloId}/periodos y normaliza tipo en blanco a null', async () => {
    const promesa = service.crearPeriodo('c1', { nombre: ' Extra ', tipo: '  ', fechaInicio: '2026-03-01', fechaFin: '2026-06-30' });
    const req = http.expectOne(`${BASE}/c1/periodos`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ nombre: 'Extra', tipo: null, fechaInicio: '2026-03-01', fechaFin: '2026-06-30' });
    req.flush({ id: 'p9' });
    await expect(promesa).resolves.toBe('p9');
  });

  it('actualizarPeriodo hace PUT /{cicloId}/periodos/{periodoId}', async () => {
    const promesa = service.actualizarPeriodo(PERIODO, { nombre: 'Ordinaria 2', tipo: 'regular', fechaInicio: '2026-03-01', fechaFin: '2026-06-30' });
    const req = http.expectOne(`${BASE}/c1/periodos/p1`);
    expect(req.request.method).toBe('PUT');
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('desactivarPeriodo hace POST /{cicloId}/periodos/{periodoId}/desactivar', async () => {
    const promesa = service.desactivarPeriodo('c1', 'p1');
    const req = http.expectOne(`${BASE}/c1/periodos/p1/desactivar`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('reactivarPeriodo hace POST /{cicloId}/periodos/{periodoId}/reactivar', async () => {
    const promesa = service.reactivarPeriodo('c1', 'p1');
    const req = http.expectOne(`${BASE}/c1/periodos/p1/reactivar`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('mapea un 400 con mensaje del cuerpo a CicloEscolarError', async () => {
    const rechazo = service.crear({ nombre: 'Mal', fechaInicio: '2026-12-31', fechaFin: '2026-01-01' });
    http.expectOne(BASE).flush({ error: 'Nombre y rango de fechas del ciclo no son validos.' }, {
      status: 400, statusText: 'Bad Request'
    });
    await expect(rechazo).rejects.toBeInstanceOf(CicloEscolarError);
    await expect(rechazo).rejects.toMatchObject({ message: expect.stringContaining('fechas') });
  });

  it('propaga institución explícita al alta y listado', async () => {
    const alta = service.crear(CICLO, 'i1');
    const request = http.expectOne(BASE);
    expect(request.request.body.institucionId).toBe('i1');
    request.flush({ id: 'c2' });
    await expect(alta).resolves.toBe('c2');
    const lista = service.listar('i1');
    http.expectOne(`${BASE}?institucionId=i1`).flush(null);
    await expect(lista).resolves.toEqual([]);
  });

  it('lista vacía de períodos se conserva como colección', async () => {
    const result = service.listarPeriodos('c1');
    http.expectOne(`${BASE}/c1/periodos`).flush(null);
    await expect(result).resolves.toEqual([]);
  });

  it.each([
    ['ciclo', (s: CicloEscolarService) => s.crear(CICLO)],
    ['período', (s: CicloEscolarService) => s.crearPeriodo('c1', { ...PERIODO, tipo: null })]
  ] as const)('alta de %s sin id no comunica éxito', async (_name, call) => {
    const result = call(service);
    const assertion = expect(result).rejects.toMatchObject({ code: 'UNKNOWN' });
    http.expectOne(request => request.url.startsWith(BASE)).flush(null);
    await assertion;
  });

  it.each([
    ['editar ciclo', (s: CicloEscolarService) => s.actualizar(CICLO, CICLO)],
    ['desactivar ciclo', (s: CicloEscolarService) => s.desactivar('c1', 'Motivo')],
    ['reactivar ciclo', (s: CicloEscolarService) => s.reactivar('c1')],
    ['listar períodos', (s: CicloEscolarService) => s.listarPeriodos('c1')],
    ['crear período', (s: CicloEscolarService) => s.crearPeriodo('c1', PERIODO)],
    ['editar período', (s: CicloEscolarService) => s.actualizarPeriodo(PERIODO, PERIODO)],
    ['desactivar período', (s: CicloEscolarService) => s.desactivarPeriodo('c1', 'p1')],
    ['reactivar período', (s: CicloEscolarService) => s.reactivarPeriodo('c1', 'p1')]
  ] as const)('%s propaga la denegación de autorización', async (_name, call) => {
    const result = call(service);
    const assertion = expect(result).rejects.toMatchObject({ name: 'CicloEscolarError', code: '403' });
    http.expectOne(request => request.url.startsWith(BASE)).flush(null, { status: 403, statusText: 'Forbidden' });
    await assertion;
  });

  it.each([
    [401, 'permiso'], [404, 'no existe'], [409, 'Ya existe'], [400, 'fechas'], [500, 'completar']
  ] as const)('traduce HTTP %s sin cuerpo', async (status, message) => {
    const result = service.listar();
    const assertion = expect(result).rejects.toMatchObject({ code: String(status), message: expect.stringContaining(message) });
    http.expectOne(BASE).flush(null, { status, statusText: 'Error' });
    await assertion;
  });

  it.each([409, 500])('conserva mensaje API para HTTP %s', async status => {
    const result = service.listar();
    const assertion = expect(result).rejects.toMatchObject({ message: 'Validación de prueba' });
    http.expectOne(BASE).flush({ error: 'Validación de prueba' }, { status, statusText: 'Error' });
    await assertion;
  });

  it('mapea un 403 a un mensaje de permiso', async () => {
    const rechazo = service.listar();
    http.expectOne(BASE).flush({}, { status: 403, statusText: 'Forbidden' });
    await expect(rechazo).rejects.toBeInstanceOf(CicloEscolarError);
    await expect(rechazo).rejects.toMatchObject({ message: expect.stringContaining('permiso') });
  });
});
