import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { AlumnoService } from '../../core/services/alumno.service';
import { CicloEscolarService } from '../../core/services/ciclo-escolar.service';
import { EstructuraAcademicaService } from '../../core/services/estructura-academica.service';
import { Matricula, MatriculaService } from '../../core/services/matriculas.service';
import { Matriculas } from './matriculas';

const MAT: Matricula = {
  id: 'm1', alumnoId: 'a1', institucionId: 'i1', cicloId: 'c1', seccionId: 's1',
  periodoMatriculaId: 'p1', registradoPor: null, fechaMatricula: '2026-09-01',
  estado: 'pendiente', fechaAnulacion: null, motivoAnulacion: null,
  createdAt: '2026-09-01T00:00:00Z', nombreAlumno: 'Ana Pérez',
  nombreSeccion: 'A', nombreGrado: 'Primero', nombreJornada: 'Matutina',
  nombrePeriodo: 'Normal', cicloNombre: '2026'
};

describe('Matriculas', () => {
  const alumnos = [{ id: 'a1', personaId: 'p1', nombreCompleto: 'Ana Pérez', identidad: null, rne: null, estado: 'activo' as const, matriculaActual: null }];
  const ciclos = [{ id: 'c1', institucionId: 'i1', nombre: '2026', activo: true }];
  const secciones = [{ id: 's1', activo: true, nombre: 'A', gradoNombre: 'Primero', jornadaNombre: 'Matutina' }];
  const periodos = [{ id: 'p1', activo: true, nombre: 'Normal' }];

  let f: ComponentFixture<Matriculas>;
  let c: Matriculas;
  let s: Record<string, ReturnType<typeof vi.fn>>;
  let per: Set<string>;
  let consultarParametro: (key: string) => string | null;
  let alumnoMock: { listar: ReturnType<typeof vi.fn>; obtenerPorId: ReturnType<typeof vi.fn> };

  function configurar(): void {
    alumnoMock = {
      listar: vi.fn().mockResolvedValue(alumnos),
      obtenerPorId: vi.fn().mockResolvedValue(alumnos[0])
    };
    TestBed.configureTestingModule({
      imports: [Matriculas],
      providers: [
        { provide: Router, useValue: { navigate: vi.fn() } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: consultarParametro } } } },
        { provide: AuthService, useValue: { tienePermiso: (x: string) => per.has(x) } },
        { provide: MatriculaService, useValue: s },
        { provide: AlumnoService, useValue: alumnoMock },
        { provide: CicloEscolarService, useValue: { listar: vi.fn().mockResolvedValue(ciclos), listarPeriodos: vi.fn().mockResolvedValue(periodos) } },
        { provide: EstructuraAcademicaService, useValue: { listarSecciones: vi.fn().mockResolvedValue(secciones) } }
      ]
    });
  }

  beforeEach(async () => {
    per = new Set(['academico.matriculas.ver', 'academico.matriculas.crear', 'academico.matriculas.cambiar_estado']);
    consultarParametro = () => null;
    s = {
      listar: vi.fn().mockReturnValue(of([MAT])),
      crear: vi.fn().mockReturnValue(of({ id: 'm2' })),
      cambiarEstado: vi.fn().mockReturnValue(of(void 0))
    };
    await TestBed.resetTestingModule();
    configurar();
    await TestBed.compileComponents();
    f = TestBed.createComponent(Matriculas);
    c = f.componentInstance;
    f.detectChanges();
    await vi.waitFor(() => expect(c.cargando).toBe(false));
  });

  it('lista matrículas del servicio', () => {
    expect(s['listar']).toHaveBeenCalledWith(undefined);
    expect(c.matriculas).toEqual([MAT]);
  });

  it('filtra por estado y ciclo', () => {
    const otra: Matricula = { ...MAT, id: 'm2', estado: 'activa' };
    c.matriculas = [MAT, otra];
    c.filtros.estado = 'activa';
    expect(c.matriculasFiltradas).toEqual([otra]);
    c.filtros.estado = '';
    c.filtros.cicloId = 'c1';
    expect(c.matriculasFiltradas).toHaveLength(2);
  });

  it('crea matrícula con sección y período', async () => {
    c.nueva = { alumnoId: 'a1', cicloId: 'c1', seccionId: 's1', periodoMatriculaId: 'p1' };
    await c.crearMatricula();
    expect(s['crear']).toHaveBeenCalledWith({
      alumnoId: 'a1', seccionId: 's1', periodoMatriculaId: 'p1'
    });
    expect(c.mensaje).toContain('registrada');
  });

  it('al seleccionar ciclo carga secciones y períodos activos', async () => {
    c.nueva.cicloId = 'c1';
    await c.alSeleccionarCiclo();
    expect(TestBed.inject(CicloEscolarService)['listarPeriodos']).toHaveBeenCalledWith('c1');
    expect(c.secciones).toHaveLength(1);
    expect(c.periodos).toHaveLength(1);
  });

  it('exige completar todos los campos al crear', async () => {
    c.nueva = { alumnoId: '', cicloId: '', seccionId: '', periodoMatriculaId: '' };
    await c.crearMatricula();
    expect(s['crear']).not.toHaveBeenCalled();
    expect(c.mensaje).toContain('Seleccione');
  });

  it('limita las transiciones según el estado actual', () => {
    c.abrirCambioEstado(MAT);
    expect(c.estadosDisponiblesCambio).toEqual(['activa', 'anulada']);
    expect(c.cambioDe.estado).toBe('activa');

    const activa: Matricula = { ...MAT, estado: 'activa' };
    c.abrirCambioEstado(activa);
    expect(c.estadosDisponiblesCambio).toEqual(['finalizada', 'retirada', 'anulada', 'trasladada']);

    const finalizada: Matricula = { ...MAT, estado: 'finalizada' };
    expect(c.puedeTransicionar(finalizada)).toBe(false);
  });

  it('cambia estado y exige motivo para estados terminales', async () => {
    c.cambioDe = { matricula: MAT, estado: 'anulada', motivo: '' };
    expect(c.requiereMotivo).toBe(true);
    await c.aplicarCambioEstado();
    expect(s['cambiarEstado']).not.toHaveBeenCalled();
    expect(c.mensaje).toContain('motivo');

    c.cambioDe.motivo = 'duplicado';
    await c.aplicarCambioEstado();
    expect(s['cambiarEstado']).toHaveBeenCalledWith('m1', { estado: 'anulada', motivo: 'duplicado' });
    expect(c.cambioDe.matricula).toBeNull();
  });

  it('no exige motivo para estados no terminales', async () => {
    c.cambioDe = { matricula: MAT, estado: 'activa', motivo: '' };
    expect(c.requiereMotivo).toBe(false);
    await c.aplicarCambioEstado();
    expect(s['cambiarEstado']).toHaveBeenCalledWith('m1', { estado: 'activa', motivo: null });
  });

  it('rechaza una transición inválida antes de llamar al backend', async () => {
    c.cambioDe = { matricula: MAT, estado: 'finalizada', motivo: '' };
    await c.aplicarCambioEstado();
    expect(s['cambiarEstado']).not.toHaveBeenCalled();
    expect(c.mensaje).toContain('no es válida');
  });

  it('oculta botones según permisos', () => {
    per.clear();
    expect(c.puedeCrear).toBe(false);
    expect(c.puedeCambiarEstado).toBe(false);
  });

  it('preselecciona alumno y evita cargar la lista completa', async () => {
    consultarParametro = (key: string) => (key === 'alumnoId' ? 'a1' : null);
    await TestBed.resetTestingModule();
    configurar();
    await TestBed.compileComponents();
    f = TestBed.createComponent(Matriculas);
    c = f.componentInstance;
    f.detectChanges();
    await vi.waitFor(() => expect(c.cargando).toBe(false));

    expect(c.nueva.alumnoId).toBe('a1');
    expect(c.filtros.alumnoId).toBe('a1');
    expect(c.mostrarFormulario).toBe(true);
    expect(alumnoMock.obtenerPorId).toHaveBeenCalledWith('a1');
    expect(alumnoMock.listar).not.toHaveBeenCalled();
    expect(s['listar']).toHaveBeenCalledWith('a1');
    expect(c.matriculas).toEqual([MAT]);
  });

  it('no preselecciona ni abre formulario sin permiso crear', async () => {
    per = new Set(['academico.matriculas.ver']);
    consultarParametro = (key: string) => (key === 'alumnoId' ? 'a1' : null);
    await TestBed.resetTestingModule();
    configurar();
    await TestBed.compileComponents();
    f = TestBed.createComponent(Matriculas);
    c = f.componentInstance;
    f.detectChanges();
    await vi.waitFor(() => expect(c.cargando).toBe(false));
    expect(c.nueva.alumnoId).toBe('');
    expect(c.mostrarFormulario).toBe(false);
    expect(alumnoMock.listar).toHaveBeenCalled();
  });

  it('muestra estado vacío cuando no hay matrículas', async () => {
    s = { ...s, listar: vi.fn().mockReturnValue(of([])) };
    await TestBed.resetTestingModule();
    configurar();
    await TestBed.compileComponents();
    f = TestBed.createComponent(Matriculas);
    c = f.componentInstance;
    f.detectChanges();
    await vi.waitFor(() => expect(c.cargando).toBe(false));
    f.detectChanges();
    expect(c.matriculas).toEqual([]);
    expect(f.nativeElement.textContent).toContain('No hay matrículas registradas');
  });

  it('muestra un error seguro cuando falla el listado', async () => {
    s = { ...s, listar: vi.fn().mockReturnValue(throwError(() => new Error('boom'))) };
    await TestBed.resetTestingModule();
    configurar();
    await TestBed.compileComponents();
    f = TestBed.createComponent(Matriculas);
    c = f.componentInstance;
    f.detectChanges();
    await vi.waitFor(() => expect(c.cargando).toBe(false));
    expect(c.matriculas).toEqual([]);
    expect(c.mensaje).toContain('No se pudieron cargar las matrículas');
    expect(c.esError).toBe(true);
  });

  it('limpia el filtro de ciclo obsoleto al recargar', async () => {
    await TestBed.resetTestingModule();
    configurar();
    await TestBed.compileComponents();
    f = TestBed.createComponent(Matriculas);
    c = f.componentInstance;
    c.filtros.cicloId = 'ciclo-inexistente';
    f.detectChanges();
    await vi.waitFor(() => expect(c.cargando).toBe(false));
    expect(c.filtros.cicloId).toBe('');
  });
});
