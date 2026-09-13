import { ChangeDetectorRef, Component, OnDestroy } from '@angular/core';
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
import { AuthService, InstitucionAcceso } from '../../core/services/auth';
import { ContextoInstitucionService } from '../../core/services/contexto-institucion.service';

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
  private readonly usuarioSubscription: Subscription;
  private readonly contextoSubscription: Subscription;
  navAbierta = false;
  roles: string[] = [];
  instituciones: readonly InstitucionAcceso[] = [];
  institucionActual: InstitucionAcceso | null = null;

  // Enlaces con el permiso real que exige cada ruta; el guard sigue siendo la
  // autoridad para navegación directa por URL. Panel: sin permiso concreto.
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
    private readonly contextoInstitucion: ContextoInstitucionService,
    private readonly router: Router,
    private readonly cdr: ChangeDetectorRef
  ) {
    this.navSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => {
        this.navAbierta = false;
      });

    this.usuarioSubscription = this.auth.usuarioActual$.subscribe((usuario) => {
      this.roles = usuario?.roles ?? [];
      this.instituciones = usuario?.instituciones ?? [];
      // En modo zoneless, una emisión RxJS no agenda por sí sola un refresco
      // del template. Se marca el shell explícitamente sin forzar detectChanges().
      this.cdr.markForCheck();
    });

    this.contextoSubscription = this.contextoInstitucion.institucionActual$.subscribe(institucion => {
      this.institucionActual = institucion;
      this.cdr.markForCheck();
    });
  }

  get puedeVerConfiguracion(): boolean {
    return this.auth.tienePermiso('configuracion.sistema.ver')
      || this.auth.tienePermiso('configuracion.instituciones.ver');
  }

  get requiereSeleccionInstitucion(): boolean {
    return this.instituciones.length > 1 && this.institucionActual === null;
  }

  mostrarItem(item: NavItem): boolean {
    // Panel (sin permiso) siempre visible para autenticados; el resto exige el
    // mismo permiso que su ruta. En 042F aún se conserva la unión compatible
    // de /auth/me: el selector no reemplaza autorización backend/RLS.
    if (!item.permiso) return true;
    return this.auth.tienePermiso(item.permiso);
  }

  seleccionarInstitucion(event: Event): void {
    const select = event.target as HTMLSelectElement | null;
    const institucionId = select?.value ?? '';
    if (!institucionId) {
      this.contextoInstitucion.limpiar();
      return;
    }

    this.contextoInstitucion.seleccionar(institucionId);
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
    this.contextoSubscription.unsubscribe();
  }

  alternarNav(): void {
    this.navAbierta = !this.navAbierta;
  }

  cerrarNav(): void {
    this.navAbierta = false;
  }

  logout(): void {
    this.contextoInstitucion.limpiar();
    void this.auth.logout();
    void this.router.navigate(['/login']);
  }

  volverAlPanel(): void {
    void this.router.navigate(['/dashboard']);
  }
}
