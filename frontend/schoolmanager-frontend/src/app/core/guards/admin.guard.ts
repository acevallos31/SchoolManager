import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Protege rutas que solo el rol "admin" puede visitar
@Injectable({ providedIn: 'root' })
export class AdminGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean {
    if (this.authService.estaAutenticado() && this.authService.getRol() === 'admin') {
      return true;
    }
    this.router.navigate(['/login']);
    return false;
  }
}
