import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { ConfiguracionError, ConfiguracionService } from '../../core/services/configuracion.service';
import { DebugStateService } from '../../core/services/debug-state.service';
import { Configuracion } from './configuracion';

describe('Configuracion debug', () => {
  let component: Configuracion;
  let permisos: Set<string>;
  let service: Record<string, ReturnType<typeof vi.fn>>;
  let debugState: DebugStateService;

  const configuracion = {
    multiplesInstituciones: false,
    institucion: {
      id: 'i1', nombre: 'Centro', nombreCorto: null, direccion: null,
      telefono: null, correo: null, logoUrl: null
    },
    identificadores: {
      rneRequerido: false, identificacionCivilRequerida: false,
      codigoInternoRequerido: false, tiposIdentificacionPermitidos: ['identidad']
    }
  };

  beforeEach(async () => {
    permisos = new Set(['sistema.debug.ver']);
    service = {
      obtenerConfiguracionInstitucion: vi.fn().mockResolvedValue(configuracion),
      obtenerDebug: vi.fn().mockResolvedValue({ habilitado: false, expiraEn: null }),
      actualizarDebug: vi.fn().mockResolvedValue({ habilitado: true, expiraEn: '2026-09-10T05:00:00Z' })
    };

    await TestBed.configureTestingModule({
      imports: [Configuracion],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { tienePermiso: (p: string) => permisos.has(p) } },
        { provide: ConfiguracionService, useValue: service }
      ]
    }).compileComponents();

    debugState = TestBed.inject(DebugStateService);
    const fixture = TestBed.createComponent(Configuracion);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  });

  it('carga el estado debug al iniciar si tiene permiso', () => {
    expect(service['obtenerDebug']).toHaveBeenCalledOnce();
    expect(component.puedeUsarDebug).toBe(true);
  });

  it('activa debug con la duración seleccionada y muestra confirmación', async () => {
    component.debugMinutos = 30;
    await component.cambiarDebug(true);
    expect(service['actualizarDebug']).toHaveBeenCalledWith(true, 30);
    expect(component.mensaje).toContain('Modo debug habilitado temporalmente hasta');
    expect(component.esError).toBe(false);
    expect(component.debugGuardando).toBe(false);
  });

  it('desactiva debug y respeta guardas de permiso/operación en curso', async () => {
    service['actualizarDebug'].mockResolvedValueOnce({ habilitado: false, expiraEn: null });
    await component.cambiarDebug(false);
    expect(component.mensaje).toBe('Modo debug deshabilitado.');

    service['actualizarDebug'].mockClear();
    permisos.delete('sistema.debug.ver');
    await component.cambiarDebug(true);
    expect(service['actualizarDebug']).not.toHaveBeenCalled();

    permisos.add('sistema.debug.ver');
    component.debugGuardando = true;
    await component.cambiarDebug(true);
    expect(service['actualizarDebug']).not.toHaveBeenCalled();
  });

  it('muestra error si falla la consulta o actualización de debug', async () => {
    service['obtenerDebug'].mockRejectedValueOnce(new ConfiguracionError('No autorizado', '42501'));
    await component.cargarDebug();
    expect(component.mensaje).toBe('No autorizado');
    expect(component.esError).toBe(true);

    service['actualizarDebug'].mockRejectedValueOnce(new Error('fallo'));
    await component.cambiarDebug(true);
    expect(component.mensaje).toBe('No se pudo actualizar el modo debug.');
    expect(component.esError).toBe(true);
  });

  it('copia un diagnóstico seguro y no hace nada si no existe', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });

    await component.copiarDiagnostico();
    expect(writeText).not.toHaveBeenCalled();

    debugState.captureDiagnostic({
      requestId: 'req-77', status: 409, source: 'PostgreSQL', endpoint: 'POST /api/matriculas',
      sqlState: '23505', constraint: 'uq_matriculas_alumno_ciclo', technicalMessage: 'duplicate',
      timestamp: '2026-09-10T04:00:00Z'
    });
    await component.copiarDiagnostico();

    expect(writeText).toHaveBeenCalledOnce();
    expect(writeText.mock.calls[0][0]).toContain('Request ID: req-77');
    expect(writeText.mock.calls[0][0]).toContain('SQLSTATE: 23505');
    expect(component.mensaje).toBe('Diagnóstico copiado al portapapeles.');
  });

  it('maneja fallo del portapapeles y valores opcionales del diagnóstico', async () => {
    Object.defineProperty(navigator, 'clipboard', {
      configurable: true,
      value: { writeText: vi.fn().mockRejectedValue(new Error('clipboard')) }
    });
    debugState.captureDiagnostic({ requestId: 'req-88', status: 400 });
    await component.copiarDiagnostico();
    expect(component.mensaje).toBe('No se pudo copiar el diagnóstico.');
    expect(component.esError).toBe(true);
    expect(component.formatearFecha(null)).toBe('—');
    expect(component.formatearFecha(undefined)).toBe('—');
  });
});
