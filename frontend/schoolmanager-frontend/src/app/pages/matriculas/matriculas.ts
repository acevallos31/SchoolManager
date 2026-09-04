import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import { AlumnoListado, AlumnoService } from '../../core/services/alumno.service';
import {
  CicloEscolar,
  CicloEscolarService,
  PeriodoMatricula
} from '../../core/services/ciclo-escolar.service';
import {
  EstructuraAcademicaService,
  Seccion
} from '../../core/services/estructura-academica.service';
import {
  ESTADOS_MATRICULA,
  ESTADOS_TERMINALES,
  EstadoMatricula,
  Matricula,
  MatriculaError,
  MatriculaService
} from '../../core/services/matriculas.service';

interface FiltrosMatriculas {
  alumnoId: string;
  cicloId: string;
  estado: '' | EstadoMatricula;
}

interface NuevaMatricula {
  alumnoId: string;
  cicloId: string;
  seccionId: string;
  periodoMatriculaId: string;
}

interface CambioEstado {
  matricula: Matricula | null;
  estado: '' | EstadoMatricula;
  motivo: string;
}

interface CicloDisponible {
  id: string;
  nombre: string;
}

const TRANSICIONES_ESTADO: Readonly<Record<EstadoMatricula, readonly EstadoMatricula[]>> = {
  pendiente: ['activa', 'anulada'],
  activa: ['finalizada', 'retirada', 'anulada', 'trasladada'],
  finalizada: [],
  retirada: [],
  anulada: [],
  trasladada: []
};

@Component({
  selector: 'app-matriculas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './matriculas.html',
  styleUrl: './matriculas.css'
})
export class Matriculas implements OnInit {
  matriculas: Matricula[] = [];
  alumnos: AlumnoListado[] = [];
  ciclos: CicloEscolar[] = [];
  secciones: Seccion[] = [];
  periodos: PeriodoMatricula[] = [];

  filtros: FiltrosMatriculas = { alumnoId: '', cicloId: '', estado: '' };
  nueva: NuevaMatricula = { alumnoId: '', cicloId: '', seccionId: '', periodoMatriculaId: '' };
  cambioDe: CambioEstado = { matricula: null, estado: '', motivo: '' };

  mostrarFormulario = false;
  cargando = false;
  guardando = false;
  mensaje = '';
  esError = false;
  readonly estados = ESTADOS_MATRICULA;

  constructor(
    private readonly router: Router,
    private readonly route: ActivatedRoute,
    private readonly auth: AuthService,
    private readonly matriculaService: MatriculaService,
    private readonly alumnoService: AlumnoService,
    private readonly cicloService: CicloEscolarService,
    private readonly estructuraService: EstructuraAcademicaService
  ) {}

  get puedeVer(): boolean {
    return this.auth.tienePermiso('academico.matriculas.ver');
  }

  get puedeCrear(): boolean {
    return this.auth.tienePermiso('academico.matriculas.crear');
  }

  get puedeCambiarEstado(): boolean {
    return this.auth.tienePermiso('academico.matriculas.cambiar_estado');
  }

  get matriculasFiltradas(): Matricula[] {
    const { alumnoId, cicloId, estado } = this.filtros;
    return this.matriculas.filter(m =>
      (!alumnoId || m.alumnoId === alumnoId) &&
      (!cicloId || m.cicloId === cicloId) &&
      (!estado || m.estado === estado)
    );
  }

  get ciclosDisponibles(): CicloDisponible[] {
    const vistos = new Map<string, CicloDisponible>();
    for (const m of this.matriculas) {
      if (m.cicloId && !vistos.has(m.cicloId)) {
        vistos.set(m.cicloId, { id: m.cicloId, nombre: m.cicloNombre });
      }
    }
    return [...vistos.values()];
  }

  get estadosDisponiblesCambio(): readonly EstadoMatricula[] {
    const estadoActual = this.cambioDe.matricula?.estado;
    return estadoActual ? TRANSICIONES_ESTADO[estadoActual] : [];
  }

  async ngOnInit(): Promise<void> {
    const alumnoId = this.route.snapshot.queryParamMap.get('alumnoId');
    const alumnoPreseleccionado = alumnoId && this.puedeCrear ? alumnoId : null;

    if (alumnoPreseleccionado) {
      this.nueva.alumnoId = alumnoPreseleccionado;
      this.filtros.alumnoId = alumnoPreseleccionado;
      this.mostrarFormulario = true;
    }

    await this.cargarDatosIniciales(alumnoPreseleccionado);
  }

  async cargarDatosIniciales(alumnoId?: string | null): Promise<void> {
    this.cargando = true;
    try {
      if (alumnoId) {
        const [alumno, ciclos] = await Promise.all([
          this.alumnoService.obtenerPorId(alumnoId),
          this.cicloService.listar()
        ]);
        this.alumnos = alumno?.estado === 'activo' ? [alumno] : [];
        this.ciclos = ciclos;
      } else {
        const [alumnos, ciclos] = await Promise.all([
          this.alumnoService.listar(),
          this.cicloService.listar()
        ]);
        this.alumnos = alumnos.filter(a => a.estado === 'activo');
        this.ciclos = ciclos;
      }

      if (this.puedeVer) await this.cargarMatriculas(alumnoId ?? undefined);
    } finally {
      this.cargando = false;
    }
  }

