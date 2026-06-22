import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Protege rutas que solo el rol de portal puede visitar.
@Injectable({ providedIn: 'root' })
export class PadreGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean {
    const rol = this.authService.getRol();
    if (this.authService.estaAutenticado() && (rol === 'usuario' || rol === 'padre')) {
      return true;
    }
    this.router.navigate(['/login']);
    return false;
  }
}
