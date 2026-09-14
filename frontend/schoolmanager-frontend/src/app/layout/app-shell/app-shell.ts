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
import { AuthService, InstitucionAcceso, UsuarioActual } from '../../core/services/auth';
import { ContextoInstitucionService } from '../../core/services/contexto-institucion.service';

interface NavItem {
  etiqueta: string;
  ruta: string;
  permiso?: string;
}

type UsuarioActualExtendido = UsuarioActual & {
  nombreCompleto?: string;
  institucionesAdministrables?: InstitucionAcceso[];
};

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
  nombreUsuario = 'Usuario';
  instituciones: readonly InstitucionAcceso[] = [];
  institucionActual: InstitucionAcceso | null = null;

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
      const extendido = usuario as UsuarioActualExtendido | null;
      this.nombreUsuario = extendido?.nombreCompleto?.trim() || 'Usuario';
      this.instituciones = this.institucionesParaContexto(extendido);
      this.cdr.markForCheck();
    });

    this.contextoSubscription = this.contextoInstitucion.institucionActual$.subscribe(institucion => {
      this.institucionActual = institucion;
      this.cdr.markForCheck();
    });
  }

  get esSuperadministrador(): boolean {
    return this.auth.esSuperadministrador();
  }

  get rolVisible(): string {
    if (this.esSuperadministrador) return 'Superadministrador';
    const rol = this.roles[0];
    if (!rol) return '';

    const etiquetas: Record<string, string> = {
      admin: 'Administrador',
      school_admin: 'Administrador institucional',
      academic_coordinator: 'Coordinación académica',
      finance_operator: 'Finanzas',
      teacher: 'Docente',
      parent: 'Responsable',
      student: 'Alumno',
      demo_viewer: 'Demo / solo lectura',
      support_agent: 'Soporte'
    };
    return etiquetas[rol] ?? rol.replaceAll('_', ' ');
  }

  get puedeVerConfiguracion(): boolean {
    return this.esSuperadministrador
      || this.auth.tienePermiso('configuracion.sistema.ver')
      || this.auth.tienePermiso('configuracion.instituciones.ver')
      || this.auth.tienePermiso('identidad.roles.ver')
      || this.auth.tienePermiso('identidad.usuarios.ver');
  }

  get requiereSeleccionInstitucion(): boolean {
    return this.instituciones.length > 1 && this.institucionActual === null;
  }

  mostrarItem(item: NavItem): boolean {
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

  private institucionesParaContexto(usuario: UsuarioActualExtendido | null): readonly InstitucionAcceso[] {
    if (!usuario) return [];
    const explicitas = usuario.instituciones ?? [];
    const administrables = usuario.ambitoGlobal?.roles.includes('platform_admin')
      ? usuario.institucionesAdministrables ?? []
      : [];

    const porId = new Map<string, InstitucionAcceso>();
    for (const institucion of administrables) porId.set(institucion.id, institucion);
    for (const institucion of explicitas) porId.set(institucion.id, institucion);
    return [...porId.values()];
  }
}