  async cargarMatriculas(alumnoId?: string): Promise<void> {
    this.matriculas = [];
    await new Promise<void>((resolve) => {
      this.matriculaService.listar(alumnoId).subscribe({
        next: (data) => {
          this.matriculas = data;
          resolve();
        },
        error: (err) => {
          this.mostrarError(err, 'No se pudieron cargar las matrículas.');
          resolve();
        }
      });
    });
    this.validarFiltroCiclo();
    this.validarFiltroAlumno();
  }

  async alSeleccionarCiclo(): Promise<void> {
    this.secciones = [];
    this.periodos = [];
    this.nueva.seccionId = '';
    this.nueva.periodoMatriculaId = '';
    if (!this.nueva.cicloId) return;
    try {
      const [secciones, periodos] = await Promise.all([
        this.estructuraService.listarSecciones(this.nueva.cicloId),
        this.cicloService.listarPeriodos(this.nueva.cicloId)
      ]);
      this.secciones = secciones.filter(s => s.activo);
      this.periodos = periodos.filter(p => p.activo);
    } catch {
      this.mostrarError(null, 'No se pudieron cargar las secciones o períodos del ciclo.');
    }
  }

  abrirFormulario(): void {
    this.mostrarFormulario = true;
  }

  cerrarFormulario(): void {
    this.mostrarFormulario = false;
    this.nueva = { alumnoId: '', cicloId: '', seccionId: '', periodoMatriculaId: '' };
    this.secciones = [];
    this.periodos = [];
  }

  async crearMatricula(): Promise<void> {
    if (!this.nueva.alumnoId || !this.nueva.cicloId || !this.nueva.seccionId || !this.nueva.periodoMatriculaId) {
      this.mostrarMensaje('Seleccione alumno, ciclo, sección y período.', true);
      return;
    }
    this.guardando = true;
    await new Promise<void>((resolve) => {
      this.matriculaService.crear({
        alumnoId: this.nueva.alumnoId,
        seccionId: this.nueva.seccionId,
        periodoMatriculaId: this.nueva.periodoMatriculaId
      }).subscribe({
        next: () => {
          this.mostrarMensaje('✅ Matrícula registrada correctamente.');
          this.cerrarFormulario();
          resolve();
        },
        error: (err) => {
          this.mostrarError(err, 'No se pudo registrar la matrícula.');
          resolve();
        }
      });
    });
    this.guardando = false;
    await this.cargarMatriculas(this.filtros.alumnoId || undefined);
  }

  puedeTransicionar(m: Matricula): boolean {
    return TRANSICIONES_ESTADO[m.estado].length > 0;
  }

  abrirCambioEstado(m: Matricula): void {
    const opciones = TRANSICIONES_ESTADO[m.estado];
    this.cambioDe = { matricula: m, estado: opciones[0] ?? '', motivo: '' };
  }

  cerrarCambioEstado(): void {
    this.cambioDe = { matricula: null, estado: '', motivo: '' };
  }

  get requiereMotivo(): boolean {
    return this.cambioDe.estado !== '' && ESTADOS_TERMINALES.includes(this.cambioDe.estado);
  }

  async aplicarCambioEstado(): Promise<void> {
    const m = this.cambioDe.matricula;
    const nuevoEstado = this.cambioDe.estado;
    if (!m || !nuevoEstado) return;
    if (!TRANSICIONES_ESTADO[m.estado].includes(nuevoEstado)) {
      this.mostrarMensaje('La transición de estado seleccionada no es válida.', true);
      return;
    }
    if (this.requiereMotivo && !this.cambioDe.motivo.trim()) {
      this.mostrarMensaje('El motivo es obligatorio para este estado.', true);
      return;
    }
    const confirmar = m.estado === 'activa' && ['anulada', 'retirada', 'trasladada'].includes(nuevoEstado)
      ? window.confirm(`¿Confirmar estado "${nuevoEstado}" para ${m.nombreAlumno}?`)
      : true;
    if (!confirmar) return;

    this.guardando = true;
    await new Promise<void>((resolve) => {
      this.matriculaService.cambiarEstado(m.id, {
        estado: nuevoEstado,
        motivo: this.requiereMotivo ? this.cambioDe.motivo.trim() : null
      }).subscribe({
        next: () => {
          this.mostrarMensaje(`✅ Estado actualizado a "${nuevoEstado}".`);
          this.cerrarCambioEstado();
          resolve();
        },
        error: (err) => {
          this.mostrarError(err, 'No se pudo cambiar el estado de la matrícula.');
          resolve();
        }
      });
    });
    this.guardando = false;
    await this.cargarMatriculas(this.filtros.alumnoId || undefined);
  }

  claseEstado(estado: string): string {
    return `badge-estado badge-${estado}`;
  }

  volver(): void {
    void this.router.navigate(['/dashboard']);
  }

  private validarFiltroCiclo(): void {
    if (this.filtros.cicloId && !this.ciclosDisponibles.some(c => c.id === this.filtros.cicloId)) {
      this.filtros.cicloId = '';
    }
  }

  private validarFiltroAlumno(): void {
    if (this.filtros.alumnoId && this.puedeVer) {
      const existe = this.matriculas.some(m => m.alumnoId === this.filtros.alumnoId) || this.alumnos.some(a => a.id === this.filtros.alumnoId);
      if (!existe) this.filtros.alumnoId = '';
    }
  }

  private mostrarError(err: unknown, fallback: string): void {
    this.mostrarMensaje(
      err instanceof MatriculaError ? err.message : fallback,
      true
    );
  }

  private mostrarMensaje(mensaje: string, esError = false): void {
    this.mensaje = mensaje;
    this.esError = esError;
    setTimeout(() => {
      this.mensaje = '';
      this.esError = false;
    }, 4000);
  }
}
