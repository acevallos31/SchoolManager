import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { ConfiguracionError, ConfiguracionService } from '../../core/services/configuracion.service';
import { Configuracion } from './configuracion';

describe('Configuracion', () => {
  let fixture: ComponentFixture<Configuracion>;
  let component: Configuracion;
  let service: Record<string, ReturnType<typeof vi.fn>>;
  let permisos: Set<string>;

  const configuracion = {
    multiplesInstituciones: false,
    institucion: {
      id: 'institucion-1', nombre: 'Centro educativo', nombreCorto: 'CE',
      direccion: 'Tegucigalpa', telefono: '2222-2222', correo: 'info@centro.hn', logoUrl: null
    },
    identificadores: {
      rneRequerido: true, identificacionCivilRequerida: true,
      codigoInternoRequerido: false, tiposIdentificacionPermitidos: ['identidad', 'pasaporte']
    }
  };

  beforeEach(async () => {
    permisos = new Set(['configuracion.instituciones.editar', 'configuracion.sistema.editar', 'configuracion.ciclos.ver']);
    service = {
      obtenerConfiguracionInstitucion: vi.fn().mockResolvedValue(configuracion),
      crearInstitucion: vi.fn().mockResolvedValue(configuracion),
      actualizarInstitucion: vi.fn().mockResolvedValue(configuracion),
      actualizarModo: vi.fn().mockResolvedValue({ multiplesInstituciones: true, institucion: null })
    };
    await TestBed.configureTestingModule({
      imports: [Configuracion],
      providers: [
        provideRouter([{ path: 'configuracion/ciclos', component: Configuracion }]),
        { provide: AuthService, useValue: { tienePermiso: (p: string) => permisos.has(p) } },
        { provide: ConfiguracionService, useValue: service }
      ]
    }).compileComponents();
    await crearComponente();
  });

  it('muestra datos actuales y retira el estado de carga', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Cargando configuración...');
    expect(texto).toContain('Centro educativo');
    expect(texto).toContain('Tegucigalpa');
    expect(texto).toContain('RNE: obligatorio');
  });

  it('muestra la tarjeta de ciclos con permiso y navega a su ruta', async () => {
    const tarjeta = fixture.nativeElement.querySelector('.navigation-card') as HTMLElement;
    const enlace = tarjeta.querySelector('a') as HTMLAnchorElement;
    expect(tarjeta.textContent).toContain('Ciclos escolares y períodos de matrícula');
    expect(tarjeta.textContent).toContain('Administra los ciclos escolares y sus períodos de matrícula.');
    expect(enlace.textContent).toContain('Gestionar ciclos');
    expect(enlace.getAttribute('href')).toBe('/configuracion/ciclos');

    enlace.click();
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/configuracion/ciclos'));
  });

  it('oculta la tarjeta de ciclos sin permiso de lectura', async () => {
    fixture.destroy();
    permisos.delete('configuracion.ciclos.ver');
    await crearComponente();
    expect(fixture.nativeElement.querySelector('.navigation-card')).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('Gestionar ciclos');
  });

  it('permite editar, guardar por RPC y cancelar restaura el formulario', async () => {
    component.iniciarEdicion();
    component.formulario.nombre = 'Cambio temporal';
    component.cancelarEdicion();
    expect(component.formulario.nombre).toBe('');

    component.iniciarEdicion();
    component.formulario.nombre = 'Centro actualizado';
    await component.guardarInstitucion();
    expect(service['actualizarInstitucion']).toHaveBeenCalledWith(
      'institucion-1', expect.objectContaining({ nombre: 'Centro actualizado' })
    );
    expect(component.editando).toBe(false);
  });

  it('sin permiso queda en modo lectura', async () => {
    fixture.destroy();
    permisos.clear();
    await crearComponente();
    expect(fixture.nativeElement.textContent).not.toContain('Editar');
    expect(component.puedeEditarInstitucion).toBe(false);
  });

  it('sin institución ofrece configurar y usa RPC de creación', async () => {
    fixture.destroy();
    service['obtenerConfiguracionInstitucion'].mockResolvedValueOnce({
      multiplesInstituciones: false, institucion: null, identificadores: null
    });
    await crearComponente();
    expect(fixture.nativeElement.textContent).toContain('No hay un centro educativo configurado.');
    expect(fixture.nativeElement.textContent).toContain('Configurar centro educativo');
    component.iniciarEdicion();
    component.formulario.nombre = 'Primer centro';
    await component.guardarInstitucion();
    expect(service['crearInstitucion']).toHaveBeenCalled();
  });

  it('muestra errores async y actualiza visualmente el toggle', async () => {
    service['actualizarInstitucion'].mockRejectedValueOnce(
      new ConfiguracionError('El correo no tiene un formato válido.', '22023')
    );
    component.iniciarEdicion();
    await component.guardarInstitucion();
    expect(fixture.nativeElement.textContent).toContain('El correo no tiene un formato válido.');

    service['obtenerConfiguracionInstitucion'].mockRejectedValueOnce(
      new ConfiguracionError('Seleccione una institución para continuar.', 'SM003')
    );
    await component.cambiarModo(true);
    expect(component.configuracion?.multiplesInstituciones).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Seleccione una institución');
  });

  async function crearComponente(): Promise<void> {
    fixture = TestBed.createComponent(Configuracion);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await vi.waitFor(() => expect(component.cargando).toBe(false));
    fixture.detectChanges();
  }
});
