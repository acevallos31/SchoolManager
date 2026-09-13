import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { ContextoInstitucionService } from '../../core/services/contexto-institucion.service';
import {
  SeguridadAccesoError,
  SeguridadAccesoService,
  SeguridadAccesoSnapshot
} from '../../core/services/seguridad-acceso.service';
import { ConfiguracionSeguridadAcceso } from './configuracion-seguridad-acceso';

describe('ConfiguracionSeguridadAcceso', () => {
  let fixture: ComponentFixture<ConfiguracionSeguridadAcceso>;
  let component: ConfiguracionSeguridadAcceso;
  let service: Record<string, ReturnType<typeof vi.fn>>;
  let institucionActual: { id: string; nombre: string } | null;
  let navigate: ReturnType<typeof vi.fn>;

  const rol = {
    id: 'rol-1', codigo: 'secretaria', nombre: 'Secretaría', descripcion: null,
    activo: true, protegido: false, rolBaseId: null, plantillaVersion: 1,
    permisos: ['academico.alumnos.ver']
  };

  const snapshot: SeguridadAccesoSnapshot = {
    institucionId: 'inst-1',
    capacidades: {
      rolesVer: true, rolesCrear: true, rolesEditar: true,
      rolesAsignarPermisos: true, usuariosVer: true, usuariosAsignarRoles: true
    },
    roles: [rol],
    plantillas: [{
      id: 'tpl-1', codigo: 'school_staff', nombre: 'Personal', descripcion: null,
      version: 1, clonable: true
    }],
    permisosDelegables: [{
      codigo: 'academico.alumnos.ver', modulo: 'academico', nombre: 'Ver alumnos',
      descripcion: null, riesgo: 'bajo'
    }, {
      codigo: 'academico.pagos.ver', modulo: 'academico', nombre: 'Ver pagos',
      descripcion: null, riesgo: 'medio'
    }],
    asignaciones: []
  };

  beforeEach(async () => {
    institucionActual = { id: 'inst-1', nombre: 'Colegio Alfa' };
    service = {
      obtener: vi.fn().mockResolvedValue(snapshot),
      crearRol: vi.fn().mockResolvedValue('rol-nuevo'),
      clonarPlantilla: vi.fn().mockResolvedValue('rol-clonado'),
      reemplazarPermisos: vi.fn().mockResolvedValue(undefined),
      desactivarRol: vi.fn().mockResolvedValue(undefined)
    };

    await TestBed.configureTestingModule({
      imports: [ConfiguracionSeguridadAcceso],
      providers: [
        provideRouter([]),
        {
          provide: ContextoInstitucionService,
          useValue: { institucionActual: () => institucionActual }
        },
        { provide: SeguridadAccesoService, useValue: service }
      ]
    }).compileComponents();

    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  async function crearComponente(): Promise<void> {
    fixture = TestBed.createComponent(ConfiguracionSeguridadAcceso);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await vi.waitFor(() => expect(component.cargando).toBe(false));
    fixture.detectChanges();
  }

  it('carga el snapshot y expone institución y capacidades', async () => {
    await crearComponente();
    expect(service['obtener']).toHaveBeenCalledWith('inst-1');
    expect(component.institucionId).toBe('inst-1');
    expect(component.institucionNombre).toBe('Colegio Alfa');
    expect(component.puedeCrear).toBe(true);
    expect(component.puedeEditar).toBe(true);
    expect(component.puedeAsignarPermisos).toBe(true);
  });

  it('sin institución muestra un estado accionable y no consulta la API', async () => {
    institucionActual = null;
    await crearComponente();
    expect(service['obtener']).not.toHaveBeenCalled();
    expect(component.snapshot).toBeNull();
    expect(component.institucionNombre).toBe('Sin institución seleccionada');
    expect(component.mensaje).toContain('Seleccione una institución');
    expect(component.esError).toBe(true);
  });

  it('crea un rol normalizando código, nombre y descripción', async () => {
    await crearComponente();
    component.nuevoRol = { codigo: '  CAJA ', nombre: ' Caja ', descripcion: ' Cobranza ' };
    await component.crearRol();
    expect(service['crearRol']).toHaveBeenCalledWith({
      institucionId: 'inst-1', codigo: 'caja', nombre: 'Caja', descripcion: 'Cobranza'
    });
    expect(component.nuevoRol).toEqual({ codigo: '', nombre: '', descripcion: '' });
    expect(service['obtener']).toHaveBeenCalledTimes(2);
    expect(component.guardando).toBe(false);
  });

  it('valida campos obligatorios antes de crear un rol', async () => {
    await crearComponente();
    component.nuevoRol = { codigo: ' ', nombre: '', descripcion: '' };
    await component.crearRol();
    expect(service['crearRol']).not.toHaveBeenCalled();
    expect(component.mensaje).toContain('Código y nombre son obligatorios');
    expect(component.esError).toBe(true);
  });

  it('clona una plantilla y normaliza la nueva definición', async () => {
    await crearComponente();
    component.clonado = {
      plantillaCodigo: 'school_staff', codigo: '  SECRETARIA ', nombre: ' Secretaría ', descripcion: ''
    };
    await component.clonarPlantilla();
    expect(service['clonarPlantilla']).toHaveBeenCalledWith({
      institucionId: 'inst-1', plantillaCodigo: 'school_staff',
      codigo: 'secretaria', nombre: 'Secretaría', descripcion: null
    });
    expect(component.clonado).toEqual({ plantillaCodigo: '', codigo: '', nombre: '', descripcion: '' });
  });

  it('selecciona rol, alterna permisos y guarda la selección', async () => {
    await crearComponente();
    component.seleccionarRol(rol);
    expect(component.rolSeleccionado?.id).toBe('rol-1');
    expect(component.permisoSeleccionado(snapshot.permisosDelegables[0])).toBe(true);

    component.alternarPermiso('academico.pagos.ver', true);
    component.alternarPermiso('academico.alumnos.ver', false);
    expect(component.permisosSeleccionados.has('academico.pagos.ver')).toBe(true);
    expect(component.permisosSeleccionados.has('academico.alumnos.ver')).toBe(false);

    await component.guardarPermisos();
    expect(service['reemplazarPermisos']).toHaveBeenCalledWith(
      'rol-1', ['academico.pagos.ver']
    );
  });

  it('desactiva un rol editable y limpia la selección activa', async () => {
    await crearComponente();
    component.seleccionarRol(rol);
    await component.desactivarRol(rol);
    expect(service['desactivarRol']).toHaveBeenCalledWith(
      'rol-1', 'Desactivado desde Configuración > Seguridad y acceso'
    );
    expect(component.rolSeleccionadoId).toBe('');
    expect(component.permisosSeleccionados.size).toBe(0);
  });

  it('no muta permisos ni desactiva roles cuando la capacidad lo impide', async () => {
    service['obtener'].mockResolvedValue({
      ...snapshot,
      capacidades: { ...snapshot.capacidades, rolesEditar: false, rolesAsignarPermisos: false }
    });
    await crearComponente();
    component.seleccionarRol(rol);
    component.alternarPermiso('academico.pagos.ver', true);
    await component.guardarPermisos();
    await component.desactivarRol(rol);
    expect(component.permisosSeleccionados.has('academico.pagos.ver')).toBe(false);
    expect(service['reemplazarPermisos']).not.toHaveBeenCalled();
    expect(service['desactivarRol']).not.toHaveBeenCalled();
  });

  it('presenta el error funcional de seguridad y libera el estado guardando', async () => {
    service['crearRol'].mockRejectedValueOnce(new SeguridadAccesoError('Permiso denegado.', 403));
    await crearComponente();
    component.nuevoRol = { codigo: 'caja', nombre: 'Caja', descripcion: '' };
    await component.crearRol();
    expect(component.mensaje).toBe('Permiso denegado.');
    expect(component.esError).toBe(true);
    expect(component.guardando).toBe(false);
  });

  it('limpia una selección obsoleta después de recargar', async () => {
    await crearComponente();
    component.seleccionarRol(rol);
    service['obtener'].mockResolvedValueOnce({ ...snapshot, roles: [] });
    await component.cargar();
    expect(component.rolSeleccionado).toBeNull();
    expect(component.rolSeleccionadoId).toBe('');
    expect(component.permisosSeleccionados.size).toBe(0);
  });

  it('vuelve al hub de configuración', async () => {
    await crearComponente();
    component.volver();
    expect(navigate).toHaveBeenCalledWith(['/configuracion']);
  });
});
