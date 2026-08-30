import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from '../services/auth';

// Compatibilidad de navegacion; la autorizacion real permanece en el backend/RLS.
@Injectable({ providedIn: 'root' })
export class PadreGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean {
    if (this.authService.isLoggedIn() && this.authService.tieneRol('padre')) {
      return true;
    }
    this.router.navigate(['/login']);
    return false;
  }
}
