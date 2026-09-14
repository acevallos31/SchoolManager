import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthAppError, AuthService } from '../../core/services/auth';
import { OAuthProviderService } from '../../core/services/oauth-provider.service';
import { resolverRutaInicial } from '../../core/services/landing-route';

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
  proveedorCargando: 'google' | 'microsoft' | null = null;

  constructor(
    private auth: AuthService,
    private oauth: OAuthProviderService,
    private router: Router
  ) {}

  ngOnInit(): void {
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
      const ruta = resolverRutaInicial(usuario);
      await this.router.navigate([ruta ?? '/acceso-pendiente']);
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
    await this.iniciarOAuth('google');
  }

  async loginWithMicrosoft() {
    await this.iniciarOAuth('microsoft');
  }

  private async iniciarOAuth(proveedor: 'google' | 'microsoft'): Promise<void> {
    this.error = '';
    this.proveedorCargando = proveedor;

    try {
      if (proveedor === 'google') {
        await this.oauth.continuarConGoogle();
      } else {
        await this.oauth.continuarConMicrosoft();
      }
    } catch (error: unknown) {
      this.error = this.obtenerMensajeError(error);
    } finally {
      this.proveedorCargando = null;
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
          return 'Tu cuenta existe, pero todavía no está vinculada a un perfil habilitado de SchoolManager. Si eres padre o encargado, revisa la invitación enviada por tu institución.';
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
