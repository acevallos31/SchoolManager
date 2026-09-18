import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { skip, Subscription } from 'rxjs';
import { ContextoInstitucionService } from '../../core/services/contexto-institucion.service';
import {
  AsignacionRolSeguridad,
  PermisoDelegableSeguridad,
  RolInstitucionalSeguridad,
  SeguridadAccesoError,
  SeguridadAccesoService,
  SeguridadAccesoSnapshot,
  UsuarioRolSeguridad,
  UsuarioSeguridad
} from '../../core/services/seguridad-acceso.service';

@Component({
  selector: 'app-configuracion-seguridad-acceso',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './configuracion-seguridad-acceso.html',
  styleUrl: './configuracion-seguridad-acceso.css'
})
export class ConfiguracionSeguridadAcceso implements OnInit, OnDestroy {
  snapshot: SeguridadAccesoSnapshot | null = null;
  usuarios: UsuarioSeguridad[] = [];
  cargando = false;
  guardando = false;
  mensaje = '';
  esError = false;
  filtroUsuario = '';
  rolPorUsuario: Record<string, string> = {};

  nuevoUsuario = { nombres: '', apellidos: '', correo: '', rolId: '' };
  usuarioEditandoId = '';
  edicionUsuario = { nombres: '', apellidos: '', correo: '' };
  nuevoRol = { codigo: '', nombre: '', descripcion: '' };
  clonado = { plantillaCodigo: '', codigo: '', nombre: '', descripcion: '' };
  edicionRol = { nombre: '', descripcion: '' };
  rolSeleccionadoId = '';
  permisosSeleccionados = new Set<string>();
  private contextoSubscription: Subscription | null = null;

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

  get puedeGestionarAsignaciones(): boolean {
    return this.snapshot?.capacidades.usuariosAsignarRoles ?? false;
  }

  get puedePrepararInvitacion(): boolean {
    return (this.snapshot?.capacidades.usuariosVer ?? false)
      && this.puedeGestionarAsignaciones
      && this.rolesActivos.length > 0;
  }

  get rolesActivos(): RolInstitucionalSeguridad[] {
    return (this.snapshot?.roles ?? []).filter(rol => rol.activo);
  }

  get usuariosFiltrados(): UsuarioSeguridad[] {
    const filtro = this.filtroUsuario.trim().toLocaleLowerCase();
    if (!filtro) return this.usuarios;
    return this.usuarios.filter(usuario =>
      usuario.nombre.toLocaleLowerCase().includes(filtro)
      || (usuario.correo?.toLocaleLowerCase().includes(filtro) ?? false)
    );
  }

  async ngOnInit(): Promise<void> {
    await this.cargar();
    this.contextoSubscription = this.contexto.institucionActual$
      .pipe(skip(1))
      .subscribe(() => {
        this.limpiarSeleccionRol();
        this.cancelarEdicionUsuario();
        this.nuevoUsuario = { nombres: '', apellidos: '', correo: '', rolId: '' };
        this.usuarios = [];
        this.filtroUsuario = '';
        this.rolPorUsuario = {};
        void this.cargar();
      });
  }

  ngOnDestroy(): void {
    this.contextoSubscription?.unsubscribe();
  }

