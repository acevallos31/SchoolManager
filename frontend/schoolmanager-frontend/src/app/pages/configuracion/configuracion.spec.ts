import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { ConfiguracionService } from '../../core/services/configuracion.service';
import { Configuracion } from './configuracion';

describe('Configuracion', () => {
  let fixture: ComponentFixture<Configuracion>;
  let component: Configuracion;
  let service: Record<string, ReturnType<typeof vi.fn>>;
  let puedeEditar: boolean;

  beforeEach(async () => {
    puedeEditar = true;
    service = {
      obtenerContexto: vi.fn().mockResolvedValue({
        multiplesInstituciones: false,
        institucion: { id: 'institucion-1', nombre: 'Centro educativo' }
      }),
      actualizarModo: vi.fn().mockResolvedValue({
        multiplesInstituciones: true, institucion: null
      })
    };
    await TestBed.configureTestingModule({
      imports: [Configuracion],
      providers: [
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: AuthService, useValue: {
          tienePermiso: () => puedeEditar
        } },
        { provide: ConfiguracionService, useValue: service }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(Configuracion);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await vi.waitFor(() => expect(component.cargando).toBe(false));
    fixture.detectChanges();
  });

  it('muestra institución y estado single', () => {
    expect(component.contexto).toEqual({
      multiplesInstituciones: false,
      institucion: { id: 'institucion-1', nombre: 'Centro educativo' }
    });
  });

  it('sin permiso no permite editar', () => {
    puedeEditar = false;
    expect(component.puedeEditar).toBe(false);
  });

  it('el toggle usa la operación segura', async () => {
    await component.cambiarModo(true);
    expect(service['actualizarModo']).toHaveBeenCalledWith(true);
    expect(component.contexto?.multiplesInstituciones).toBe(true);
  });
});
