import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
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
  let contexto$: BehaviorSubject<{ id: string; nombre: string } | null>;
  let navigate: ReturnType<typeof vi.fn>;

  const rol = {
    id: 'rol-1', codigo: 'secretaria', nombre: 'Secretaría', descripcion: null,
    activo: true, protegido: false, rolBaseId: null, plantillaVersion: 1,
    permisos: ['academico.alumnos.ver']
  };

  const asignacion = {
    id: 'asig-1', usuarioId: 'usuario-1', nombre: 'Ana Pérez',
    rolId: 'rol-1', rolCodigo: 'secretaria', rolNombre: 'Secretaría',
    rolTipo: 'institucional', activo: true, creadoEn: '2026-09-13T10:00:00Z'
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
    asignaciones: [asignacion]
  };

  beforeEach(async () => {
    institucionActual = { id: 'inst-1', nombre: 'Colegio Alfa' };
    contexto$ = new BehaviorSubject<{ id: string; nombre: string } | null>(institucionActual);
    service = {
      obtener: vi.fn().mockResolvedValue(snapshot),
      crearRol: vi.fn().mockResolvedValue('rol-nuevo'),
      clonarPlantilla: vi.fn().mockResolvedValue('rol-clonado'),
      editarRol: vi.fn().mockResolvedValue(undefined),
      reemplazarPermisos: vi.fn().mockResolvedValue(undefined),
      desactivarRol: vi.fn().mockResolvedValue(undefined),
      desactivarAsignacion: vi.fn().mockResolvedValue(undefined)
    };

    await TestBed.configureTestingModule({
      imports: [ConfiguracionSeguridadAcceso],
      providers: [
        provideRouter([]),
        {
          provide: ContextoInstitucionService,
          useValue: {
            institucionActual: () => institucionActual,
            institucionActual$: contexto$.asObservable()
          }
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
    expect(component.puedeGestionarAsignaciones).toBe(true);
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

  it('recarga automáticamente al cambiar la institución del AppShell', async () => {
    await crearComponente();
    const snapshotBeta = { ...snapshot, institucionId: 'inst-2', roles: [] };
    service['obtener'].mockResolvedValueOnce(snapshotBeta);
    institucionActual = { id: 'inst-2', nombre: 'Colegio Beta' };
    contexto$.next(institucionActual);

    await vi.waitFor(() => expect(service['obtener']).toHaveBeenCalledWith('inst-2'));
    expect(component.institucionNombre).toBe('Colegio Beta');
    expect(component.snapshot?.institucionId).toBe('inst-2');
  });

  it('crea un rol normalizando código, nombre y descripción y conserva la confirmación', async () => {
    await crearComponente();
    component.nuevoRol = { codigo: '  CAJA ', nombre: ' Caja ', descripcion: ' Cobranza ' };
    await component.crearRol();
    expect(service['crearRol']).toHaveBeenCalledWith({
      institucionId: 'inst-1', codigo: 'caja', nombre: 'Caja', descripcion: 'Cobranza'
    });
    expect(component.nuevoRol).toEqual({ codigo: '', nombre: '', descripcion: '' });
    expect(service['obtener']).toHaveBeenCalledTimes(2);
    expect(component.mensaje).toBe('Rol institucional creado correctamente.');
    expect(component.esError).toBe(false);
  });

  it('valida campos obligatorios antes de crear un rol', async () => {
    await crearComponente();
    component.nuevoRol = { codigo: ' ', nombre: '', descripcion: '' };
    await component.crearRol();
    expect(service['crearRol']).not.toHaveBeenCalled();
    expect(component.mensaje).toContain('Código y nombre son obligatorios');
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
    expect(component.mensaje).toBe('Plantilla clonada como rol institucional.');
  });

  it('edita nombre y descripción de un rol activo', async () => {
    await crearComponente();
    component.seleccionarRol(rol);
    component.edicionRol = { nombre: ' Secretaría académica ', descripcion: ' Apoyo escolar ' };
    await component.guardarRol();
    expect(service['editarRol']).toHaveBeenCalledWith('rol-1', 'Secretaría académica', 'Apoyo escolar');
    expect(component.mensaje).toBe('Definición del rol actualizada.');
  });

  it('no guarda una definición de rol sin nombre', async () => {
    await crearComponente();
    component.seleccionarRol(rol);
    component.edicionRol.nombre = ' ';
    await component.guardarRol();
    expect(service['editarRol']).not.toHaveBeenCalled();
  });

  it('selecciona rol, alterna permisos y guarda la selección', async () => {
    await crearComponente();
    component.seleccionarRol(rol);
    component.alternarPermiso('academico.pagos.ver', true);
    component.alternarPermiso('academico.alumnos.ver', false);
    await component.guardarPermisos();
    expect(service['reemplazarPermisos']).toHaveBeenCalledWith('rol-1', ['academico.pagos.ver']);
    expect(component.mensaje).toBe('Permisos del rol actualizados.');
  });

  it('desactiva un rol editable y limpia la selección activa', async () => {
    await crearComponente();
    component.seleccionarRol(rol);
    service['obtener'].mockResolvedValueOnce({ ...snapshot, roles: [] });
    await component.desactivarRol(rol);
    expect(service['desactivarRol']).toHaveBeenCalledWith(
      'rol-1', 'Desactivado desde Configuración > Seguridad y acceso'
    );
    expect(component.rolSeleccionadoId).toBe('');
    expect(component.mensaje).toBe('Rol desactivado correctamente.');
  });

  it('retira una asignación solo con capacidad usuarios.asignar_roles', async () => {
    await crearComponente();
    await component.retirarAsignacion(asignacion);
    expect(service['desactivarAsignacion']).toHaveBeenCalledWith(
      'asig-1', 'Asignación retirada desde Configuración > Seguridad y acceso'
    );
  });

  it('no muta roles, permisos ni asignaciones cuando las capacidades lo impiden', async () => {
    service['obtener'].mockResolvedValue({
      ...snapshot,
      capacidades: {
        ...snapshot.capacidades,
        rolesEditar: false,
        rolesAsignarPermisos: false,
        usuariosAsignarRoles: false
      }
    });
    await crearComponente();
    component.seleccionarRol(rol);
    component.alternarPermiso('academico.pagos.ver', true);
    await component.guardarRol();
    await component.guardarPermisos();
    await component.desactivarRol(rol);
    await component.retirarAsignacion(asignacion);
    expect(service['editarRol']).not.toHaveBeenCalled();
    expect(service['reemplazarPermisos']).not.toHaveBeenCalled();
    expect(service['desactivarRol']).not.toHaveBeenCalled();
    expect(service['desactivarAsignacion']).not.toHaveBeenCalled();
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
  });

  it('vuelve al hub de configuración', async () => {
    await crearComponente();
    component.volver();
    expect(navigate).toHaveBeenCalledWith(['/configuracion']);
  });
});
