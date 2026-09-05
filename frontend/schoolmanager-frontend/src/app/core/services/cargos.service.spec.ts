import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Cargo, CargoError, CargosService, ResumenFinanciero } from './cargos.service';

const BASE = `${environment.apiUrl}/cargos`;
const CARGO: Cargo = {
  id: 'c1', matriculaId: 'm1', alumnoId: 'a1', planPagoId: 'p1', orden: 1,
  conceptoId: null, conceptoNombre: 'Colegiatura', descripcion: null,
  montoOriginal: 200, fechaVencimiento: '2026-09-30', estado: 'pendiente',
  fechaGeneracion: '2026-09-01T00:00:00Z', fechaAnulacion: null, motivoAnulacion: null,
  esVencido: false,
};
const RESUMEN: ResumenFinanciero = {
  alumnoId: 'a1', institucionId: '11111111-1111-1111-1111-111111111111',
  totalObligaciones: 1, totalMontoOriginal: 200, totalPendiente: 200, totalVencido: 0, totalAnulado: 0,
};

describe('CargosService', () => {
  let service: CargosService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CargosService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lista cargos de un alumno a /cargos/alumno/{id}', async () => {
    const promesa = service.listarCargosAlumno('a1');
    const req = http.expectOne(`${BASE}/alumno/a1`);
    expect(req.request.method).toBe('GET');
    req.flush([CARGO]);
    await expect(promesa).resolves.toHaveLength(1);
  });

  it('obtiene el resumen financiero a /cargos/alumno/{id}/resumen', async () => {
    const promesa = service.obtenerResumenAlumno('a1');
    const req = http.expectOne(`${BASE}/alumno/a1/resumen`);
    expect(req.request.method).toBe('GET');
    req.flush(RESUMEN);
    await expect(promesa).resolves.toMatchObject({ totalPendiente: 200 });
  });

  it('mapea un 403 con mensaje del cuerpo a CargoError', async () => {
    const promesa = service.listarCargosAlumno('a1');
    http.expectOne(`${BASE}/alumno/a1`).flush({ error: 'Sin permiso' }, { status: 403, statusText: 'Forbidden' });
    await expect(promesa).rejects.toBeInstanceOf(CargoError);
    await expect(promesa).rejects.toThrow('Sin permiso');
  });
});
