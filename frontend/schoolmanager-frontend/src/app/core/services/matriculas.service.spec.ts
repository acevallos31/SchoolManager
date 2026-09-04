import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { MatriculaError, MatriculaService } from './matriculas.service';

const BASE = `${environment.apiUrl}/matriculas`;

const MATRICULA = {
  id: 'm1',
  alumnoId: 'a1',
  institucionId: 'i1',
  cicloId: 'c1',
  seccionId: 's1',
  periodoMatriculaId: 'p1',
  registradoPor: null,
  fechaMatricula: '2026-09-01',
  estado: 'pendiente',
  fechaAnulacion: null,
  motivoAnulacion: null,
  createdAt: '2026-09-01T00:00:00Z',
  nombreAlumno: 'Ana Pérez',
  nombreSeccion: 'A',
  nombreGrado: 'Primero',
  nombreJornada: 'Matutina',
  nombrePeriodo: 'Normal',
  cicloNombre: '2026'
};

describe('MatriculaService', () => {
  let service: MatriculaService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(MatriculaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lista matrículas y envía alumnoId como query param', () => {
    let resultado: unknown;
    service.listar('a1').subscribe(v => (resultado = v));
    const req = http.expectOne(`${BASE}?alumnoId=a1`);
    expect(req.request.method).toBe('GET');
    req.flush([MATRICULA]);
    expect(resultado).toHaveLength(1);
    expect((resultado as typeof MATRICULA[])[0].nombreAlumno).toBe('Ana Pérez');
  });

  it('lista sin filtro llama a /matriculas', () => {
    let resultado: unknown;
    service.listar().subscribe(v => (resultado = v));
    const req = http.expectOne(BASE);
    expect(req.request.params.has('alumnoId')).toBe(false);
    req.flush([]);
    expect(resultado).toEqual([]);
  });

  it('listarPaginado envía filtros y page/pageSize como query params', () => {
    let resultado: unknown;
    service
      .listarPaginado({ alumnoId: 'a1', cicloId: 'c1', estado: 'pendiente', page: 2, pageSize: 20 })
      .subscribe(v => (resultado = v));
    const req = http.expectOne(
      `${BASE}?alumnoId=a1&cicloId=c1&estado=pendiente&page=2&pageSize=20`
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [MATRICULA], page: 2, pageSize: 20, totalItems: 41, totalPages: 3 });
    const r = resultado as { items: unknown[]; totalItems: number; totalPages: number };
    expect(r.items).toHaveLength(1);
    expect(r.totalItems).toBe(41);
    expect(r.totalPages).toBe(3);
  });

  it('listarPaginado sin filtros solo llama a /matriculas', () => {
    service.listarPaginado().subscribe();
    const req = http.expectOne(BASE);
    expect(req.request.params.has('page')).toBe(false);
    expect(req.request.params.has('pageSize')).toBe(false);
    req.flush({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
  });

  it('crea una matrícula mediante POST', () => {
    let resultado: unknown;
    service.crear({ alumnoId: 'a1', seccionId: 's1', periodoMatriculaId: 'p1' }).subscribe(v => (resultado = v));
    const req = http.expectOne(BASE);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ alumnoId: 'a1', seccionId: 's1', periodoMatriculaId: 'p1' });
    req.flush({ id: 'm1' });
    expect(resultado).toEqual({ id: 'm1' });
  });

  it('cambia el estado mediante PUT /{id}/estado', () => {
    let ok = false;
    service.cambiarEstado('m1', { estado: 'anulada', motivo: 'duplicado' }).subscribe(() => (ok = true));
    const req = http.expectOne(`${BASE}/m1/estado`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ estado: 'anulada', motivo: 'duplicado' });
    req.flush(null);
    expect(ok).toBe(true);
  });

  it('mapea un 409 con mensaje del cuerpo a MatriculaError', () => {
    let fallo: unknown;
    service.crear({ alumnoId: 'a1', seccionId: 's1', periodoMatriculaId: 'p1' }).subscribe({
      error: (e: unknown) => (fallo = e)
    });
    http.expectOne(BASE).flush({ error: 'Ya existe una matrícula.' }, {
      status: 409,
      statusText: 'Conflict'
    });
    expect(fallo).toBeInstanceOf(MatriculaError);
    expect((fallo as MatriculaError).status).toBe(409);
    expect((fallo as MatriculaError).message).toContain('Ya existe');
  });

  it('mapea un 403 a un mensaje de permiso', () => {
    let fallo: unknown;
    service.listar().subscribe({ error: (e: unknown) => (fallo = e) });
    http.expectOne(BASE).flush({}, { status: 403, statusText: 'Forbidden' });
    expect((fallo as MatriculaError).message).toContain('permiso');
  });
});