import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Protege rutas que solo el rol "padre" puede visitar
@Injectable({ providedIn: 'root' })
export class PadreGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean {
    if (this.authService.estaAutenticado() && this.authService.getRol() === 'padre') {
      return true;
    }
    this.router.navigate(['/login']);
    return false;
  }
}
