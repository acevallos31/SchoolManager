import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { vi } from 'vitest';
import { Pagos } from './pagos';
import { AuthService } from '../../core/services/auth';
import { AlumnoService } from '../../core/services/alumno.service';
import { Cargo, CargosService } from '../../core/services/cargos.service';
import { PagosService, Pago } from '../../core/services/pagos.service';

describe('Pagos (021)', () => {
  const cargoPendiente: Cargo = {
    id: 'c1', matriculaId: 'm1', alumnoId: 'a1', planPagoId: 'p1', orden: 1,
    conceptoId: null, conceptoNombre: 'Colegiatura', descripcion: 'Cuota 1',
    montoOriginal: 200, fechaVencimiento: '2026-09-30', estado: 'pendiente',
    fechaGeneracion: new Date().toISOString(), fechaAnulacion: null, motivoAnulacion: null,
    esVencido: false, saldo: 200, aplicado: 0,
  };
  const cargoVencido: Cargo = {
    ...cargoPendiente, id: 'c2', orden: 2, esVencido: true, fechaVencimiento: '2026-08-01',
  };
  const cargoPagado: Cargo = { ...cargoPendiente, id: 'c3', estado: 'pagado', saldo: 0, aplicado: 200 };

  const pagoRegistrado: Pago = {
    id: 'p1', institucionId: '11111111-1111-1111-1111-111111111111', alumnoId: 'a1',
    responsableId: null, numeroRecibo: 1, montoTotal: 200, fechaPago: new Date().toISOString(),
    metodoPago: 'transferencia', referenciaExterna: 'REF-1', estado: 'registrado',
    registradoPor: 'u1', fechaAnulacion: null, anuladoPor: null, motivoAnulacion: null,
    createdAt: new Date().toISOString(),
  };

  let f: ComponentFixture<Pagos>;
  let c: Pagos;
  let s: Record<string, ReturnType<typeof vi.fn>>;
  let permisos: Set<string>;
  let router: { navigate: ReturnType<typeof vi.fn> };
  let alumnoService: { listar: ReturnType<typeof vi.fn> };

  async function armar(alumnoId: string | null): Promise<void> {
    await TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Pagos],
      providers: [
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => alumnoId } } } },
        { provide: AuthService, useValue: { tienePermiso: (x: string) => permisos.has(x) } },
        { provide: AlumnoService, useValue: alumnoService },
        { provide: CargosService, useValue: { listarCargosAlumno: s.listarCargosAlumno } },
        { provide: PagosService, useValue: s }
      ]
    });
    await TestBed.compileComponents();
    f = TestBed.createComponent(Pagos);
    c = f.componentInstance;
    f.detectChanges();
  }

  beforeEach(() => {
    permisos = new Set(['academico.pagos.ver', 'academico.pagos.registrar', 'academico.pagos.anular']);
    router = { navigate: vi.fn().mockResolvedValue(true) };
    alumnoService = {
      listar: vi.fn().mockResolvedValue([
        { id: 'a1', nombreCompleto: 'Ana Pérez', estado: 'activo' }
      ])
    };
    s = {
      listarCargosAlumno: vi.fn().mockResolvedValue([cargoPendiente, cargoVencido]),
      listarPagosAlumno: vi.fn().mockResolvedValue([pagoRegistrado]),
      registrarPago: vi.fn().mockResolvedValue({ id: 'p9' }),
      anularPago: vi.fn().mockResolvedValue(undefined),
      obtenerAplicaciones: vi.fn().mockResolvedValue([])
    };
  });

  it('crea el componente', async () => {
    await armar('a1');
    expect(c).toBeTruthy();
  });

  it('redirige al dashboard sin permiso de ver y no carga', async () => {
    await armar('a1');
    s.listarCargosAlumno.mockClear();
    s.listarPagosAlumno.mockClear();
    router.navigate.mockClear();
    permisos = new Set();
    await c.ngOnInit();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
    expect(s.listarPagosAlumno).not.toHaveBeenCalled();
  });

  it('carga cargos y pagos del alumno del query param', async () => {
    await armar('a1');
    await c.cargar();
    expect(s.listarCargosAlumno).toHaveBeenCalledWith('a1');
    expect(s.listarPagosAlumno).toHaveBeenCalledWith('a1');
    expect(c.cargos).toHaveLength(2);
    expect(c.pagos).toHaveLength(1);
  });

  it('carga alumnos aunque no venga alumnoId para permitir filtrar', async () => {
    await armar(null);
    await c.ngOnInit();
    expect(alumnoService.listar).toHaveBeenCalled();
    expect(c.alumnos).toHaveLength(1);
    expect(s.listarPagosAlumno).not.toHaveBeenCalled();
  });

  it('expone solo cargos pendientes o parciales como cobrables', async () => {
    await armar('a1');
    c.cargos = [cargoPendiente, cargoVencido, cargoPagado];
    expect(c.cargoCobrables.map((x) => x.id)).toEqual(['c1', 'c2']);
  });

  it('suma los montos ingresados como total a cobrar', async () => {
    await armar('a1');
    c.cargos = [cargoPendiente, cargoVencido, cargoPagado];
    c.montos = { c1: '100', c2: '50' };
    expect(c.totalMontoTotal).toBe(150);
    expect(c.totalSeleccionadoValido).toBe(true);
  });

  it('rechaza un monto que excede el saldo del cargo (sobrepago)', async () => {
    await armar('a1');
    c.cargos = [cargoPendiente, cargoPagado];
    c.montos = { c1: '250' };
    expect(c.totalSeleccionadoValido).toBe(false);
  });

  it('registra el pago con el cuerpo correcto y limpia el formulario', async () => {
    await armar('a1');
    c.cargos = [cargoPendiente, cargoPagado];
    c.montos = { c1: '200' };
    c.metodoPago = 'transferencia';
    c.showFormulario = true;
    await c.registrar();
    expect(s.registrarPago).toHaveBeenCalledWith('a1', {
      montoTotal: 200,
      aplicaciones: [{ cargoId: 'c1', monto: 200 }],
      metodoPago: 'transferencia',
      referenciaExterna: null,
    });
    expect(c.showFormulario).toBe(false);
    expect(c.mensaje).toContain('registrado');
  });

  it('anular exige motivo y no llama a la API en blanco', async () => {
    await armar('a1');
    c.motivoAnulacion = '';
    await c.anular(pagoRegistrado);
    expect(s.anularPago).not.toHaveBeenCalled();
    expect(c.esError).toBe(true);
  });

  it('anula el pago con motivo y restablece saldos', async () => {
    await armar('a1');
    c.motivoAnulacion = 'Pago duplicado';
    await c.anular(pagoRegistrado);
    expect(s.anularPago).toHaveBeenCalledWith('p1', 'Pago duplicado');
    expect(c.mensaje).toContain('anulado');
  });
});