  async cargar(preservarMensaje = false): Promise<void> {
    const institucionId = this.institucionId;
    if (!institucionId) {
      this.snapshot = null;
      this.usuarios = [];
      this.mostrarError('Seleccione una institución para administrar su seguridad.');
      this.cdr.detectChanges();
      return;
    }

    this.cargando = true;
    if (!preservarMensaje) this.mensaje = '';
    try {
      this.snapshot = await this.seguridad.obtener(institucionId);
      const rol = this.rolSeleccionado;
      if (this.rolSeleccionadoId && !rol) {
        this.limpiarSeleccionRol();
      } else if (rol) {
        this.edicionRol = { nombre: rol.nombre, descripcion: rol.descripcion ?? '' };
      }

      if (this.snapshot.capacidades.usuariosVer) {
        try {
          this.usuarios = await this.seguridad.obtenerUsuarios(institucionId);
          if (this.usuarioEditandoId && !this.usuarios.some(usuario => usuario.id === this.usuarioEditandoId)) {
            this.cancelarEdicionUsuario();
          }
        } catch (error) {
          this.usuarios = [];
          this.mostrarError(this.mensajeError(error));
        }
      } else {
        this.usuarios = [];
      }
    } catch (error) {
      this.snapshot = null;
      this.usuarios = [];
      this.mostrarError(this.mensajeError(error));
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  async prepararNuevoUsuario(): Promise<void> {
    const institucionId = this.institucionId;
    if (!institucionId || !this.puedePrepararInvitacion || this.guardando) return;

    const nombres = this.nuevoUsuario.nombres.trim();
    const apellidos = this.nuevoUsuario.apellidos.trim();
    const correo = this.nuevoUsuario.correo.trim().toLowerCase();
    const rolId = this.nuevoUsuario.rolId;

    if (!nombres || !apellidos || !correo || !rolId) {
      this.mostrarError('Nombre, apellido, correo y rol inicial son obligatorios.');
      return;
    }

    await this.ejecutar(async () => {
      const respuesta = await this.seguridad.prepararInvitacionUsuario({
        institucionId,
        nombres,
        apellidos,
        correo,
        rolId,
        origen: 'administracion'
      });
      this.nuevoUsuario = { nombres: '', apellidos: '', correo: '', rolId: '' };
      this.mostrarExito(
        respuesta.invitacionCreada
          ? 'Usuario preparado e invitación pendiente creada correctamente.'
          : 'El usuario ya tenía una invitación pendiente; se reutilizó sin duplicarla.'
      );
    });
  }

  editarUsuario(usuario: UsuarioSeguridad): void {
    if (!usuario.puedeEditar || this.guardando) return;
    this.usuarioEditandoId = usuario.id;
    this.edicionUsuario = {
      nombres: usuario.nombres ?? '',
      apellidos: usuario.apellidos ?? '',
      correo: usuario.correo ?? ''
    };
  }

  cancelarEdicionUsuario(): void {
    this.usuarioEditandoId = '';
    this.edicionUsuario = { nombres: '', apellidos: '', correo: '' };
  }

  async guardarUsuario(): Promise<void> {
    const institucionId = this.institucionId;
    const usuario = this.usuarios.find(item => item.id === this.usuarioEditandoId);
    if (!institucionId || !usuario?.puedeEditar || this.guardando) return;

    const nombres = this.edicionUsuario.nombres.trim();
    const apellidos = this.edicionUsuario.apellidos.trim();
    const correo = this.edicionUsuario.correo.trim().toLowerCase();
    if (!nombres || !apellidos) {
      this.mostrarError('Nombres y apellidos son obligatorios.');
      return;
    }

    await this.ejecutar(async () => {
      await this.seguridad.editarUsuario(usuario.id, {
        institucionId,
        nombres,
        apellidos,
        correo: correo || null
      });
      this.cancelarEdicionUsuario();
      this.mostrarExito('Datos del usuario actualizados en SchoolManager. La identidad Google/Microsoft no fue modificada.');
    });
  }

  async aprobarVinculacion(usuario: UsuarioSeguridad): Promise<void> {
    const institucionId = this.institucionId;
    if (!institucionId || !usuario.puedeEditar || !usuario.solicitudVinculacionId || this.guardando) return;

    await this.ejecutar(async () => {
      await this.seguridad.operarVinculacion(usuario.id, {
        institucionId,
        invitacionId: usuario.solicitudVinculacionId!,
        operacion: 'aprobar'
      });
      this.mostrarExito(`Identidad de ${usuario.nombre || 'el usuario'} vinculada correctamente.`);
    });
  }

  async rechazarVinculacion(usuario: UsuarioSeguridad): Promise<void> {
    const institucionId = this.institucionId;
    if (!institucionId || !usuario.puedeEditar || !usuario.solicitudVinculacionId || this.guardando) return;

    await this.ejecutar(async () => {
      await this.seguridad.operarVinculacion(usuario.id, {
        institucionId,
        invitacionId: usuario.solicitudVinculacionId!,
        operacion: 'rechazar',
        motivo: 'Rechazada desde Configuración > Seguridad y acceso'
      });
      this.mostrarExito(`Solicitud de identidad de ${usuario.nombre || 'el usuario'} rechazada.`);
    });
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
    this.edicionRol = { nombre: rol.nombre, descripcion: rol.descripcion ?? '' };
  }

  async guardarRol(): Promise<void> {
    const rol = this.rolSeleccionado;
    if (!rol || !this.puedeEditar || !rol.activo || rol.protegido || this.guardando) return;
    const nombre = this.edicionRol.nombre.trim();
    if (!nombre) {
      this.mostrarError('El nombre del rol es obligatorio.');
      return;
    }

    await this.ejecutar(async () => {
      await this.seguridad.editarRol(rol.id, nombre, this.edicionRol.descripcion.trim() || null);
      this.mostrarExito('Definición del rol actualizada.');
    });
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
      if (this.rolSeleccionadoId === rol.id) this.limpiarSeleccionRol();
      this.mostrarExito('Rol desactivado correctamente.');
    });
  }

