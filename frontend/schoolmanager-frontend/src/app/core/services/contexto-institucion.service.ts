import { Injectable, OnDestroy, inject } from '@angular/core';
import { BehaviorSubject, Subscription } from 'rxjs';
import { AuthService, InstitucionAcceso, UsuarioActual } from './auth';

const STORAGE_KEY = 'schoolmanager-institucion-contexto';

interface ContextoPersistido {
  usuarioId: string;
  institucionId: string;
}

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
 * Mantiene la institución seleccionada dentro del conjunto explícito devuelto
 * por /api/auth/me. Nunca acepta un institucionId que el perfil actual no tenga
 * en instituciones[]. El backend/RPC/RLS continúan siendo la autoridad real.
 */
@Injectable({ providedIn: 'root' })
export class ContextoInstitucionService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly storage = obtenerStorageSeguro();
  private readonly institucionSubject = new BehaviorSubject<InstitucionAcceso | null>(null);
  private readonly usuarioSubscription: Subscription;

  readonly institucionActual$ = this.institucionSubject.asObservable();

  constructor() {
    this.usuarioSubscription = this.auth.usuarioActual$.subscribe(usuario => {
      this.reconciliarConUsuario(usuario);
    });
  }

  institucionActual(): InstitucionAcceso | null {
    return this.institucionSubject.value;
  }

  institucionesDisponibles(): readonly InstitucionAcceso[] {
    return this.auth.institucionesDisponibles();
  }

  /**
   * Selecciona solo una institución expresamente autorizada en el perfil.
   * Devuelve false en vez de fabricar contexto cuando el id no pertenece al usuario.
   */
  seleccionar(institucionId: string): boolean {
    const usuario = this.auth.usuarioActual();
    if (!usuario) return false;

    const institucion = (usuario.instituciones ?? [])
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
      this.limpiar();
      return;
    }

    const disponibles = usuario.instituciones ?? [];
    if (disponibles.length === 0) {
      this.limpiar();
      return;
    }

    const seleccionActual = this.institucionSubject.value;
    if (seleccionActual) {
      const vigente = disponibles.find(item => item.id === seleccionActual.id);
      if (vigente) {
        // Refresca roles/permisos si /auth/me cambió manteniendo el mismo id.
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

    // Con una sola institución no hay ambigüedad: se selecciona automáticamente.
    if (disponibles.length === 1) {
      this.institucionSubject.next(disponibles[0]);
      this.persistir(usuario.id, disponibles[0].id);
      return;
    }

    // Con varias instituciones y sin selección válida se exige elección explícita.
    this.institucionSubject.next(null);
    this.eliminarPersistencia();
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
