import { Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
  NavigationEnd
} from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { AuthService } from '../../core/services/auth';

/** Enlace de navegación primario del shell. `permiso` opcional: cuando se
 *  indica, el enlace se oculta sin ese permiso; el guard de ruta sigue siendo
 *  la autoridad para la navegación directa por URL. */
interface NavItem {
  etiqueta: string;
  ruta: string;
  permiso?: string;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css'
})
export class AppShell implements OnDestroy {
  private readonly navSubscription: Subscription;
  private usuarioSubscription: Subscription;
  navAbierta = false;
  roles: string[] = [];

  // Panel y Matrículas sin permiso concreto (regla histórica: siempre visibles).
  // Cargos/Pagos/Responsables/Alumnos con permiso; Configuración con OR de dos.
  readonly items: NavItem[] = [
    { etiqueta: 'Panel', ruta: '/dashboard' },
    { etiqueta: 'Alumnos', ruta: '/alumnos', permiso: 'academico.alumnos.ver' },
    { etiqueta: 'Matrículas', ruta: '/matriculas' },
    { etiqueta: 'Responsables', ruta: '/responsables', permiso: 'academico.responsables.ver' },
    { etiqueta: 'Cargos', ruta: '/cargos', permiso: 'academico.cargos.ver' },
    { etiqueta: 'Pagos', ruta: '/pagos', permiso: 'academico.pagos.ver' }
  ];

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router
  ) {
    this.navSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => {
        this.navAbierta = false;
      });

    this.usuarioSubscription = this.auth.usuarioActual$.subscribe((usuario) => {
      this.roles = usuario?.roles ?? [];
    });
  }

  get puedeVerConfiguracion(): boolean {
    return this.auth.tienePermiso('configuracion.sistema.ver')
      || this.auth.tienePermiso('configuracion.instituciones.ver');
  }

  mostrarItem(item: NavItem): boolean {
    // Panel y Matrículas: sin permiso concreto, siempre visibles para autenticados.
    if (!item.permiso) return true;
    return this.auth.tienePermiso(item.permiso);
  }

  /** Activa el enlace del panel solo en su ruta exacta; el resto, por prefijo. */
  esRutaActiva(item: NavItem): boolean {
    const url = this.router.url;
    if (item.ruta === '/dashboard') return url === '/dashboard';
    return url === item.ruta || url.startsWith(item.ruta + '/');
  }

  ngOnDestroy(): void {
    this.navSubscription.unsubscribe();
    this.usuarioSubscription.unsubscribe();
  }

  alternarNav(): void {
    this.navAbierta = !this.navAbierta;
  }

  cerrarNav(): void {
    this.navAbierta = false;
  }

  logout(): void {
    void this.auth.logout();
    void this.router.navigate(['/login']);
  }

  volverAlPanel(): void {
    void this.router.navigate(['/dashboard']);
  }
}