  rolesAsignables(usuario: UsuarioSeguridad): RolInstitucionalSeguridad[] {
    const asignados = new Set(usuario.roles.map(rol => rol.rolId));
    return this.rolesActivos.filter(rol => !asignados.has(rol.id));
  }

  async asignarRolUsuario(usuario: UsuarioSeguridad): Promise<void> {
    if (!this.puedeGestionarAsignaciones || !usuario.activo || this.guardando) return;
    const rolId = this.rolPorUsuario[usuario.id];
    const rol = this.rolesAsignables(usuario).find(candidato => candidato.id === rolId);
    if (!rol) {
      this.mostrarError('Seleccione un rol disponible para el usuario.');
      return;
    }

    await this.ejecutar(async () => {
      await this.seguridad.asignarRol(rol.id, usuario.id);
      delete this.rolPorUsuario[usuario.id];
      this.mostrarExito(`Rol ${rol.nombre} asignado a ${usuario.nombre || 'el usuario'}.`);
    });
  }

  async retirarRolUsuario(usuario: UsuarioSeguridad, rol: UsuarioRolSeguridad): Promise<void> {
    if (!this.puedeGestionarAsignaciones || this.guardando) return;
    await this.ejecutar(async () => {
      await this.seguridad.desactivarAsignacion(
        rol.asignacionId,
        'Asignación retirada desde Administración de usuarios'
      );
      this.mostrarExito(`Rol ${rol.nombre} retirado de ${usuario.nombre || 'el usuario'}.`);
    });
  }

  async retirarAsignacion(asignacion: AsignacionRolSeguridad): Promise<void> {
    if (!this.puedeGestionarAsignaciones || !asignacion.activo || this.guardando) return;
    await this.ejecutar(async () => {
      await this.seguridad.desactivarAsignacion(
        asignacion.id,
        'Asignación retirada desde Configuración > Seguridad y acceso'
      );
      this.mostrarExito('Asignación retirada correctamente.');
    });
  }

  volver(): void {
    void this.router.navigate(['/configuracion']);
  }

  private limpiarSeleccionRol(): void {
    this.rolSeleccionadoId = '';
    this.permisosSeleccionados.clear();
    this.edicionRol = { nombre: '', descripcion: '' };
  }

  private async ejecutar(operacion: () => Promise<void>): Promise<void> {
    this.guardando = true;
    this.mensaje = '';
    try {
      await operacion();
      await this.cargar(true);
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
