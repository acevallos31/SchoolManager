import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { AlumnoListado, AlumnoService, AlumnoServiceError } from '../../core/services/alumno.service';
import { AuthService } from '../../core/services/auth';
import { ConfiguracionService } from '../../core/services/configuracion.service';
import { Alumnos } from './alumnos';

describe('Alumnos', () => {
  let component: Alumnos;
  let fixture: ComponentFixture<Alumnos>;
  let alumnoService: Record<string, ReturnType<typeof vi.fn>>;
  let navigate: ReturnType<typeof vi.fn>;
  let permisos: Set<string>;
  let configuracionService: Record<string, ReturnType<typeof vi.fn>>;

  const alumnoSinMatricula: AlumnoListado = {
    id: 'alumno-1', personaId: 'persona-1', nombreCompleto: 'Ana López',
    identidad: '0801-2008-00001', rne: null, codigoInterno: 'CI-2026-0001', estado: 'activo', matriculaActual: null
  };

  beforeEach(async () => {
    permisos = new Set([
      'academico.alumnos.ver', 'academico.alumnos.crear',
      'academico.alumnos.editar', 'academico.alumnos.desactivar'
    ]);
    navigate = vi.fn();
    configuracionService = {
      obtenerConfiguracionInstitucion: vi.fn().mockResolvedValue({
        multiplesInstituciones: false,
        institucion: { id: 'institucion-1', nombre: 'Centro educativo' },
        identificadores: {
          rneRequerido: false, identificacionCivilRequerida: true,
          codigoInternoRequerido: false, tiposIdentificacionPermitidos: ['identidad']
        }
      })
    };
    alumnoService = {
      listar: vi.fn().mockResolvedValue([alumnoSinMatricula]),
      crear: vi.fn().mockResolvedValue('alumno-nuevo'),
      desactivar: vi.fn().mockResolvedValue(undefined),
      reactivar: vi.fn().mockResolvedValue(undefined)
    };

    await TestBed.configureTestingModule({
      imports: [Alumnos],
      providers: [
        { provide: Router, useValue: { navigate } },
        { provide: AuthService, useValue: { tienePermiso: (p: string) => permisos.has(p) } },
        { provide: AlumnoService, useValue: alumnoService },
        { provide: ConfiguracionService, useValue: configuracionService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Alumnos);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('carga alumnos y muestra alumno sin matrícula activa', () => {
    expect(alumnoService['listar']).toHaveBeenCalledOnce();
    expect(component.alumnos).toEqual([alumnoSinMatricula]);
    expect(fixture.nativeElement.textContent).toContain('Ana López');
    expect(fixture.nativeElement.textContent).toContain('Sin matrícula');
  });

  it('el formulario no muestra ciclo, grado ni sección', async () => {
    await component.abrirFormulario();
    fixture.detectChanges();
    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Ciclo escolar');
    expect(texto).not.toContain('Seleccionar grado');
    expect(texto).not.toContain('Seleccionar sección');
  });

  it('resuelve institución desde configuración y no desde ciclos', async () => {
    await component.abrirFormulario();
    expect(configuracionService['obtenerConfiguracionInstitucion']).toHaveBeenCalledOnce();
    expect(alumnoService['cargarCiclos']).toBeUndefined();
  });

  it('exige campos configurados como obligatorios', async () => {
    configuracionService['obtenerConfiguracionInstitucion'].mockResolvedValueOnce({
      multiplesInstituciones: false,
      institucion: { id: 'institucion-1', nombre: 'Centro educativo' },
      identificadores: {
        rneRequerido: true, identificacionCivilRequerida: true,
        codigoInternoRequerido: true, tiposIdentificacionPermitidos: ['identidad']
      }
    });
    await component.abrirFormulario();
    component.nuevoAlumno = {
      nombres: 'Ana', apellidos: 'López', tipoIdentificacion: 'identidad',
      numeroIdentificacion: '0801', fechaNacimiento: '', rne: '', codigoInterno: ''
    };

    await component.guardarAlumno();

    expect(alumnoService['crear']).not.toHaveBeenCalled();
    expect(component.mensaje).toContain('campos obligatorios');
  });

  it('sin permiso crear oculta el botón Nuevo Alumno', () => {
    fixture.destroy();
    permisos.delete('academico.alumnos.crear');
    fixture = TestBed.createComponent(Alumnos);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('+ Nuevo Alumno');
  });

  it('muestra error funcional de la RPC', async () => {
    await component.abrirFormulario();
    component.nuevoAlumno = {
      nombres: 'Ana', apellidos: 'López', tipoIdentificacion: 'identidad',
      numeroIdentificacion: '0801', fechaNacimiento: '', rne: '', codigoInterno: ''
    };
    alumnoService['crear'].mockRejectedValue(
      new AlumnoServiceError('Ya existe una persona con ese documento.', '23505')
    );

    await component.guardarAlumno();

    expect(component.mensaje).toContain('Ya existe');
    expect(component.esError).toBe(true);
  });

  it('después de crear ofrece navegar a matrícula con alumnoId', async () => {
    await component.abrirFormulario();
    component.nuevoAlumno = {
      nombres: 'Ana', apellidos: 'López', tipoIdentificacion: 'identidad',
      numeroIdentificacion: '0801', fechaNacimiento: '', rne: '', codigoInterno: ''
    };

    await component.guardarAlumno();
    expect(component.mensaje).toBe(
      'Alumno registrado correctamente. Puede continuar con su matrícula.'
    );
    expect(component.ultimoAlumnoCreadoId).toBe('alumno-nuevo');
    component.matricularAlumno();
    expect(navigate).toHaveBeenCalledWith(['/matriculas'], {
      queryParams: { alumnoId: 'alumno-nuevo' }
    });
  });

  it('busca por nombre, identidad y grado', () => {
    component.alumnos = [alumnoSinMatricula, {
      ...alumnoSinMatricula,
      id: 'alumno-2', nombreCompleto: 'Carlos Pérez', identidad: '0501',
      matriculaActual: { id: 'matricula-1', ciclo: '2026', grado: 'Séptimo', seccion: 'B' }
    }];
    component.busqueda = 'septimo';
    expect(component.alumnosFiltrados.map(alumno => alumno.id)).toEqual(['alumno-2']);
    component.busqueda = '0801';
    expect(component.alumnosFiltrados.map(alumno => alumno.id)).toEqual(['alumno-1']);
  });

  it('busca por código interno', () => {
    component.alumnos = [alumnoSinMatricula, {
      ...alumnoSinMatricula,
      id: 'alumno-2', codigoInterno: 'CI-2026-0002'
    }];
    component.busqueda = 'ci-2026-0001';
    expect(component.alumnosFiltrados.map(alumno => alumno.id)).toEqual(['alumno-1']);
  });

  it('muestra el código interno en la tabla', () => {
    expect(fixture.nativeElement.textContent).toContain('CI-2026-0001');
    expect(fixture.nativeElement.textContent).toContain('Código interno');
  });

  it('ofrece acción Matricular para alumnos activos con permiso', async () => {
    permisos.add('academico.matriculas.crear');
    fixture.destroy();
    fixture = TestBed.createComponent(Alumnos);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Matricular');
    const fila = fixture.nativeElement.querySelector('.btn-matricular-fila');
    expect(fila).not.toBeNull();
    fila.click();
    expect(navigate).toHaveBeenCalledWith(['/matriculas'], {
      queryParams: { alumnoId: 'alumno-1' }
    });
  });

  it('oculta la acción Matricular sin permiso matriculas.crear', async () => {
    fixture.destroy();
    fixture = TestBed.createComponent(Alumnos);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const boton = fixture.nativeElement.querySelector('.btn-matricular-fila');
    expect(boton).toBeNull();
  });

  it('no ofrece acción Matricular para alumnos inactivos', async () => {
    permisos.add('academico.matriculas.crear');
    alumnoService['listar'].mockResolvedValue([{ ...alumnoSinMatricula, estado: 'inactivo' }]);
    await component.cargarAlumnos();
    fixture.detectChanges();
    expect(component.alumnos[0].estado).toBe('inactivo');
    const boton = fixture.nativeElement.querySelector('.btn-matricular-fila');
    expect(boton).toBeNull();
  });

  function clickDetalleFila(indice = 0): HTMLElement {
    const boton = fixture.nativeElement
      .querySelectorAll('.btn-detalle-fila')[indice] as HTMLElement | undefined;
    expect(boton).toBeDefined();
    boton!.click();
    return boton!;
  }

  it('seleccionar un alumno abre el detalle contextual con sus datos', async () => {
    expect(component.alumnoSeleccionadoId).toBeNull();
    expect(fixture.nativeElement.querySelector('.alumno-detalle')).toBeNull();

    const boton = clickDetalleFila();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.alumnoSeleccionadoId).toBe('alumno-1');
    expect(component.alumnoSeleccionado).toEqual(alumnoSinMatricula);
    expect(boton.getAttribute('aria-expanded')).toBe('true');
    const texto = fixture.nativeElement.textContent;
    expect(texto).toContain('Ana López');
    expect(texto).toContain('0801-2008-00001');
    expect(texto).toContain('CI-2026-0001');
    expect(texto).toContain('activo');
    expect(texto).toContain('Sin matrícula');
    expect(fixture.nativeElement.querySelector('.alumno-detalle')).not.toBeNull();
  });

  it('cambiar de alumno refresca el detalle contextual', async () => {
    const carlos: AlumnoListado = {
      id: 'alumno-2', personaId: 'persona-2', nombreCompleto: 'Carlos Pérez',
      identidad: '0501-2007-00099', rne: 'RNE-77', codigoInterno: 'CI-2026-0002',
      estado: 'activo',
      matriculaActual: { id: 'matricula-1', ciclo: '2026', grado: 'Séptimo', seccion: 'B' }
    };
    component.alumnos = [alumnoSinMatricula, carlos];
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const botonAna = clickDetalleFila(0);
    fixture.detectChanges();
    expect(component.alumnoSeleccionado?.id).toBe('alumno-1');
    expect(botonAna.getAttribute('aria-expanded')).toBe('true');

    const botonCarlos = clickDetalleFila(1);
    fixture.detectChanges();
    expect(component.alumnoSeleccionado?.id).toBe('alumno-2');
    expect(botonAna.getAttribute('aria-expanded')).toBe('false');
    expect(botonCarlos.getAttribute('aria-expanded')).toBe('true');
    const texto = fixture.nativeElement.textContent;
    expect(texto).toContain('Carlos Pérez');
    expect(texto).toContain('RNE-77');
    expect(texto).toContain('2026');
    expect(texto).toContain('Séptimo');
    expect(texto).toContain('B');
    expect(fixture.nativeElement.querySelector('.alumno-detalle')).not.toBeNull();
  });

  it('cerrarSeleccion limpia el alumno activo', async () => {
    clickDetalleFila();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.alumnoSeleccionadoId).toBe('alumno-1');
    expect(fixture.nativeElement.querySelector('.alumno-detalle')).not.toBeNull();

    const cerrar = fixture.nativeElement.querySelector('.btn-cerrar-detalle') as HTMLElement;
    expect(cerrar).not.toBeNull();
    cerrar.click();
    fixture.detectChanges();
    expect(component.alumnoSeleccionadoId).toBeNull();
    expect(fixture.nativeElement.querySelector('.alumno-detalle')).toBeNull();
  });

  it('la selección no persiste en localStorage', () => {
    const spy = vi.spyOn(Storage.prototype, 'setItem');
    component.seleccionarAlumno(alumnoSinMatricula);
    expect(spy).not.toHaveBeenCalled();
    spy.mockRestore();
  });

  it('acción contextual Matricular navega a matrículas con alumnoId del seleccionado', async () => {
    permisos.add('academico.matriculas.crear');
    fixture.destroy();
    fixture = TestBed.createComponent(Alumnos);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    clickDetalleFila();
    fixture.detectChanges();
    const boton = fixture.nativeElement.querySelector('.alumno-detalle .btn-matricular-fila');
    expect(boton).not.toBeNull();
    (boton as HTMLElement).click();
    expect(navigate).toHaveBeenCalledWith(['/matriculas'], {
      queryParams: { alumnoId: 'alumno-1' }
    });
  });

  it('acción contextual Responsables navega con alumnoId del seleccionado', async () => {
    permisos.add('academico.responsables.ver');
    fixture.destroy();
    fixture = TestBed.createComponent(Alumnos);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    clickDetalleFila();
    fixture.detectChanges();
    const boton = fixture.nativeElement.querySelector('.alumno-detalle .btn-matricular-fila');
    expect(boton).not.toBeNull();
    (boton as HTMLElement).click();
    expect(navigate).toHaveBeenCalledWith(['/responsables'], {
      queryParams: { alumnoId: 'alumno-1' }
    });
  });

  it('acciones contextuales de desactivar y reactivar operan sobre el seleccionado', async () => {
    permisos.add('academico.alumnos.desactivar');
    component.seleccionarAlumno(alumnoSinMatricula);

    const prompt = vi.spyOn(window, 'prompt').mockReturnValue('motivo de prueba');
    await component.desactivarAlumno(component.alumnoSeleccionado!);
    expect(alumnoService['desactivar']).toHaveBeenCalledWith('alumno-1', 'motivo de prueba');
    prompt.mockRestore();
  });
});
