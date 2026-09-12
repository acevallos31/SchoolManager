import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

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
      this.mensaje = 'No se pudo validar la sesión. Regresando al login...';
      await this.router.navigate(['/login']);
      return;
    }

    await this.router.navigate([
      this.auth.tieneRol('padre') ? '/portal-padre' : '/dashboard'
    ]);
  }
}
