import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfiguracionPlanesPago } from './configuracion-planes-pago';
import { AuthService } from '../../core/services/auth';

describe('ConfiguracionPlanesPago', () => {
  let fixture: ComponentFixture<ConfiguracionPlanesPago>;
  const permisos = new Set<string>();
  const auth = { tienePermiso: (p: string) => permisos.has(p) } as AuthService;

  beforeEach(() => {
    permisos.clear();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        provideHttpClient(), provideHttpClientTesting()
      ]
    });
    fixture = TestBed.createComponent(ConfiguracionPlanesPago);
  });

  it('se crea correctamente', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('expone permisos según el usuario', () => {
    const c = fixture.componentInstance;
    expect(c.puedeCrear).toBe(false);
    permisos.add('configuracion.planes_pago.crear');
    permisos.add('configuracion.planes_pago.editar');
    permisos.add('configuracion.planes_pago.desactivar');
    expect(c.puedeCrear).toBe(true);
    expect(c.puedeEditar).toBe(true);
    expect(c.puedeDesactivar).toBe(true);
  });

  it('valida un plan sin cuotas', () => {
    const c = fixture.componentInstance;
    c.planForm = { nombre: 'Plan', descripcion: null, cuotas: [] };
    c.validar();
    expect(c.mensaje).toContain('al menos una cuota');
  });

  it('agrega cuotas con orden correlativo', () => {
    const c = fixture.componentInstance;
    c.planForm.cuotas = [{ orden: 1, conceptoId: null, descripcion: null, monto: 10, vencimientoDias: 30 }];
    c.agregarCuota();
    expect(c.planForm.cuotas).toHaveLength(2);
    expect(c.planForm.cuotas[1].orden).toBe(2);
  });

  it('calcula el total del plan', () => {
    const c = fixture.componentInstance;
    c.planForm.cuotas = [
      { orden: 1, conceptoId: null, descripcion: null, monto: 120, vencimientoDias: 30 },
      { orden: 2, conceptoId: null, descripcion: null, monto: 80, vencimientoDias: 60 }
    ];
    expect(c.totalForm).toBe(200);
  });
});