import { inject, Injectable } from '@angular/core';
import { SUPABASE_CLIENT } from './auth';

export interface InstitucionContexto {
  id: string;
  nombre: string;
}

export interface ContextoImplementacion {
  multiplesInstituciones: boolean;
  institucion: InstitucionContexto | null;
}

interface SupabaseErrorLike {
  code?: string;
  message?: string;
}

export class ConfiguracionError extends Error {
  constructor(message: string, public readonly code: string) {
    super(message);
    this.name = 'ConfiguracionError';
  }
}

@Injectable({ providedIn: 'root' })
export class ConfiguracionService {
  private readonly supabase = inject(SUPABASE_CLIENT);

  async obtenerContexto(): Promise<ContextoImplementacion> {
    const { data, error } = await this.supabase.rpc('rpc_obtener_contexto_implementacion');
    if (error) throw this.mapError(error);
    return this.validarContexto(data);
  }

  async actualizarModo(multiplesInstituciones: boolean): Promise<ContextoImplementacion> {
    const { data, error } = await this.supabase.rpc(
      'rpc_actualizar_multiples_instituciones',
      { p_multiples_instituciones: multiplesInstituciones }
    );
    if (error) throw this.mapError(error);
    return this.validarContexto(data);
  }

  async esMultiInstitucion(): Promise<boolean> {
    return (await this.obtenerContexto()).multiplesInstituciones;
  }

  async obtenerInstitucionActual(): Promise<InstitucionContexto> {
    const contexto = await this.obtenerContexto();
    if (!contexto.institucion) {
      throw new ConfiguracionError(
        'Seleccione una institución para continuar.',
        'INSTITUTION_CONTEXT_REQUIRED'
      );
    }
    return contexto.institucion;
  }

  private validarContexto(data: unknown): ContextoImplementacion {
    if (!this.esRegistro(data) || typeof data['multiplesInstituciones'] !== 'boolean') {
      throw new ConfiguracionError('La configuración recibida no es válida.', 'INVALID_RESPONSE');
    }
    const institucion = data['institucion'];
    if (institucion !== null && (
      !this.esRegistro(institucion) ||
      typeof institucion['id'] !== 'string' ||
      typeof institucion['nombre'] !== 'string'
    )) {
      throw new ConfiguracionError('La institución recibida no es válida.', 'INVALID_RESPONSE');
    }
    return data as unknown as ContextoImplementacion;
  }

  private mapError(error: SupabaseErrorLike): ConfiguracionError {
    switch (error.code) {
      case 'SM001':
        return new ConfiguracionError('No hay un centro educativo configurado.', error.code);
      case 'SM002':
        return new ConfiguracionError(
          'Hay más de una institución activa y el sistema está configurado para una sola institución.',
          error.code
        );
      case 'SM003':
        return new ConfiguracionError('Seleccione una institución para continuar.', error.code);
      case '42501':
        return new ConfiguracionError('No tienes permiso para modificar la configuración.', error.code);
      default:
        return new ConfiguracionError('No se pudo obtener la configuración del sistema.', error.code ?? 'UNKNOWN');
    }
  }

  private esRegistro(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
  }
}
