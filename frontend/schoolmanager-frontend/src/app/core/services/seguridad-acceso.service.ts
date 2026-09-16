import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SeguridadCapacidades {
  rolesVer: boolean;
  rolesCrear: boolean;
  rolesEditar: boolean;
  rolesAsignarPermisos: boolean;
  usuariosVer: boolean;
  usuariosAsignarRoles: boolean;
}

export interface RolInstitucionalSeguridad {
  id: string;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  activo: boolean;
  protegido: boolean;
  rolBaseId: string | null;
  plantillaVersion: number | null;
  permisos: string[];
}

export interface PlantillaRolSeguridad {
  id: string;
  codigo: string;
  nombre: string;
  descripcion: string | null;
  version: number | null;
  clonable: boolean;
}

export interface PermisoDelegableSeguridad {
  codigo: string;
  modulo: string;
  nombre: string;
  descripcion: string | null;
  riesgo: 'bajo' | 'medio' | 'alto' | 'critico';
}

export interface AsignacionRolSeguridad {
  id: string;
  usuarioId: string;
  nombre: string;
  rolId: string;
  rolCodigo: string;
  rolNombre: string;
  rolTipo: string;
  activo: boolean;
  creadoEn: string;
}

export interface UsuarioRolSeguridad {
  asignacionId: string;
  rolId: string;
  codigo: string;
  nombre: string;
}

export interface UsuarioSeguridad {
  id: string;
  nombre: string;
  correo: string | null;
  activo: boolean;
  identidadVinculada: boolean;
  roles: UsuarioRolSeguridad[];
}

export interface SeguridadAccesoSnapshot {
  institucionId: string;
  capacidades: SeguridadCapacidades;
  roles: RolInstitucionalSeguridad[];
  plantillas: PlantillaRolSeguridad[];
  permisosDelegables: PermisoDelegableSeguridad[];
  asignaciones: AsignacionRolSeguridad[];
}

export interface CrearRolInput {
  institucionId: string;
  codigo: string;
  nombre: string;
  descripcion?: string | null;
}

export interface ClonarPlantillaInput extends CrearRolInput {
  plantillaCodigo: string;
}

interface IdentificadorRespuesta { id: string; }

export class SeguridadAccesoError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'SeguridadAccesoError';
  }
}

@Injectable({ providedIn: 'root' })
export class SeguridadAccesoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/configuracion/seguridad`;

  async obtener(institucionId: string): Promise<SeguridadAccesoSnapshot> {
    return this.obtenerJson<SeguridadAccesoSnapshot>(
      `${this.baseUrl}?institucionId=${encodeURIComponent(institucionId)}`
    );
  }

  async obtenerUsuarios(institucionId: string): Promise<UsuarioSeguridad[]> {
    return this.obtenerJson<UsuarioSeguridad[]>(
      `${this.baseUrl}/usuarios?institucionId=${encodeURIComponent(institucionId)}`
    );
  }

  async crearRol(input: CrearRolInput): Promise<string> {
    const respuesta = await this.ejecutar<IdentificadorRespuesta>('POST', '/roles', input);
    return respuesta.id;
  }

  async clonarPlantilla(input: ClonarPlantillaInput): Promise<string> {
    const respuesta = await this.ejecutar<IdentificadorRespuesta>('POST', '/roles/clonar', input);
    return respuesta.id;
  }

  async editarRol(rolId: string, nombre: string, descripcion: string | null): Promise<void> {
    await this.ejecutar('PUT', `/roles/${encodeURIComponent(rolId)}`, { nombre, descripcion });
  }

  async reemplazarPermisos(rolId: string, permisos: readonly string[]): Promise<void> {
    await this.ejecutar('PUT', `/roles/${encodeURIComponent(rolId)}/permisos`, {
      permisos: [...permisos]
    });
  }

  async asignarRol(rolId: string, usuarioId: string): Promise<string> {
    const respuesta = await this.ejecutar<IdentificadorRespuesta>(
      'POST', `/roles/${encodeURIComponent(rolId)}/asignaciones`, { usuarioId }
    );
    return respuesta.id;
  }

  async desactivarRol(rolId: string, motivo: string): Promise<void> {
    await this.ejecutar('POST', `/roles/${encodeURIComponent(rolId)}/desactivar`, { motivo });
  }

  async desactivarAsignacion(asignacionId: string, motivo: string): Promise<void> {
    await this.ejecutar(
      'POST', `/asignaciones/${encodeURIComponent(asignacionId)}/desactivar`, { motivo }
    );
  }

  private async obtenerJson<T>(url: string): Promise<T> {
    try {
      return await firstValueFrom(this.http.get<T>(url));
    } catch (error) {
      throw this.mapearError(error);
    }
  }

  private async ejecutar<T = unknown>(method: string, path: string, body: unknown): Promise<T> {
    try {
      return await firstValueFrom(this.http.request<T>(method, `${this.baseUrl}${path}`, { body }));
    } catch (error) {
      throw this.mapearError(error);
    }
  }

  private mapearError(error: unknown): SeguridadAccesoError {
    if (!(error instanceof HttpErrorResponse)) {
      return new SeguridadAccesoError('No se pudo completar la operación de seguridad.', 0);
    }

    const payload = error.error as { error?: unknown } | null;
    if (payload && typeof payload === 'object' && typeof payload.error === 'string') {
      return new SeguridadAccesoError(payload.error, error.status);
    }

    if (error.status === 403) {
      return new SeguridadAccesoError(
        'No tienes permiso para administrar la seguridad de esta institución.',
        error.status
      );
    }
    if (error.status === 404) {
      return new SeguridadAccesoError(
        'La API desplegada todavía no incluye esta operación de Seguridad y acceso.',
        error.status
      );
    }
    if (error.status >= 500) {
      return new SeguridadAccesoError(
        'La API de seguridad no pudo completar la operación. Revisa el despliegue del backend.',
        error.status
      );
    }

    return new SeguridadAccesoError('No se pudo completar la operación de seguridad.', error.status);
  }
}
