import { inject, Injectable } from '@angular/core';
import { SUPABASE_CLIENT } from './auth';

export interface InstitucionContexto {
  id: string;
  nombre: string;
}

export interface InstitucionConfigurada extends InstitucionContexto {
  nombreCorto: string | null;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
  logoUrl: string | null;
}

export interface ConfiguracionIdentificadores {
  rneRequerido: boolean;
  identificacionCivilRequerida: boolean;
  codigoInternoRequerido: boolean;
  tiposIdentificacionPermitidos: string[];
}

export interface ConfiguracionInstitucion {
  multiplesInstituciones: boolean;
  institucion: InstitucionConfigurada | null;
  identificadores: ConfiguracionIdentificadores | null;
}

export interface GuardarInstitucionInput {
  nombre: string;
  nombreCorto: string | null;
  direccion: string | null;
  telefono: string | null;
  correo: string | null;
  logoUrl: string | null;
  identificadores: ConfiguracionIdentificadores;
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

  async obtenerConfiguracionInstitucion(institucionId?: string): Promise<ConfiguracionInstitucion> {
    const parametros = institucionId ? { p_institucion_id: institucionId } : undefined;
    const { data, error } = await this.supabase.rpc(
      'rpc_obtener_configuracion_institucion',
      parametros
    );
    if (error) throw this.mapError(error);
    return this.validarConfiguracionInstitucion(data);
  }

  async crearInstitucion(input: GuardarInstitucionInput): Promise<ConfiguracionInstitucion> {
    const { data, error } = await this.supabase.rpc(
      'rpc_crear_institucion',
      this.parametrosInstitucion(input)
    );
    if (error) throw this.mapError(error);
    return this.validarConfiguracionInstitucion(data);
  }

  async actualizarInstitucion(
    institucionId: string,
    input: GuardarInstitucionInput
  ): Promise<ConfiguracionInstitucion> {
    const { data, error } = await this.supabase.rpc('rpc_actualizar_institucion', {
      p_institucion_id: institucionId,
      ...this.parametrosInstitucion(input)
    });
    if (error) throw this.mapError(error);
    return this.validarConfiguracionInstitucion(data);
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

  private validarConfiguracionInstitucion(data: unknown): ConfiguracionInstitucion {
    if (!this.esRegistro(data) || typeof data['multiplesInstituciones'] !== 'boolean') {
      throw new ConfiguracionError('La configuración recibida no es válida.', 'INVALID_RESPONSE');
    }
    const institucion = data['institucion'];
    const identificadores = data['identificadores'];
    if (institucion === null && identificadores === null) {
      return data as unknown as ConfiguracionInstitucion;
    }
    if (
      !this.esRegistro(institucion) || typeof institucion['id'] !== 'string' ||
      typeof institucion['nombre'] !== 'string' || !this.esRegistro(identificadores) ||
      typeof identificadores['rneRequerido'] !== 'boolean' ||
      typeof identificadores['identificacionCivilRequerida'] !== 'boolean' ||
      typeof identificadores['codigoInternoRequerido'] !== 'boolean' ||
      !Array.isArray(identificadores['tiposIdentificacionPermitidos']) ||
      !identificadores['tiposIdentificacionPermitidos'].every(tipo => typeof tipo === 'string')
    ) {
      throw new ConfiguracionError('La configuración institucional recibida no es válida.', 'INVALID_RESPONSE');
    }
    return data as unknown as ConfiguracionInstitucion;
  }

  private parametrosInstitucion(input: GuardarInstitucionInput): Record<string, unknown> {
    return {
      p_nombre: input.nombre.trim(),
      p_nombre_corto: this.nullIfBlank(input.nombreCorto),
      p_direccion: this.nullIfBlank(input.direccion),
      p_telefono: this.nullIfBlank(input.telefono),
      p_correo: this.nullIfBlank(input.correo),
      p_logo_url: this.nullIfBlank(input.logoUrl),
      p_rne_requerido: input.identificadores.rneRequerido,
      p_identificacion_civil_requerida: input.identificadores.identificacionCivilRequerida,
      p_codigo_interno_requerido: input.identificadores.codigoInternoRequerido,
      p_tipos_identificacion_permitidos: input.identificadores.tiposIdentificacionPermitidos
    };
  }

  private nullIfBlank(value: string | null): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
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
      case '22023':
        return new ConfiguracionError(error.message || 'Los datos ingresados no son válidos.', error.code);
      case 'SM004':
      case '23505':
        return new ConfiguracionError('Ya existe un centro educativo activo para esta configuración.', error.code);
      case 'P0002':
        return new ConfiguracionError('El centro educativo no existe o está inactivo.', error.code);
      default:
        return new ConfiguracionError('No se pudo obtener la configuración del sistema.', error.code ?? 'UNKNOWN');
    }
  }

  private esRegistro(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
  }
}
