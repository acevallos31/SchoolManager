import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-usuarios',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './usuarios.html',
  styleUrl: './usuarios.css'
})
export class Usuarios implements OnInit {
  usuarios: any[] = [];
  cargando = false;
  mensaje = '';
  mensajeTipo: 'success' | 'error' = 'success';
  mostrarFormulario = false;
  usuarioEditandoId = '';

  nuevoUsuario = this.crearFormularioVacio();

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    await this.cargarUsuarios();
  }

  async cargarUsuarios() {
    this.cargando = true;
    this.cdr.detectChanges();

    try {
      const data = await this.auth.apiRequest<any[]>('/usuarios');
      this.usuarios = Array.isArray(data) ? [...data] : [];
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudieron cargar los usuarios.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  async guardarUsuario() {
    if (!this.nuevoUsuario.nombre || !this.nuevoUsuario.correo || !this.nuevoUsuario.rol) {
      this.mostrarMensaje('Nombre, correo y rol son obligatorios.', 'error');
      return;
    }

    if (!this.usuarioEditandoId && !this.nuevoUsuario.password) {
      this.mostrarMensaje('La contrasena inicial es obligatoria.', 'error');
      return;
    }

    if (this.nuevoUsuario.password && this.nuevoUsuario.password.length < 8) {
      this.mostrarMensaje('La contrasena debe tener al menos 8 caracteres.', 'error');
      return;
    }

    this.cargando = true;

    try {
      const url = this.usuarioEditandoId ? `/usuarios/${this.usuarioEditandoId}` : '/usuarios';
      await this.auth.apiRequest(url, {
        method: this.usuarioEditandoId ? 'PUT' : 'POST',
        body: JSON.stringify(this.nuevoUsuario)
      });

      this.mostrarMensaje(this.usuarioEditandoId ? 'Usuario actualizado correctamente.' : 'Usuario creado correctamente.', 'success');
      this.cancelarEdicion();
      await this.cargarUsuarios();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo guardar el usuario.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  abrirNuevo() {
    this.nuevoUsuario = this.crearFormularioVacio();
    this.usuarioEditandoId = '';
    this.mostrarFormulario = true;
    this.cdr.detectChanges();
  }

  editarUsuario(usuario: any) {
    this.usuarioEditandoId = usuario.id;
    this.nuevoUsuario = {
      usuario: usuario.usuario || '',
      nombre: usuario.nombreCompleto || usuario.nombre_completo || usuario.nombre || '',
      correo: usuario.correo || '',
      password: '',
      rol: usuario.rol || 'operador',
      activo: usuario.activo !== false
    };
    this.mostrarFormulario = true;
    this.cdr.detectChanges();
  }

  async desactivarUsuario(id: string) {
    if (!confirm('Desactivar este usuario?')) {
      return;
    }

    await this.auth.apiRequest(`/usuarios/${id}`, { method: 'DELETE' });
    await this.cargarUsuarios();
  }

  async eliminarUsuario(id: string) {
    if (!confirm('Eliminar definitivamente este usuario? Si tiene alumnos vinculados, la base puede rechazarlo.')) {
      return;
    }

    try {
      await this.auth.apiRequest(`/usuarios/${id}?permanente=true`, { method: 'DELETE' });
      this.mostrarMensaje('Usuario eliminado correctamente.', 'success');
      await this.cargarUsuarios();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo eliminar el usuario.', 'error');
    }
  }

  async activarUsuario(id: string) {
    await this.auth.apiRequest(`/usuarios/${id}/activar`, { method: 'PUT' });
    await this.cargarUsuarios();
  }

  cancelarEdicion() {
    this.nuevoUsuario = this.crearFormularioVacio();
    this.usuarioEditandoId = '';
    this.mostrarFormulario = false;
    this.cdr.detectChanges();
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }

  private crearFormularioVacio() {
    return {
      usuario: '',
      nombre: '',
      correo: '',
      password: '',
      rol: 'operador',
      activo: true
    };
  }

  private mostrarMensaje(texto: string, tipo: 'success' | 'error') {
    this.mensaje = texto;
    this.mensajeTipo = tipo;
    this.cdr.detectChanges();

    setTimeout(() => {
      this.mensaje = '';
      this.cdr.detectChanges();
    }, 4200);
  }
}
