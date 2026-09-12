import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthAppError, AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login implements OnInit {
  correo = '';
  password = '';
  error = '';
  cargando = false;
  cargandoGoogle = false;

  constructor(private auth: AuthService, private router: Router) {}

  ngOnInit(): void {
    // Motivo conservado por AuthService cuando había sesión pero el backend no
    // reconoció el perfil (p. ej. identidad de Google aún no vinculada). Se
    // muestra en lugar del genérico "no se pudo validar la sesión".
    this.error = this.auth.consumirMensajeSesionInvalida() ?? '';
  }

  async login() {
    this.error = '';

    const correo = this.correo.trim();
    const password = this.password.trim();

    if (!correo || !password) {
      this.error = 'Ingresa tu correo y contrasena para continuar.';
      return;
    }

    this.cargando = true;

    try {
      const usuario = await this.auth.login(correo, password);

      if (usuario.roles.includes('admin')) {
        await this.router.navigate(['/dashboard']);
      } else if (usuario.roles.includes('padre')) {
        await this.router.navigate(['/portal-padre']);
      } else {
        this.error = 'Rol de usuario no reconocido.';
        await this.auth.logout();
      }
    } catch (error: unknown) {
      this.error = this.obtenerMensajeError(error);

      try {
        await this.auth.logout();
      } catch (logoutError) {
        console.error('No se pudo cerrar la sesion despues del error:', logoutError);
      }
    } finally {
      this.cargando = false;
    }
  }

  async loginWithGoogle() {
    this.error = '';
    this.cargandoGoogle = true;

    try {
      await this.auth.loginWithGoogle();
    } catch (error: unknown) {
      this.error = this.obtenerMensajeError(error);
    } finally {
      this.cargandoGoogle = false;
    }
  }

  private obtenerMensajeError(error: unknown): string {
    if (error instanceof AuthAppError) {
      switch (error.code) {
        case 'INVALID_CREDENTIALS':
          return 'Correo o contrasena incorrectos.';
        case 'EMAIL_NOT_CONFIRMED':
          return 'Debes confirmar tu correo antes de iniciar sesion.';
        case 'USER_PROFILE_NOT_FOUND':
          return 'Tu cuenta existe, pero no esta vinculada a un usuario de SchoolManager. Contacta al administrador para vincular tu identidad.';
        case 'USER_PROFILE_ERROR':
          return 'No se pudo validar tu perfil. Contacta al administrador.';
        case 'REQUEST_TIMEOUT':
          return error.message;
        case 'SESSION_NOT_FOUND':
          return 'No se pudo crear una sesion valida. Intenta nuevamente.';
        default:
          return error.message;
      }
    }

    console.error('Error no controlado en login:', error);
    return 'Ocurrio un error inesperado. Intenta nuevamente.';
  }
}
