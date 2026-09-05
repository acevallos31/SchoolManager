import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import {
  ConfiguracionFinancieraError,
  ConfiguracionFinancieraService,
} from './configuracion-financiera.service';

const BASE = environment.apiUrl;
const CONCEPTO = { id: 'c1', nombre: 'Matrícula', descripcion: null, monto: 100, activo: true, fechaDesactivacion: null, motivoDesactivacion: null };
const PLAN = { id: 'p1', nombre: 'Plan 2026', descripcion: null, activo: true, fechaDesactivacion: null, motivoDesactivacion: null, totalCuotas: 2, montoTotal: 300 };

describe('ConfiguracionFinancieraService', () => {
  let service: ConfiguracionFinancieraService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ConfiguracionFinancieraService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lista conceptos financia a /conceptosfinancieros', async () => {
    const promesa = service.listarConceptos();
    const req = http.expectOne(`${BASE}/conceptosfinancieros`);
    expect(req.request.method).toBe('GET');
    req.flush([CONCEPTO]);
    await expect(promesa).resolves.toHaveLength(1);
  });

  it('lista conceptos con filtro activo', async () => {
    const promesa = service.listarConceptos(false);
    const req = http.expectOne(`${BASE}/conceptosfinancieros?activo=false`);
    req.flush([]);
    await expect(promesa).resolves.toEqual([]);
  });

  it('crea un concepto mediante POST', async () => {
    const cuerpo = { nombre: 'Matrícula', monto: 100, descripcion: null };
    const promesa = service.crearConcepto(cuerpo);
    const req = http.expectOne(`${BASE}/conceptosfinancieros`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(cuerpo);
    req.flush({ id: 'c1' });
    await expect(promesa).resolves.toEqual({ id: 'c1' });
  });

  it('actualiza un concepto con PUT', async () => {
    const promesa = service.actualizarConcepto('c1', { nombre: 'Nuevo', monto: 9, descripcion: null });
    const req = http.expectOne(`${BASE}/conceptosfinancieros/c1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ nombre: 'Nuevo', monto: 9, descripcion: null });
    req.flush(null, { status: 200, statusText: 'OK' });
    await expect(promesa).resolves.toBeUndefined();
  });

  it('desactiva un concepto con DELETE y motivo en el cuerpo', async () => {
    const promesa = service.desactivarConcepto('c1', 'duplicado');
    const req = http.expectOne(`${BASE}/conceptosfinancieros/c1`);
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body).toEqual({ motivo: 'duplicado' });
    req.flush(null, { status: 200, statusText: 'OK' });
    await expect(promesa).resolves.toBeUndefined();
  });

  it('reactiva un concepto con POST /reactivar', async () => {
    const promesa = service.reactivarConcepto('c1');
    const req = http.expectOne(`${BASE}/conceptosfinancieros/c1/reactivar`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 200, statusText: 'OK' });
    await expect(promesa).resolves.toBeUndefined();
  });

  it('lista planes a /planesPago', async () => {
    const promesa = service.listarPlanes();
    const req = http.expectOne(`${BASE}/planesPago`);
    expect(req.request.method).toBe('GET');
    req.flush([PLAN]);
    await expect(promesa).resolves.toHaveLength(1);
  });

  it('obtiene un plan con sus cuotas', async () => {
    const detalle = { ...PLAN, cuotas: [{ id: 'q1', orden: 1, conceptoId: null, conceptoNombre: null, descripcion: null, monto: 150, vencimientoDias: 30 }] };
    const promesa = service.obtenerPlan('p1');
    const req = http.expectOne(`${BASE}/planesPago/p1`);
    expect(req.request.method).toBe('GET');
    req.flush(detalle);
    const r = await promesa;
    expect(r.cuotas).toHaveLength(1);
  });

  it('crea un plan con cuotas mediante POST', async () => {
    const cuerpo = { nombre: 'P', descripcion: null, cuotas: [{ orden: 1, conceptoId: null, descripcion: null, monto: 100, vencimientoDias: 30 }] };
    const promesa = service.crearPlan(cuerpo);
    const req = http.expectOne(`${BASE}/planesPago`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(cuerpo);
    req.flush({ id: 'p1' });
    await expect(promesa).resolves.toEqual({ id: 'p1' });
  });

  it('actualiza un plan y las cuotas atomáticamente con PUT', async () => {
    const cuerpo = { nombre: 'P2', descripcion: null, cuotas: [{ orden: 1, conceptoId: null, descripcion: null, monto: 9, vencimientoDias: 30 }] };
    const promesa = service.actualizarPlan('p1', cuerpo);
    const req = http.expectOne(`${BASE}/planesPago/p1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(cuerpo);
    req.flush(null, { status: 200, statusText: 'OK' });
    await expect(promesa).resolves.toBeUndefined();
  });

  it('reactiva un plan con POST /reactivar', async () => {
    const promesa = service.reactivarPlan('p1');
    const req = http.expectOne(`${BASE}/planesPago/p1/reactivar`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 200, statusText: 'OK' });
    await expect(promesa).resolves.toBeUndefined();
  });

  it('mapea un 403 con mensaje del cuerpo a ConfiguracionFinancieraError', async () => {
    const promesa = service.listarConceptos();
    http.expectOne(`${BASE}/conceptosfinancieros`).flush({ error: 'Sin permiso' }, { status: 403, statusText: 'Forbidden' });
    await expect(promesa).rejects.toBeInstanceOf(ConfiguracionFinancieraError);
    await expect(promesa).rejects.toThrow('Sin permiso');
  });
});