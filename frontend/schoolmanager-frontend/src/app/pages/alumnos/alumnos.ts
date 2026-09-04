import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AlumnoListado,
  AlumnoService,
  AlumnoServiceError
} from '../../core/services/alumno.service';
import { AuthService } from '../../core/services/auth';
import {
  ConfiguracionError,
  ConfiguracionIdentificadores,
  ConfiguracionService
} from '../../core/services/configuracion.service';

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
  configuracionIdentificadores: ConfiguracionIdentificadores | null = null;
  nuevoAlumno = this.crearFormularioVacio();

  constructor(
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly alumnoService: AlumnoService,
    private readonly configuracionService: ConfiguracionService
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

  get puedeMatricular(): boolean {
    return this.auth.tienePermiso('academico.matriculas.crear');
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
      const configuracion = await this.configuracionService.obtenerConfiguracionInstitucion();
      if (!configuracion.institucion || !configuracion.identificadores) {
        throw new ConfiguracionError(
          'No hay un centro educativo configurado.',
          'INSTITUTION_CONTEXT_REQUIRED'
        );
      }
      this.institucionCreacionId = configuracion.institucion.id;
      this.configuracionIdentificadores = configuracion.identificadores;
      const tipos = configuracion.identificadores.tiposIdentificacionPermitidos;
      if (tipos.length > 0 && !tipos.includes(this.nuevoAlumno.tipoIdentificacion)) {
        this.nuevoAlumno.tipoIdentificacion = tipos[0];
      }
    } catch (error) {
      this.institucionCreacionId = null;
      this.configuracionIdentificadores = null;
      this.mostrarError(error, 'No se pudo determinar la institución del alumno.');
    }
  }

  cerrarFormulario(): void {
    if (this.guardando) return;
    this.mostrarFormulario = false;
    this.nuevoAlumno = this.crearFormularioVacio();
    this.institucionCreacionId = null;
    this.configuracionIdentificadores = null;
  }

  async guardarAlumno(): Promise<void> {
    if (this.guardando || !this.puedeCrear) return;

    if (
      !this.nuevoAlumno.nombres.trim() ||
      !this.nuevoAlumno.apellidos.trim() ||
      !this.nuevoAlumno.tipoIdentificacion.trim() ||
      !this.nuevoAlumno.numeroIdentificacion.trim() ||
      (this.configuracionIdentificadores?.rneRequerido && !this.nuevoAlumno.rne.trim()) ||
      (this.configuracionIdentificadores?.codigoInternoRequerido && !this.nuevoAlumno.codigoInterno.trim()) ||
      !this.institucionCreacionId
    ) {
      this.mostrarMensaje(
        'Complete los campos obligatorios para registrar el alumno.',
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
      this.configuracionIdentificadores = null;
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
      this.matricularAlumnoId(this.ultimoAlumnoCreadoId);
    }
  }

  matricularAlumnoId(alumnoId: string): void {
    void this.router.navigate(['/matriculas'], {
      queryParams: { alumnoId }
    });
  }

  get puedeVerResponsables(): boolean {
    return this.auth.tienePermiso('academico.responsables.ver');
  }

  verResponsables(alumno: AlumnoListado): void {
    void this.router.navigate(['/responsables'], {
      queryParams: { alumnoId: alumno.id }
    });
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

  private normalizar(valor: string): string {
    return valor.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
  }

  private mostrarError(error: unknown, fallback: string): void {
    this.mostrarMensaje(
      error instanceof AlumnoServiceError || error instanceof ConfiguracionError
        ? error.message
        : fallback,
      true
    );
  }

  private mostrarMensaje(mensaje: string, esError = false): void {
    this.mensaje = mensaje;
    this.esError = esError;
  }
}
