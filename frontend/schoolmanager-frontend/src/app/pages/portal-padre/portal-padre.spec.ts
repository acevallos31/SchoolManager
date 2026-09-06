import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { PortalPadre } from './portal-padre';
import { AuthService } from '../../core/services/auth';
import {
  AplicacionPago, Cargo, MisAlumno, Pago, PortalResponsableService,
  ResumenFinanciero,
} from '../../core/services/portal-responsable.service';

describe('PortalPadre (022)', () => {
  const hijoA: MisAlumno = {
    id: 'a1', institucionId: '11111111-1111-1111-1111-111111111111',
    nombres: 'María', apellidos: 'López', parentesco: 'Madre', esPrincipal: true,
  };
  const hijoB: MisAlumno = {
    id: 'a2', institucionId: '11111111-1111-1111-1111-111111111111',
    nombres: 'Juan', apellidos: 'López', parentesco: 'Madre', esPrincipal: false,
  };
  const resumen: ResumenFinanciero = {
    alumnoId: 'a1', institucionId: '11111111-1111-1111-1111-111111111111',
    totalObligaciones: 1, totalMontoOriginal: 200, totalPendiente: 200,
    totalVencido: 0, totalAnulado: 0, totalAplicado: 0,
  };
  const cargo: Cargo = {
    id: 'c1', matriculaId: 'm1', alumnoId: 'a1', planPagoId: 'p1', orden: 1,
    conceptoId: null, conceptoNombre: 'Colegiatura', descripcion: 'Cuota 1',
    montoOriginal: 200, fechaVencimiento: '2026-09-30', estado: 'pendiente',
    fechaGeneracion: new Date().toISOString(), fechaAnulacion: null,
    motivoAnulacion: null, esVencido: false, saldo: 200, aplicado: 0,
  };
  const pago: Pago = {
    id: 'pg1', institucionId: '11111111-1111-1111-1111-111111111111',
    alumnoId: 'a1', responsableId: 'r1', numeroRecibo: 1, montoTotal: 100,
    fechaPago: '2026-09-01', metodoPago: 'efectivo', referenciaExterna: null,
    estado: 'registrado', registradoPor: null, fechaAnulacion: null,
    anuladoPor: null, motivoAnulacion: null, createdAt: new Date().toISOString(),
  };
  const aplicacion: AplicacionPago = {
    aplicacionId: 'ap1', pagoId: 'pg1', cargoId: 'c1',
    institucionId: '11111111-1111-1111-1111-111111111111', montoAplicado: 100,
    estado: 'activa', fechaReversion: null, cargoEstado: 'parcial',
    conceptoNombre: 'Colegiatura', montoOriginal: 200,
  };

  let f: ComponentFixture<PortalPadre>;
  let c: PortalPadre;
  let s: Record<string, ReturnType<typeof vi.fn>>;
  let auth: Record<string, ReturnType<typeof vi.fn>>;
  let router: { navigate: ReturnType<typeof vi.fn> };

  async function armar(): Promise<void> {
    await TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [PortalPadre],
      providers: [
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: auth },
        { provide: PortalResponsableService, useValue: s }
      ]
    });
    await TestBed.compileComponents();
    f = TestBed.createComponent(PortalPadre);
    c = f.componentInstance;
    f.detectChanges();
    await f.whenStable();
  }

  beforeEach(() => {
    router = { navigate: vi.fn().mockResolvedValue(true) };
    auth = {
      getUsuarioActual: vi.fn().mockResolvedValue({ id: 'u1', personaId: 'p1', roles: ['padre'], permisos: [] }),
      logout: vi.fn().mockResolvedValue(undefined)
    };
    s = {
      misAlumnos: vi.fn().mockResolvedValue([hijoA, hijoB]),
      resumenAlumno: vi.fn().mockResolvedValue(resumen),
      cargosAlumno: vi.fn().mockResolvedValue([cargo]),
      pagosAlumno: vi.fn().mockResolvedValue([pago]),
      aplicacionesPago: vi.fn().mockResolvedValue([aplicacion])
    };
  });

  it('crea el componente', async () => {
    await armar();
    expect(c).toBeTruthy();
  });

  it('carga los hijos y selecciona el primero automáticamente', async () => {
    await armar();
    await c.cargarHijos();
    expect(s['misAlumnos']).toHaveBeenCalled();
    expect(c.hijos).toHaveLength(2);
    expect(c.hijoSeleccionadoId).toBe('a1');
    expect(s['resumenAlumno']).toHaveBeenCalledWith('a1');
    expect(s['cargosAlumno']).toHaveBeenCalledWith('a1');
    expect(s['pagosAlumno']).toHaveBeenCalledWith('a1');
  });

  it('redirige a login si no hay sesión', async () => {
    await armar();
    s['misAlumnos'].mockClear();
    router.navigate.mockClear();
    auth['getUsuarioActual'] = vi.fn().mockResolvedValue(null);
    await c.cargarHijos();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(s['misAlumnos']).not.toHaveBeenCalled();
  });

  it('seleccionar otro hijo carga sus datos', async () => {
    await armar();
    await c.cargarHijos();
    s['resumenAlumno'].mockClear();
    await c.seleccionarHijo('a2');
    expect(c.hijoSeleccionadoId).toBe('a2');
    expect(s['resumenAlumno']).toHaveBeenCalledWith('a2');
  });

  it('no muestra tarjeta de pago: sin botón de pagar en el resumen', async () => {
    await armar();
    await c.cargarHijos();
    const html = f.nativeElement.innerHTML as string;
    expect(html).not.toContain('Pagar');
    expect(html).not.toContain('tarjeta');
    expect(c.cargos).toHaveLength(1);
    expect(c.resumen?.totalPendiente).toBe(200);
  });

  it('expone aplicaciones de un pago al alternar', async () => {
    await armar();
    await c.cargarHijos();
    await c.alternarAplicaciones(pago);
    expect(s['aplicacionesPago']).toHaveBeenCalledWith('pg1');
    expect(c.aplicacionesPorPago['pg1']).toHaveLength(1);
    expect(c.pagoAbiertoId).toBe('pg1');
    await c.alternarAplicaciones(pago);
    expect(c.pagoAbiertoId).toBeNull();
  });

  it('muestra estado vacío cuando no hay hijos', async () => {
    await armar();
    s['misAlumnos'] = vi.fn().mockResolvedValue([]);
    c.hijoSeleccionadoId = '';
    await c.cargarHijos();
    expect(c.hijos).toHaveLength(0);
    expect(c.hijoSeleccionadoId).toBe('');
  });

  it('captura error al cargar hijos', async () => {
    await armar();
    s['misAlumnos'] = vi.fn().mockRejectedValue(new Error('Sin conexión'));
    await c.cargarHijos();
    expect(c.errorInicial).toBeTruthy();
  });

  it('captura error al cargar la información del alumno', async () => {
    await armar();
    s['cargosAlumno'] = vi.fn().mockRejectedValue(new Error('Error de la API'));
    await c.seleccionarHijo('a1');
    expect(c.errorTab).toBeTruthy();
  });

  it('formatea montos en HNL', async () => {
    await armar();
    expect(c.fmt(200)).toBe('L. 200.00');
    expect(c.fmt(null)).toBe('L. 0.00');
  });

  it('hace logout y navega a login', async () => {
    await armar();
    await c.logout();
    expect(auth['logout']).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });
});
