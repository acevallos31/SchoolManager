import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';
import { AlumnoListado, AlumnoService, AlumnoServiceError } from '../../core/services/alumno.service';
import { AuthService } from '../../core/services/auth';
import { Alumnos } from './alumnos';

describe('Alumnos', () => {
  let component: Alumnos;
  let fixture: ComponentFixture<Alumnos>;
  let alumnoService: Record<string, ReturnType<typeof vi.fn>>;
  let navigate: ReturnType<typeof vi.fn>;
  let permisos: Set<string>;

  const alumnoSinMatricula: AlumnoListado = {
    id: 'alumno-1', personaId: 'persona-1', nombreCompleto: 'Ana López',
    identidad: '0801-2008-00001', rne: null, estado: 'activo', matriculaActual: null
  };

  beforeEach(async () => {
    permisos = new Set([
      'academico.alumnos.ver', 'academico.alumnos.crear',
      'academico.alumnos.editar', 'academico.alumnos.desactivar'
    ]);
    navigate = vi.fn();
    alumnoService = {
      listar: vi.fn().mockResolvedValue([alumnoSinMatricula]),
      crear: vi.fn().mockResolvedValue('alumno-nuevo'),
      desactivar: vi.fn().mockResolvedValue(undefined),
      reactivar: vi.fn().mockResolvedValue(undefined),
      cargarCiclos: vi.fn().mockResolvedValue([
        { id: 'ciclo-1', institucionId: 'institucion-1', nombre: '2026' }
      ])
    };

    await TestBed.configureTestingModule({
      imports: [Alumnos],
      providers: [
        { provide: Router, useValue: { navigate } },
        { provide: AuthService, useValue: { tienePermiso: (p: string) => permisos.has(p) } },
        { provide: AlumnoService, useValue: alumnoService }
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
});
