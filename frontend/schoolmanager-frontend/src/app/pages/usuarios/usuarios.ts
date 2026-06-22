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
    if (!this.nuevoUsuario.nombre || !this.nuevoUsuario.correo || !this.nuevoUsuario.password || !this.nuevoUsuario.rol) {
      this.mostrarMensaje('Nombre, correo, contrasena y rol son obligatorios.', 'error');
      return;
    }

    if (this.nuevoUsuario.password.length < 8) {
      this.mostrarMensaje('La contrasena debe tener al menos 8 caracteres.', 'error');
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest('/usuarios', {
        method: 'POST',
        body: JSON.stringify(this.nuevoUsuario)
      });

      this.mostrarMensaje('Usuario creado correctamente.', 'success');
      this.nuevoUsuario = this.crearFormularioVacio();
      this.mostrarFormulario = false;
      await this.cargarUsuarios();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo crear el usuario.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
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
      rol: 'operador'
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
