import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { ConfiguracionConceptosFinancieros } from './configuracion-conceptos-financieros';
import { AuthService } from '../../core/services/auth';

describe('ConfiguracionConceptosFinancieros', () => {
  let fixture: ComponentFixture<ConfiguracionConceptosFinancieros>;
  const permisos = new Set<string>();
  const auth = { tienePermiso: (p: string) => permisos.has(p) } as AuthService;

  beforeEach(() => {
    permisos.clear();
    permisos.add('configuracion.conceptos_financieros.ver');
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        provideHttpClient(), provideHttpClientTesting()
      ]
    });
    fixture = TestBed.createComponent(ConfiguracionConceptosFinancieros);
  });

  it('se crea correctamente', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('expone permisos según el usuario', () => {
    const c = fixture.componentInstance;
    expect(c.puedeCrear).toBe(false);
    permisos.add('configuracion.conceptos_financieros.crear');
    permisos.add('configuracion.conceptos_financieros.editar');
    permisos.add('configuracion.conceptos_financieros.desactivar');
    expect(c.puedeCrear).toBe(true);
    expect(c.puedeEditar).toBe(true);
    expect(c.puedeDesactivar).toBe(true);
  });

  it('valida el formulario y notifica un error sin nombre', () => {
    const c = fixture.componentInstance;
    c.validar();
    expect(c.mensaje).toContain('nombre es obligatorio');
    expect(c.esError).toBe(true);
  });
});