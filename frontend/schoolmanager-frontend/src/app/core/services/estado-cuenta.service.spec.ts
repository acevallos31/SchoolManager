import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { EstadoCuenta, EstadoCuentaError, EstadoCuentaService } from './estado-cuenta.service';

const BASE = `${environment.apiUrl}/estado-cuenta`;
const ESTADO: EstadoCuenta = {
  institucion: {
    id: 'i1', nombre: 'Colegio Ejemplo', nombreCorto: 'CE', direccion: 'Av. 1',
    telefono: '123', correo: 'a@b.c', logoUrl: null,
  },
  alumno: { id: 'a1', nombreCompleto: 'Ana Pérez', rne: 'RNE-1', codigoInterno: 'A-01' },
  resumen: {
    alumnoId: 'a1', institucionId: 'i1', totalObligaciones: 1,
    totalMontoOriginal: 1400, totalPendiente: 900, totalVencido: 0,
    totalAnulado: 0, totalAplicado: 500,
  },
  cargos: [],
  pagos: [],
};

describe('EstadoCuentaService (047B)', () => {
  let service: EstadoCuentaService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(EstadoCuentaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('usa la ruta /estado-cuenta/alumno/{id} sin enviar institucionId', async () => {
    const promesa = service.obtenerEstadoCuenta('a1');
    const req = http.expectOne(`${BASE}/alumno/a1`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.has('institucionId')).toBe(false);
    req.flush(ESTADO);
    await promesa;
  });

  it('devuelve el DTO autoritativo del backend sin recalcular totales', async () => {
    const promesa = service.obtenerEstadoCuenta('a1');
    http.expectOne(`${BASE}/alumno/a1`).flush(ESTADO);

    const estado = await promesa;
    expect(estado.alumno.nombreCompleto).toBe('Ana Pérez');
    expect(estado.resumen.totalObligaciones).toBe(1);
    expect(estado.resumen.totalPendiente).toBe(900);
    expect(estado.resumen.totalAplicado).toBe(500);
  });

  it('respuesta vacía se convierte en EstadoCuentaError', async () => {
    const promesa = service.obtenerEstadoCuenta('a1');
    http.expectOne(`${BASE}/alumno/a1`).flush(null);

    await expect(promesa).rejects.toBeInstanceOf(EstadoCuentaError);
    await expect(promesa).rejects.toThrow('La API no devolvió datos.');
  });

  it('mapea un 404 a EstadoCuentaError con el mensaje del backend', async () => {
    const promesa = service.obtenerEstadoCuenta('a1');
    http.expectOne(`${BASE}/alumno/a1`).flush(
      { error: 'El alumno no existe.' },
      { status: 404, statusText: 'Not Found' },
    );

    await expect(promesa).rejects.toBeInstanceOf(EstadoCuentaError);
    await expect(promesa).rejects.toThrow('El alumno no existe.');
  });
});
