import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { PagoError, PagosService, ReciboPago } from './pagos.service';

const BASE = `${environment.apiUrl}/pagos`;
const RECIBO: ReciboPago = {
  pagoId: 'p1',
  numeroRecibo: 7,
  fechaPago: '2026-09-15T10:00:00Z',
  montoTotal: 200,
  metodoPago: 'transferencia',
  referenciaExterna: 'REF-7',
  estado: 'registrado',
  fechaAnulacion: null,
  motivoAnulacion: null,
  institucion: {
    id: 'i1', nombre: 'Colegio Ejemplo', nombreCorto: 'CE', direccion: 'Av. 1',
    telefono: '123', correo: 'a@b.c', logoUrl: null,
  },
  alumno: { id: 'a1', nombreCompleto: 'Ana Pérez', rne: 'RNE-1', codigoInterno: 'A-01' },
  detalles: [{ cargoId: 'c1', concepto: 'Colegiatura', montoAplicado: 200, estado: 'aplicado' }],
};

describe('PagosService (047A recibo)', () => {
  let service: PagosService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PagosService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('obtiene el recibo autoritativo a /pagos/{id}/recibo', async () => {
    const promesa = service.obtenerRecibo('p1');
    const req = http.expectOne(`${BASE}/p1/recibo`);
    expect(req.request.method).toBe('GET');
    req.flush(RECIBO);
    const recibo = await promesa;
    expect(recibo.numeroRecibo).toBe(7);
    expect(recibo.institucion.nombre).toBe('Colegio Ejemplo');
    expect(recibo.alumno.nombreCompleto).toBe('Ana Pérez');
    expect(recibo.detalles).toHaveLength(1);
    // El DTO llega tal cual del backend: sin recálculo en el cliente.
    expect(recibo.montoTotal).toBe(200);
  });

  it('mapea un 403 del recibo a PagoError con el mensaje del cuerpo', async () => {
    const promesa = service.obtenerRecibo('p1');
    http.expectOne(`${BASE}/p1/recibo`).flush(
      { error: 'Sin permiso para ver pagos' },
      { status: 403, statusText: 'Forbidden' },
    );
    await expect(promesa).rejects.toBeInstanceOf(PagoError);
    await expect(promesa).rejects.toThrow('Sin permiso para ver pagos');
  });
});
