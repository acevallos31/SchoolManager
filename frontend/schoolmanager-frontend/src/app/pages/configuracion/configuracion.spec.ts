import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { ConfiguracionError, ConfiguracionService } from '../../core/services/configuracion.service';
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
    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Cargando configuración...');
    expect(texto).toContain('Centro educativo');
    const toggle = fixture.nativeElement.querySelector('input[type="checkbox"]') as HTMLInputElement;
    expect(toggle.checked).toBe(false);
  });

  it('sin permiso no permite editar', async () => {
    fixture.destroy();
    puedeEditar = false;
    fixture = TestBed.createComponent(Configuracion);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await vi.waitFor(() => expect(component.cargando).toBe(false));
    const toggle = fixture.nativeElement.querySelector('input[type="checkbox"]') as HTMLInputElement;
    expect(toggle.disabled).toBe(true);
  });

  it('el toggle usa la operación segura', async () => {
    await component.cambiarModo(true);
    expect(service['actualizarModo']).toHaveBeenCalledWith(true);
    expect(component.contexto?.multiplesInstituciones).toBe(true);
    const toggle = fixture.nativeElement.querySelector('input[type="checkbox"]') as HTMLInputElement;
    expect(toggle.checked).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Configuración actualizada correctamente.');
  });

  it('muestra el error y retira el estado de carga', async () => {
    service['obtenerContexto'].mockRejectedValueOnce(
      new ConfiguracionError('No hay un centro educativo configurado.', 'SM001')
    );

    await component.ngOnInit();

    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Cargando configuración...');
    expect(texto).toContain('No hay un centro educativo configurado.');
  });
});
