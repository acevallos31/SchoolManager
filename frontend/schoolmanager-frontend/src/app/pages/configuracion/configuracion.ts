import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import { DebugStateService } from '../../core/services/debug-state.service';
import {
  ConfiguracionError,
  ConfiguracionIdentificadores,
  ConfiguracionInstitucion,
  ConfiguracionService,
  GuardarInstitucionInput
} from '../../core/services/configuracion.service';

interface InstitucionForm {
  nombre: string;
  nombreCorto: string;
  direccion: string;
  telefono: string;
  correo: string;
  logoUrl: string;
  identificadores: ConfiguracionIdentificadores;
}

@Component({
  selector: 'app-configuracion',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './configuracion.html',
  styleUrl: './configuracion.css'
})
export class Configuracion implements OnInit {
  configuracion: ConfiguracionInstitucion | null = null;
  formulario = this.formularioVacio();
  cargando = false;
  guardando = false;
  editando = false;
  debugGuardando = false;
  debugMinutos = 60;
  mensaje = '';
  esError = false;
  readonly tiposDisponibles = ['identidad', 'pasaporte', 'otro'];

  constructor(
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly configuracionService: ConfiguracionService,
    readonly debugState: DebugStateService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  get puedeEditarInstitucion(): boolean {
    return this.auth.tienePermiso('configuracion.instituciones.editar');
  }

  get puedeEditarModo(): boolean {
    return this.auth.tienePermiso('configuracion.sistema.editar');
  }

  get puedeUsarDebug(): boolean {
    return this.auth.tienePermiso('sistema.debug.ver');
  }

  get puedeVerCiclos(): boolean {
    return this.auth.tienePermiso('configuracion.ciclos.ver');
  }

  get puedeVerEstructuraAcademica(): boolean {
    return this.auth.tienePermiso('configuracion.grados.ver')
      || this.auth.tienePermiso('configuracion.jornadas.ver')
      || this.auth.tienePermiso('configuracion.secciones.ver');
  }

  get puedeVerConceptosFinancieros(): boolean {
    return this.auth.tienePermiso('configuracion.conceptos_financieros.ver');
  }

  get puedeVerPlanesPago(): boolean {
    return this.auth.tienePermiso('configuracion.planes_pago.ver');
  }

  get creando(): boolean {
    return this.configuracion?.institucion === null;
  }

  async ngOnInit(): Promise<void> {
    await this.cargarConfiguracion();
    if (this.puedeUsarDebug) await this.cargarDebug();
  }

  async cargarDebug(): Promise<void> {
    try {
      await this.configuracionService.obtenerDebug();
    } catch (error) {
      this.mostrarError(error, 'No se pudo cargar el estado del modo debug.');
    } finally {
      this.cdr.detectChanges();
    }
  }

  async cambiarDebug(habilitado: boolean): Promise<void> {
    if (!this.puedeUsarDebug || this.debugGuardando) return;
    this.debugGuardando = true;
    this.mensaje = '';
    try {
      const status = await this.configuracionService.actualizarDebug(habilitado, this.debugMinutos);
      this.mensaje = status.habilitado
        ? `Modo debug habilitado temporalmente hasta ${this.formatearFecha(status.expiraEn)}.`
        : 'Modo debug deshabilitado.';
      this.esError = false;
    } catch (error) {
      this.mostrarError(error, 'No se pudo actualizar el modo debug.');
    } finally {
      this.debugGuardando = false;
      this.cdr.detectChanges();
    }
  }

  async copiarDiagnostico(): Promise<void> {
    const diagnostic = this.debugState.diagnostic;
    if (!diagnostic) return;
    const texto = [
      `Request ID: ${diagnostic.requestId}`,
      `HTTP: ${diagnostic.status}`,
      `Origen: ${diagnostic.source ?? 'desconocido'}`,
      `Endpoint: ${diagnostic.endpoint ?? 'desconocido'}`,
      `SQLSTATE: ${diagnostic.sqlState ?? 'n/a'}`,
      `Constraint: ${diagnostic.constraint ?? 'n/a'}`,
      `Mensaje técnico: ${diagnostic.technicalMessage ?? 'n/a'}`,
      `Timestamp: ${diagnostic.timestamp ?? 'n/a'}`
    ].join('\n');

    try {
      await navigator.clipboard.writeText(texto);
      this.mensaje = 'Diagnóstico copiado al portapapeles.';
      this.esError = false;
    } catch {
      this.mostrarError(null, 'No se pudo copiar el diagnóstico.');
    }
  }

  async cargarConfiguracion(): Promise<void> {
    this.cargando = true;
    this.mensaje = '';
    try {
      this.configuracion = await this.configuracionService.obtenerConfiguracionInstitucion();
    } catch (error) {
      if (error instanceof ConfiguracionError && error.code === 'SM003') {
        this.configuracion = {
          multiplesInstituciones: true,
          institucion: null,
          identificadores: null
        };
      }
      this.mostrarError(error, 'No se pudo cargar la configuración.');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  iniciarEdicion(): void {
    if (!this.puedeEditarInstitucion || this.guardando) return;
    const institucion = this.configuracion?.institucion;
    const identificadores = this.configuracion?.identificadores;
    this.formulario = institucion && identificadores
      ? {
          nombre: institucion.nombre,
          nombreCorto: institucion.nombreCorto ?? '',
          direccion: institucion.direccion ?? '',
          telefono: institucion.telefono ?? '',
          correo: institucion.correo ?? '',
          logoUrl: institucion.logoUrl ?? '',
          identificadores: {
            ...identificadores,
            tiposIdentificacionPermitidos: [...identificadores.tiposIdentificacionPermitidos]
          }
        }
      : this.formularioVacio();
    this.editando = true;
    this.mensaje = '';
  }

  cancelarEdicion(): void {
    if (this.guardando) return;
    this.editando = false;
    this.formulario = this.formularioVacio();
    this.mensaje = '';
  }

  async guardarInstitucion(): Promise<void> {
    if (!this.editando || !this.puedeEditarInstitucion || this.guardando) return;
    if (!this.formulario.nombre.trim()) {
      this.mostrarError(null, 'El nombre del centro educativo es obligatorio.');
      return;
    }

    this.guardando = true;
    this.mensaje = '';
    try {
      const input: GuardarInstitucionInput = {
        ...this.formulario,
        nombreCorto: this.formulario.nombreCorto || null,
        direccion: this.formulario.direccion || null,
        telefono: this.formulario.telefono || null,
        correo: this.formulario.correo || null,
        logoUrl: this.formulario.logoUrl || null
      };
      const institucionId = this.configuracion?.institucion?.id;
      this.configuracion = institucionId
        ? await this.configuracionService.actualizarInstitucion(institucionId, input)
        : await this.configuracionService.crearInstitucion(input);
      this.editando = false;
      this.mensaje = institucionId
        ? 'Centro educativo actualizado correctamente.'
        : 'Centro educativo configurado correctamente.';
      this.esError = false;
      this.cdr.detectChanges();
    } catch (error) {
      this.mostrarError(error, 'No se pudo guardar el centro educativo.');
      this.cdr.detectChanges();
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  async cambiarModo(habilitado: boolean): Promise<void> {
    if (!this.puedeEditarModo || this.guardando || !this.configuracion) return;
    const anterior = this.configuracion.multiplesInstituciones;
    this.configuracion = { ...this.configuracion, multiplesInstituciones: habilitado };
    this.guardando = true;
    this.mensaje = '';
    try {
      await this.configuracionService.actualizarModo(habilitado);
      await this.cargarConfiguracion();
      if (!this.esError) this.mensaje = 'Configuración actualizada correctamente.';
    } catch (error) {
      this.configuracion = { ...this.configuracion, multiplesInstituciones: anterior };
      this.mostrarError(error, 'No se pudo actualizar el modo de instituciones.');
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  alternarTipo(tipo: string, habilitado: boolean): void {
    const actuales = this.formulario.identificadores.tiposIdentificacionPermitidos;
    this.formulario.identificadores.tiposIdentificacionPermitidos = habilitado
      ? [...new Set([...actuales, tipo])]
      : actuales.filter(valor => valor !== tipo);
  }

  tipoSeleccionado(tipo: string): boolean {
    return this.formulario.identificadores.tiposIdentificacionPermitidos.includes(tipo);
  }

  volver(): void {
    void this.router.navigate(['/dashboard']);
  }

  formatearFecha(value: string | null | undefined): string {
    return value ? new Date(value).toLocaleString() : '—';
  }

  private formularioVacio(): InstitucionForm {
    return {
      nombre: '', nombreCorto: '', direccion: '', telefono: '', correo: '', logoUrl: '',
      identificadores: {
        rneRequerido: false,
        identificacionCivilRequerida: false,
        codigoInternoRequerido: false,
        tiposIdentificacionPermitidos: ['identidad', 'pasaporte', 'otro']
      }
    };
  }

  private mostrarError(error: unknown, fallback: string): void {
    this.mensaje = error instanceof ConfiguracionError ? error.message : fallback;
    this.esError = true;
  }
}
