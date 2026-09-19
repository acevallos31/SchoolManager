import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { vi } from 'vitest';
import { Cargos } from './cargos';
import { AuthService } from '../../core/services/auth';
import { AlumnoService } from '../../core/services/alumno.service';
import { Cargo, CargosService, ResumenFinanciero } from '../../core/services/cargos.service';
import { EstadoCuenta, EstadoCuentaError, EstadoCuentaService } from '../../core/services/estado-cuenta.service';
import { ImpresionService } from '../../core/services/impresion.service';
import { EstadoCuentaDocumento } from '../../core/documents/estado-cuenta.documento';

describe('Cargos (020)', () => {
  const cargoPendiente: Cargo = {
    id: 'c1', matriculaId: 'm1', alumnoId: 'a1', planPagoId: 'p1', orden: 1,
    conceptoId: null, conceptoNombre: 'Colegiatura', descripcion: 'Cuota 1',
    montoOriginal: 200, fechaVencimiento: '2026-09-30', estado: 'pendiente',
    fechaGeneracion: new Date().toISOString(), fechaAnulacion: null, motivoAnulacion: null,
    esVencido: false,
    saldo: 200, aplicado: 0,
  };
  const cargoVencido: Cargo = {
    ...cargoPendiente, id: 'c2', orden: 2, esVencido: true, fechaVencimiento: '2026-08-01',
  };
  const resumen: ResumenFinanciero = {
    alumnoId: 'a1', institucionId: '11111111-1111-1111-1111-111111111111',
    totalObligaciones: 2, totalMontoOriginal: 400, totalPendiente: 400, totalVencido: 200,
    totalAnulado: 0, totalAplicado: 0,
  };

  const estadoCuenta: EstadoCuenta = {
    institucion: {
      id: '11111111-1111-1111-1111-111111111111', nombre: 'Colegio Ejemplo',
      nombreCorto: null, direccion: null, telefono: null, correo: null, logoUrl: null,
    },
    alumno: { id: 'a1', nombreCompleto: 'Ana Pérez', rne: null, codigoInterno: 'A-01' },
    resumen,
    cargos: [cargoPendiente, cargoVencido],
    pagos: [],
  };

  let f: ComponentFixture<Cargos>;
  let c: Cargos;
  let s: Record<string, ReturnType<typeof vi.fn>>;
  let estadoCuentaService: { obtenerEstadoCuenta: ReturnType<typeof vi.fn> };
  let impresion: { imprimir: ReturnType<typeof vi.fn>; descargarPdf: ReturnType<typeof vi.fn> };
  let permisos: Set<string>;
  let router: { navigate: ReturnType<typeof vi.fn> };
  let alumnoService: { listar: ReturnType<typeof vi.fn> };

  async function armar(alumnoId: string | null): Promise<void> {
    await TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Cargos],
      providers: [
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => alumnoId } } } },
        { provide: AuthService, useValue: { tienePermiso: (x: string) => permisos.has(x) } },
        { provide: AlumnoService, useValue: alumnoService },
        { provide: CargosService, useValue: s },
        { provide: EstadoCuentaService, useValue: estadoCuentaService },
        { provide: ImpresionService, useValue: impresion }
      ]
    });
    await TestBed.compileComponents();
    f = TestBed.createComponent(Cargos);
    c = f.componentInstance;
  }

  beforeEach(() => {
    permisos = new Set(['academico.cargos.ver']);
    router = { navigate: vi.fn().mockResolvedValue(true) };
    alumnoService = {
      listar: vi.fn().mockResolvedValue([
        { id: 'a1', nombreCompleto: 'Ana Pérez', estado: 'activo' }
      ])
    };
    s = {
      listarCargosAlumno: vi.fn().mockResolvedValue([cargoPendiente, cargoVencido]),
      obtenerResumenAlumno: vi.fn().mockResolvedValue(resumen)
    };
    estadoCuentaService = {
      obtenerEstadoCuenta: vi.fn().mockResolvedValue(estadoCuenta),
    };
    impresion = {
      imprimir: vi.fn(),
      descargarPdf: vi.fn().mockResolvedValue(undefined),
    };
  });

  it('crea el componente', async () => {
    await armar('a1');
    expect(c).toBeTruthy();
  });

  it('redirige al dashboard si no tiene permiso de ver', async () => {
    permisos.clear();
    await armar('a1');
    await c.ngOnInit();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
    expect(s['listarCargosAlumno']).not.toHaveBeenCalled();
  });

  it('carga cargos y resumen del alumno indicado por query param', async () => {
    await armar('a1');
    await c.ngOnInit();
    expect(s['listarCargosAlumno']).toHaveBeenCalledWith('a1');
    expect(s['obtenerResumenAlumno']).toHaveBeenCalledWith('a1');
    expect(c.cargos).toHaveLength(2);
    expect(c.resumen?.totalPendiente).toBe(400);
  });

  it('carga alumnos aunque no venga alumnoId para permitir filtrar', async () => {
    await armar(null);
    await c.ngOnInit();
    expect(alumnoService.listar).toHaveBeenCalled();
    expect(c.alumnos).toHaveLength(1);
    expect(c.alumnoId).toBeNull();
    expect(s['listarCargosAlumno']).not.toHaveBeenCalled();
  });

  it('sincroniza el alumno seleccionado en la URL y carga su detalle', async () => {
    await armar(null);
    c.alumnoId = 'a1';

    await c.seleccionarAlumno();

    expect(router.navigate).toHaveBeenCalledWith([], expect.objectContaining({
      queryParams: { alumnoId: 'a1' },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    }));
    expect(s['listarCargosAlumno']).toHaveBeenCalledWith('a1');
  });

  it('limpia el filtro sin intentar cargar detalle', async () => {
    await armar('a1');
    c.alumnoId = null;

    await c.seleccionarAlumno();

    expect(router.navigate).toHaveBeenCalledWith([], expect.objectContaining({
      queryParams: { alumnoId: null },
    }));
    expect(s['listarCargosAlumno']).not.toHaveBeenCalled();
    expect(c.resumen).toBeNull();
  });

  it('muestra error si falla la carga del selector de alumnos', async () => {
    alumnoService.listar.mockRejectedValueOnce(new Error('Sin conexión'));
    await armar(null);
    await c.ngOnInit();
    expect(c.alumnos).toEqual([]);
    expect(c.esError).toBe(true);
  });

  it('calcula el total pendiente sumando cargos pendientes', async () => {
    await armar('a1');
    c.cargos = [cargoPendiente, { ...cargoVencido, estado: 'anulado', montoOriginal: 50 }];
    expect(c.totalPendiente).toBe(200);
  });

  it('detecta cargos vencidos pendientes', async () => {
    await armar('a1');
    c.cargos = [cargoPendiente, cargoVencido];
    expect(c.hayCargosVencidos).toBe(true);
    c.cargos = [cargoPendiente];
    expect(c.hayCargosVencidos).toBe(false);
  });

  it('muestra error cuando la API falla', async () => {
    s['listarCargosAlumno'] = vi.fn().mockRejectedValue(new Error('Sin conexión'));
    await armar('a1');
    c.alumnoId = 'a1';
    await c.cargar();
    expect(c.esError).toBe(true);
    expect(c.mensaje).toBeTruthy();
  });

  it('vuelve a alumnos', async () => {
    await armar('a1');
    c.volver();
    expect(router.navigate).toHaveBeenCalledWith(['/alumnos']);
  });
  it('imprime el estado de cuenta autoritativo del alumno seleccionado', async () => {
    await armar('a1');
    c.alumnoId = 'a1';

    await c.imprimirEstadoCuenta();

    expect(estadoCuentaService.obtenerEstadoCuenta).toHaveBeenCalledWith('a1');
    expect(impresion.imprimir).toHaveBeenCalledTimes(1);
    expect(impresion.imprimir.mock.calls[0][0]).toBeInstanceOf(EstadoCuentaDocumento);
    expect(c.generandoDocumento).toBe(false);
  });

  it('descarga el estado de cuenta en PDF', async () => {
    await armar('a1');
    c.alumnoId = 'a1';

    await c.descargarEstadoCuentaPdf();

    expect(estadoCuentaService.obtenerEstadoCuenta).toHaveBeenCalledWith('a1');
    expect(impresion.descargarPdf).toHaveBeenCalledTimes(1);
    expect(impresion.descargarPdf.mock.calls[0][0]).toBeInstanceOf(EstadoCuentaDocumento);
    expect(c.generandoDocumento).toBe(false);
  });

  it('no intenta generar documento si no hay alumno seleccionado', async () => {
    await armar(null);
    c.alumnoId = null;

    await c.imprimirEstadoCuenta();
    await c.descargarEstadoCuentaPdf();

    expect(estadoCuentaService.obtenerEstadoCuenta).not.toHaveBeenCalled();
    expect(impresion.imprimir).not.toHaveBeenCalled();
    expect(impresion.descargarPdf).not.toHaveBeenCalled();
  });

  it('mantiene generandoDocumento mientras espera la API y lo libera al terminar', async () => {
    await armar('a1');
    c.alumnoId = 'a1';

    let resolver!: (estado: EstadoCuenta) => void;
    estadoCuentaService.obtenerEstadoCuenta.mockReturnValueOnce(
      new Promise<EstadoCuenta>((resolve) => { resolver = resolve; }),
    );

    const pendiente = c.descargarEstadoCuentaPdf();
    expect(c.generandoDocumento).toBe(true);

    resolver(estadoCuenta);
    await pendiente;

    expect(c.generandoDocumento).toBe(false);
  });

  it('libera generandoDocumento y muestra error cuando falla el estado de cuenta', async () => {
    await armar('a1');
    c.alumnoId = 'a1';
    estadoCuentaService.obtenerEstadoCuenta.mockRejectedValueOnce(
      new EstadoCuentaError('No se pudo obtener el estado de cuenta.'),
    );

    await c.imprimirEstadoCuenta();

    expect(c.generandoDocumento).toBe(false);
    expect(c.esError).toBe(true);
    expect(c.mensaje).toBe('No se pudo obtener el estado de cuenta.');
    expect(impresion.imprimir).not.toHaveBeenCalled();
  });

});
