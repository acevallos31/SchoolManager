import { Injectable, OnDestroy, inject } from '@angular/core';
import { BehaviorSubject, Subscription } from 'rxjs';
import { AuthService, InstitucionAcceso, UsuarioActual } from './auth';
import { ConfiguracionService } from './configuracion.service';

const STORAGE_KEY = 'schoolmanager-institucion-contexto';

interface ContextoPersistido {
  usuarioId: string;
  institucionId: string;
}

type UsuarioActualExtendido = UsuarioActual & {
  institucionesAdministrables?: InstitucionAcceso[];
};

function obtenerStorageSeguro(): Storage | null {
  if (typeof window === 'undefined') return null;

  try {
    const storage = window.localStorage;
    return typeof storage?.getItem === 'function' && typeof storage?.setItem === 'function'
      ? storage
      : null;
  } catch {
    return null;
  }
}

/**
 * Mantiene una institución de contexto autorizada. Para usuarios normales el
 * contexto proviene de membresías explícitas; para platform_admin también puede
 * provenir de institucionesAdministrables, sin convertirlo en membresía ni
 * conceder permisos locales. Backend/RPC/RLS siguen siendo la autoridad real.
 *
 * El Superadministrador puede ver el catálogo completo de instituciones aun si
 * configuracion_implementacion está en modo mono-institución. Solo las activas
 * pueden seleccionarse como contexto operativo; las inactivas siguen visibles
 * para administración de plataforma.
 *
 * Durante el preview puede ocurrir que el frontend nuevo apunte temporalmente a
 * un backend anterior que todavía no serializa institucionesAdministrables. En
 * modo mono-institución resolvemos únicamente la institución actual mediante el
 * endpoint estable /configuracion/contexto. Ese fallback no inventa permisos ni
 * membresías y desaparece en cuanto el backend 042 entrega el contrato completo.
 */
