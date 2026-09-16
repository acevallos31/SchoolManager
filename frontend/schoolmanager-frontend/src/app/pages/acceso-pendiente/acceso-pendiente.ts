import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-acceso-pendiente',
  standalone: true,
  template: `
    <main class="acceso-pendiente">
      <section class="acceso-pendiente__card" aria-labelledby="titulo-acceso-pendiente">
        <p class="acceso-pendiente__eyebrow">Acceso configurado parcialmente</p>
        <h1 id="titulo-acceso-pendiente">Tu usuario todavía no tiene una pantalla habilitada</h1>
        <p>
          La sesión es válida, pero tus roles actuales no incluyen una capacidad con una
          sección disponible en SchoolManager. Solicita al administrador de la institución
          que revise tus roles y permisos.
        </p>
        <button type="button" (click)="cerrarSesion()">Cerrar sesión</button>
      </section>
    </main>
  `,
  styles: [`
    .acceso-pendiente { min-height: 100vh; display: grid; place-items: center; padding: 24px; background: #f5f7fb; }
    .acceso-pendiente__card { width: min(620px, 100%); padding: 32px; border-radius: 16px; background: #fff; box-shadow: 0 16px 40px rgba(23,49,79,.08); color: #17314f; }
    .acceso-pendiente__eyebrow { margin: 0 0 8px; font-size: .8rem; font-weight: 700; text-transform: uppercase; letter-spacing: .08em; }
    h1 { margin: 0 0 16px; font-size: clamp(1.5rem, 4vw, 2rem); }
    p { line-height: 1.6; }
    button { margin-top: 16px; border: 0; border-radius: 8px; padding: 10px 16px; font: inherit; cursor: pointer; }
  `]
})
export class AccesoPendiente {
  constructor(
    private readonly auth: AuthService,
    private readonly router: Router
  ) {}

  async cerrarSesion(): Promise<void> {
    await this.auth.logout();
    await this.router.navigate(['/login']);
  }
}
