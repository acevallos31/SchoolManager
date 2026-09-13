import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ContextoInstitucionService } from '../../core/services/contexto-institucion.service';
import {
  PermisoDelegableSeguridad,
  RolInstitucionalSeguridad,
  SeguridadAccesoError,
  SeguridadAccesoService,
  SeguridadAccesoSnapshot
} from '../../core/services/seguridad-acceso.service';

@Component({
  selector: 'app-configuracion-seguridad-acceso',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './configuracion-seguridad-acceso.html',
  styleUrl: './configuracion-seguridad-acceso.css'
})
export class ConfiguracionSeguridadAcceso implements OnInit {
  snapshot: SeguridadAccesoSnapshot | null = null;
  cargando = false;
  guardando = false;
  mensaje = '';
  esError = false;

  nuevoRol = { codigo: '', nombre: '', descripcion: '' };
  clonado = { plantillaCodigo: '', codigo: '', nombre: '', descripcion: '' };
  rolSeleccionadoId = '';
  permisosSeleccionados = new Set<string>();

  constructor(
    private readonly contexto: ContextoInstitucionService,
    private readonly seguridad: SeguridadAccesoService,
    private readonly router: Router,
    private readonly cdr: ChangeDetectorRef
  ) {}

  get institucionId(): string | null {
    return this.contexto.institucionActual()?.id ?? null;
  }

  get institucionNombre(): string {
    return this.contexto.institucionActual()?.nombre ?? 'Sin institución seleccionada';
  }

  get rolSeleccionado(): RolInstitucionalSeguridad | null {
    return this.snapshot?.roles.find(rol => rol.id === this.rolSeleccionadoId) ?? null;
  }

  get puedeCrear(): boolean {
    return this.snapshot?.capacidades.rolesCrear ?? false;
  }

  get puedeEditar(): boolean {
    return this.snapshot?.capacidades.rolesEditar ?? false;
  }

  get puedeAsignarPermisos(): boolean {
    return this.snapshot?.capacidades.rolesAsignarPermisos ?? false;
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
  }

  async cargar(): Promise<void> {
    const institucionId = this.institucionId;
    if (!institucionId) {
      this.snapshot = null;
      this.mostrarError('Seleccione una institución para administrar su seguridad.');
      this.cdr.detectChanges();
      return;
    }

    this.cargando = true;
    this.mensaje = '';
    try {
      this.snapshot = await this.seguridad.obtener(institucionId);
      if (this.rolSeleccionadoId && !this.rolSeleccionado) {
        this.rolSeleccionadoId = '';
        this.permisosSeleccionados.clear();
      }
    } catch (error) {
      this.snapshot = null;
      this.mostrarError(this.mensajeError(error));
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  async crearRol(): Promise<void> {
    const institucionId = this.institucionId;
    if (!institucionId || !this.puedeCrear || this.guardando) return;
    if (!this.nuevoRol.codigo.trim() || !this.nuevoRol.nombre.trim()) {
      this.mostrarError('Código y nombre son obligatorios.');
      return;
    }

    await this.ejecutar(async () => {
      await this.seguridad.crearRol({
        institucionId,
        codigo: this.nuevoRol.codigo.trim().toLowerCase(),
        nombre: this.nuevoRol.nombre.trim(),
        descripcion: this.nuevoRol.descripcion.trim() || null
      });
      this.nuevoRol = { codigo: '', nombre: '', descripcion: '' };
      this.mostrarExito('Rol institucional creado correctamente.');
    });
  }

  async clonarPlantilla(): Promise<void> {
    const institucionId = this.institucionId;
    if (!institucionId || !this.puedeCrear || this.guardando) return;
    if (!this.clonado.plantillaCodigo || !this.clonado.codigo.trim() || !this.clonado.nombre.trim()) {
      this.mostrarError('Seleccione una plantilla e indique código y nombre.');
      return;
    }

    await this.ejecutar(async () => {
      await this.seguridad.clonarPlantilla({
        institucionId,
        plantillaCodigo: this.clonado.plantillaCodigo,
        codigo: this.clonado.codigo.trim().toLowerCase(),
        nombre: this.clonado.nombre.trim(),
        descripcion: this.clonado.descripcion.trim() || null
      });
      this.clonado = { plantillaCodigo: '', codigo: '', nombre: '', descripcion: '' };
      this.mostrarExito('Plantilla clonada como rol institucional.');
    });
  }

  seleccionarRol(rol: RolInstitucionalSeguridad): void {
    this.rolSeleccionadoId = rol.id;
    this.permisosSeleccionados = new Set(rol.permisos);
  }

  alternarPermiso(codigo: string, habilitado: boolean): void {
    if (!this.puedeAsignarPermisos || this.guardando) return;
    const siguiente = new Set(this.permisosSeleccionados);
    if (habilitado) siguiente.add(codigo);
    else siguiente.delete(codigo);
    this.permisosSeleccionados = siguiente;
  }

  permisoSeleccionado(permiso: PermisoDelegableSeguridad): boolean {
    return this.permisosSeleccionados.has(permiso.codigo);
  }

  async guardarPermisos(): Promise<void> {
    const rol = this.rolSeleccionado;
    if (!rol || !this.puedeAsignarPermisos || this.guardando) return;
    await this.ejecutar(async () => {
      await this.seguridad.reemplazarPermisos(rol.id, [...this.permisosSeleccionados]);
      this.mostrarExito('Permisos del rol actualizados.');
    });
  }

  async desactivarRol(rol: RolInstitucionalSeguridad): Promise<void> {
    if (!this.puedeEditar || !rol.activo || rol.protegido || this.guardando) return;
    await this.ejecutar(async () => {
      await this.seguridad.desactivarRol(rol.id, 'Desactivado desde Configuración > Seguridad y acceso');
      if (this.rolSeleccionadoId === rol.id) {
        this.rolSeleccionadoId = '';
        this.permisosSeleccionados.clear();
      }
      this.mostrarExito('Rol desactivado correctamente.');
    });
  }

  volver(): void {
    void this.router.navigate(['/configuracion']);
  }

  private async ejecutar(operacion: () => Promise<void>): Promise<void> {
    this.guardando = true;
    this.mensaje = '';
    try {
      await operacion();
      await this.cargar();
    } catch (error) {
      this.mostrarError(this.mensajeError(error));
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  private mostrarExito(mensaje: string): void {
    this.mensaje = mensaje;
    this.esError = false;
  }

  private mostrarError(mensaje: string): void {
    this.mensaje = mensaje;
    this.esError = true;
  }

  private mensajeError(error: unknown): string {
    return error instanceof SeguridadAccesoError
      ? error.message
      : 'No se pudo completar la operación de seguridad.';
  }
}