@Injectable({ providedIn: 'root' })
export class ContextoInstitucionService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly configuracion = inject(ConfiguracionService);
  private readonly storage = obtenerStorageSeguro();
  private readonly institucionSubject = new BehaviorSubject<InstitucionAcceso | null>(null);
  private readonly usuarioSubscription: Subscription;
  private fallbackAdministrables: InstitucionAcceso[] = [];
  private fallbackUsuarioId: string | null = null;
  private fallbackEnCurso = false;

  readonly institucionActual$ = this.institucionSubject.asObservable();

  constructor() {
    this.usuarioSubscription = this.auth.usuarioActual$.subscribe(usuario => {
      this.reconciliarConUsuario(usuario);
    });
  }

  institucionActual(): InstitucionAcceso | null {
    return this.institucionSubject.value;
  }

  /** Instituciones activas que pueden utilizarse como contexto operativo. */
  institucionesDisponibles(): readonly InstitucionAcceso[] {
    return this.institucionesParaContexto(this.auth.usuarioActual());
  }

  /**
   * Catálogo visible en UI. Para platform_admin incluye también instituciones
   * inactivas; para usuarios normales coincide con sus contextos autorizados.
   */
  institucionesVisibles(): readonly InstitucionAcceso[] {
    return this.institucionesCombinadas(this.auth.usuarioActual());
  }

  seleccionar(institucionId: string): boolean {
    const usuario = this.auth.usuarioActual();
    if (!usuario) return false;

    const institucion = this.institucionesParaContexto(usuario)
      .find(item => item.id === institucionId);

    if (!institucion) return false;

    this.institucionSubject.next(institucion);
    this.persistir(usuario.id, institucion.id);
    return true;
  }

  limpiar(): void {
    this.institucionSubject.next(null);
    this.eliminarPersistencia();
  }

  tienePermiso(permiso: string): boolean {
    return this.institucionSubject.value?.permisos.includes(permiso) ?? false;
  }

  tieneRol(rol: string): boolean {
    return this.institucionSubject.value?.roles.includes(rol) ?? false;
  }

  ngOnDestroy(): void {
    this.usuarioSubscription.unsubscribe();
  }

  private reconciliarConUsuario(usuario: UsuarioActual | null): void {
    if (!usuario) {
      this.fallbackAdministrables = [];
      this.fallbackUsuarioId = null;
      this.limpiar();
      return;
    }

    if (this.fallbackUsuarioId !== null && this.fallbackUsuarioId !== usuario.id) {
      this.fallbackAdministrables = [];
      this.fallbackUsuarioId = null;
    }

    const disponibles = this.institucionesParaContexto(usuario);
    if (disponibles.length === 0) {
      this.limpiar();
      if (this.esPlatformAdmin(usuario)) {
        void this.resolverFallbackMonoinstitucion(usuario);
      }
      return;
    }

    const seleccionActual = this.institucionSubject.value;
    if (seleccionActual) {
      const vigente = disponibles.find(item => item.id === seleccionActual.id);
      if (vigente) {
        this.institucionSubject.next(vigente);
        this.persistir(usuario.id, vigente.id);
        return;
      }
    }

    const persistido = this.leerPersistencia();
    if (persistido?.usuarioId === usuario.id) {
      const restaurada = disponibles.find(item => item.id === persistido.institucionId);
      if (restaurada) {
        this.institucionSubject.next(restaurada);
        return;
      }
    }

    if (disponibles.length === 1) {
      this.institucionSubject.next(disponibles[0]);
      this.persistir(usuario.id, disponibles[0].id);
      return;
    }

    this.institucionSubject.next(null);
    this.eliminarPersistencia();
  }

  private institucionesParaContexto(usuario: UsuarioActual | null): InstitucionAcceso[] {
    return this.institucionesCombinadas(usuario).filter(institucion => institucion.activo !== false);
  }

  private institucionesCombinadas(usuario: UsuarioActual | null): InstitucionAcceso[] {
    if (!usuario) return [];
    const extendido = usuario as UsuarioActualExtendido;
    const explicitas = usuario.instituciones ?? [];
    const administrablesContrato = this.esPlatformAdmin(usuario)
      ? extendido.institucionesAdministrables ?? []
      : [];
    const administrablesFallback = this.esPlatformAdmin(usuario) && this.fallbackUsuarioId === usuario.id
      ? this.fallbackAdministrables
      : [];

    const porId = new Map<string, InstitucionAcceso>();
    for (const institucion of administrablesFallback) porId.set(institucion.id, institucion);
    for (const institucion of administrablesContrato) porId.set(institucion.id, institucion);
    // Una membresía explícita gana sobre el contexto administrable porque sí
    // contiene los roles/permisos propios de esa institución.
    for (const institucion of explicitas) porId.set(institucion.id, institucion);
    return [...porId.values()];
  }

  private esPlatformAdmin(usuario: UsuarioActual): boolean {
    return usuario.ambitoGlobal?.roles.includes('platform_admin')
      ?? usuario.roles.includes('platform_admin');
  }

  private async resolverFallbackMonoinstitucion(usuario: UsuarioActual): Promise<void> {
    if (this.fallbackEnCurso || this.fallbackUsuarioId === usuario.id) return;
    this.fallbackEnCurso = true;

    try {
      const contexto = await this.configuracion.obtenerContexto();
      if (this.auth.usuarioActual()?.id !== usuario.id || !contexto.institucion) return;

      this.fallbackUsuarioId = usuario.id;
      this.fallbackAdministrables = [{
        id: contexto.institucion.id,
        nombre: contexto.institucion.nombre,
        nombreCorto: null,
        roles: [],
        permisos: [],
        activo: true
      }];
      this.reconciliarConUsuario(usuario);
    } catch {
      // El fallback es únicamente compatibilidad de preview. Si el endpoint no
      // está disponible, mantenemos el estado sin institución y la UI explica
      // que debe seleccionarse una cuando el backend 042 esté desplegado.
    } finally {
      this.fallbackEnCurso = false;
    }
  }

  private persistir(usuarioId: string, institucionId: string): void {
    if (!this.storage) return;

    try {
      this.storage.setItem(STORAGE_KEY, JSON.stringify({ usuarioId, institucionId }));
    } catch {
      // La selección sigue siendo válida en memoria aunque storage esté bloqueado.
    }
  }

  private leerPersistencia(): ContextoPersistido | null {
    if (!this.storage) return null;

    try {
      const raw = this.storage.getItem(STORAGE_KEY);
      if (!raw) return null;
      const valor = JSON.parse(raw) as Partial<ContextoPersistido>;
      return typeof valor.usuarioId === 'string' && typeof valor.institucionId === 'string'
        ? { usuarioId: valor.usuarioId, institucionId: valor.institucionId }
        : null;
    } catch {
      return null;
    }
  }

  private eliminarPersistencia(): void {
    try {
      this.storage?.removeItem(STORAGE_KEY);
    } catch {
      // No hay nada que recuperar: se mantiene el estado en memoria.
    }
  }
}
