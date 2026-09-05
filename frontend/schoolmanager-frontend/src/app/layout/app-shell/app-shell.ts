import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css'
})
export class AppShell {
  constructor(private readonly auth: AuthService, private readonly router: Router) {}

  get puedeVerResponsables(): boolean {
    return this.auth.tienePermiso('academico.responsables.ver');
  }

  get puedeVerConfiguracion(): boolean {
    return this.auth.tienePermiso('configuracion.sistema.ver')
      || this.auth.tienePermiso('configuracion.instituciones.ver');
  }

  logout(): void {
    void this.auth.logout();
    void this.router.navigate(['/login']);
  }

  volverAlPanel(): void {
    void this.router.navigate(['/dashboard']);
  }
}
