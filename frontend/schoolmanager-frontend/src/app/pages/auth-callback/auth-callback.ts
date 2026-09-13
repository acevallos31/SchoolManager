import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import { resolverRutaInicial } from '../../core/services/landing-route';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  template: `
    <main class="auth-callback" aria-live="polite">
      <p>{{ mensaje }}</p>
    </main>
  `,
  styles: [`
    .auth-callback {
      min-height: 100vh;
      display: grid;
      place-items: center;
      color: #17314f;
      font: 1rem system-ui, sans-serif;
    }
  `]
})
export class AuthCallback implements OnInit {
  mensaje = 'Validando acceso con Google...';

  constructor(
    private auth: AuthService,
    private router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    await this.auth.asegurarUsuarioInicial();

    if (!this.auth.isLoggedIn()) {
      // El motivo lo conserva AuthService (identidad de Google sin vincular,
      // usuario inactivo o fallo de red) en lugar de limpiar la sesión en
      // silencio. /login lo consume y lo muestra.
      this.mensaje =
        this.auth.mensajeSesionInvalidaPendiente() ??
        'No se pudo validar la sesión. Regresando al login...';
      await this.router.navigate(['/login']);
      return;
    }

    try {
      const usuario = await this.auth.getUsuarioActual();
      const ruta = resolverRutaInicial(usuario);

      if (!ruta) {
        this.mensaje =
          'Tu usuario esta activo, pero todavia no tiene una pantalla habilitada. Contacta al administrador para revisar sus permisos.';
        return;
      }

      await this.router.navigate([ruta]);
    } catch {
      this.mensaje = 'No se pudo validar tu perfil. Regresando al login...';
      await this.router.navigate(['/login']);
    }
  }
}
