import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AlumnoListado,
  AlumnoService,
  AlumnoServiceError,
  CicloEscolarOpcion
} from '../../core/services/alumno.service';
import { AuthService } from '../../core/services/auth';

interface NuevoAlumnoForm {
  nombres: string;
  apellidos: string;
  tipoIdentificacion: string;
  numeroIdentificacion: string;
  fechaNacimiento: string;
  rne: string;
  codigoInterno: string;
}

@Component({
  selector: 'app-alumnos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './alumnos.html',
  styleUrl: './alumnos.css'
})
export class Alumnos implements OnInit {
  alumnos: AlumnoListado[] = [];
  mostrarFormulario = false;
  busqueda = '';
  cargando = false;
  guardando = false;
  mensaje = '';
  esError = false;
  ultimoAlumnoCreadoId: string | null = null;
  private institucionCreacionId: string | null = null;
  nuevoAlumno = this.crearFormularioVacio();

  constructor(
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly alumnoService: AlumnoService
  ) {}

  get puedeVer(): boolean {
    return this.auth.tienePermiso('academico.alumnos.ver');
  }

  get puedeCrear(): boolean {
    return this.auth.tienePermiso('academico.alumnos.crear');
  }

  get puedeEditar(): boolean {
    return this.auth.tienePermiso('academico.alumnos.editar');
  }

  get puedeDesactivar(): boolean {
    return this.auth.tienePermiso('academico.alumnos.desactivar');
  }

  get alumnosFiltrados(): AlumnoListado[] {
    const termino = this.normalizar(this.busqueda);
    if (!termino) return this.alumnos;

    return this.alumnos.filter(alumno => {
      const matricula = alumno.matriculaActual;
      return [
        alumno.nombreCompleto,
        alumno.identidad,
        alumno.rne,
        matricula?.grado,
        matricula?.seccion,
        matricula?.ciclo
      ].some(valor => this.normalizar(valor ?? '').includes(termino));
    });
  }

  async ngOnInit(): Promise<void> {
    if (!this.puedeVer) {
      this.mostrarMensaje('No tienes permiso para consultar alumnos.', true);
      return;
    }
    await this.cargarAlumnos();
  }

  async cargarAlumnos(): Promise<void> {
    this.cargando = true;
    try {
      this.alumnos = await this.alumnoService.listar();
    } catch (error) {
      this.mostrarError(error, 'No se pudo cargar la lista de alumnos.');
    } finally {
      this.cargando = false;
    }
  }

  async abrirFormulario(): Promise<void> {
    if (!this.puedeCrear || this.guardando) return;

    this.mostrarFormulario = true;
    this.mensaje = '';
    try {
      const ciclos = await this.alumnoService.cargarCiclos();
      this.institucionCreacionId = this.resolverInstitucionUnica(ciclos);
      if (!this.institucionCreacionId) {
        this.mostrarMensaje(
          'No se pudo determinar una única institución para registrar el alumno.',
          true
        );
      }
    } catch (error) {
      this.institucionCreacionId = null;
      this.mostrarError(error, 'No se pudo determinar la institución del alumno.');
    }
  }

  cerrarFormulario(): void {
    if (this.guardando) return;
    this.mostrarFormulario = false;
    this.nuevoAlumno = this.crearFormularioVacio();
    this.institucionCreacionId = null;
  }

  async guardarAlumno(): Promise<void> {
    if (this.guardando || !this.puedeCrear) return;

    if (
      !this.nuevoAlumno.nombres.trim() ||
      !this.nuevoAlumno.apellidos.trim() ||
      !this.nuevoAlumno.tipoIdentificacion.trim() ||
      !this.nuevoAlumno.numeroIdentificacion.trim() ||
      !this.institucionCreacionId
    ) {
      this.mostrarMensaje(
        'Nombres, apellidos, tipo y número de documento son obligatorios.',
        true
      );
      return;
    }

    this.guardando = true;
    try {
      this.ultimoAlumnoCreadoId = await this.alumnoService.crear({
        institucionId: this.institucionCreacionId,
        nombres: this.nuevoAlumno.nombres,
        apellidos: this.nuevoAlumno.apellidos,
        tipoIdentificacion: this.nuevoAlumno.tipoIdentificacion,
        numeroIdentificacion: this.nuevoAlumno.numeroIdentificacion,
        fechaNacimiento: this.nuevoAlumno.fechaNacimiento || null,
        rne: this.nuevoAlumno.rne || null,
        codigoInterno: this.nuevoAlumno.codigoInterno || null
      });
      this.mostrarMensaje(
        'Alumno registrado correctamente. Puede continuar con su matrícula.'
      );
      this.mostrarFormulario = false;
      this.nuevoAlumno = this.crearFormularioVacio();
      this.institucionCreacionId = null;
      await this.cargarAlumnos();
    } catch (error) {
      this.mostrarError(error, 'No se pudo crear el alumno.');
    } finally {
      this.guardando = false;
    }
  }

  async desactivarAlumno(alumno: AlumnoListado): Promise<void> {
    if (!this.puedeDesactivar || this.guardando) return;
    const motivo = window.prompt(`Motivo para desactivar a ${alumno.nombreCompleto}:`)?.trim();
    if (!motivo) return;

    await this.ejecutarCambioEstado(
      () => this.alumnoService.desactivar(alumno.id, motivo),
      'Alumno desactivado correctamente.'
    );
  }

  async reactivarAlumno(alumno: AlumnoListado): Promise<void> {
    if (!this.puedeEditar || this.guardando || !window.confirm('¿Reactivar este alumno?')) return;
    await this.ejecutarCambioEstado(
      () => this.alumnoService.reactivar(alumno.id),
      'Alumno reactivado correctamente.'
    );
  }

  volver(): void {
    void this.router.navigate(['/dashboard']);
  }

  matricularAlumno(): void {
    if (this.ultimoAlumnoCreadoId) {
      void this.router.navigate(['/matriculas'], {
        queryParams: { alumnoId: this.ultimoAlumnoCreadoId }
      });
    }
  }

  private async ejecutarCambioEstado(operacion: () => Promise<void>, exito: string): Promise<void> {
    this.guardando = true;
    try {
      await operacion();
      this.mostrarMensaje(exito);
      await this.cargarAlumnos();
    } catch (error) {
      this.mostrarError(error, 'No se pudo cambiar el estado del alumno.');
    } finally {
      this.guardando = false;
    }
  }

  private crearFormularioVacio(): NuevoAlumnoForm {
    return {
      nombres: '', apellidos: '', tipoIdentificacion: 'identidad',
      numeroIdentificacion: '', fechaNacimiento: '', rne: '', codigoInterno: ''
    };
  }

  private resolverInstitucionUnica(ciclos: CicloEscolarOpcion[]): string | null {
    const instituciones = [...new Set(ciclos.map(ciclo => ciclo.institucionId))];
    return instituciones.length === 1 ? instituciones[0] : null;
  }

  private normalizar(valor: string): string {
    return valor.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }

  private mostrarError(error: unknown, fallback: string): void {
    this.mostrarMensaje(error instanceof AlumnoServiceError ? error.message : fallback, true);
  }

  private mostrarMensaje(mensaje: string, esError = false): void {
    this.mensaje = mensaje;
    this.esError = esError;
  }
}
