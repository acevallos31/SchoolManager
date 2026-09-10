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
import { ConfiguracionService } from '../../core/services/configuracion.service';
import { DebugStateService } from '../../core/services/debug-state.service';

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
  debugActivo = false;
  private debugCargado = false;

  readonly items: NavItem[] = [
    { etiqueta: 'Panel', ruta: '/dashboard' },
    { etiqueta: 'Alumnos', ruta: '/alumnos', permiso: 'academico.alumnos.ver' },
    { etiqueta: 'Matrículas', ruta: '/matriculas', permiso: 'academico.matriculas.ver' },
    { etiqueta: 'Ciclos escolares', ruta: '/configuracion/ciclos', permiso: 'academico.ciclos.ver' },
    { etiqueta: 'Estructura académica', ruta: '/configuracion/estructura-academica', permiso: 'academico.estructura.ver' },
    { etiqueta: 'Responsables', ruta: '/responsables', permiso: 'academico.responsables.ver' },
    { etiqueta: 'Cargos', ruta: '/cargos', permiso: 'academico.cargos.ver' },
    { etiqueta: 'Pagos', ruta: '/pagos', permiso: 'academico.pagos.ver' }
  ];

  constructor(
    private readonly auth: AuthService,
    private readonly router: Router,
    private readonly configuracionService: ConfiguracionService,
    debugState: DebugStateService
  ) {
    this.navSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => {
        this.navAbierta = false;
      });

    this.usuarioSubscription = this.auth.usuarioActual$.subscribe((usuario) => {
      this.roles = usuario?.roles ?? [];
      if (usuario && !this.debugCargado && this.auth.tienePermiso('sistema.debug.ver')) {
        this.debugCargado = true;
        void this.configuracionService.obtenerDebug().catch(() => undefined);
      }
    });

    debugState.status$.subscribe(status => {
      this.debugActivo = status.habilitado;
    });
  }

  get puedeVerConfiguracion(): boolean {
    return this.auth.tienePermiso('configuracion.sistema.ver')
      || this.auth.tienePermiso('configuracion.instituciones.ver');
  }

  mostrarItem(item: NavItem): boolean {
    if (!item.permiso) return true;
    return this.auth.tienePermiso(item.permiso);
  }

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
