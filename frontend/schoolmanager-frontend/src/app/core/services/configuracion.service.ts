import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

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

interface ConfiguracionApiError {
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
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/configuracion`;

  async obtenerContexto(): Promise<ContextoImplementacion> {
    return this.validarContexto(await this.solicitar('GET', '/contexto'));
  }

  async actualizarModo(multiplesInstituciones: boolean): Promise<ContextoImplementacion> {
    return this.validarContexto(await this.solicitar('PUT', '/modo', { multiplesInstituciones }));
  }

  async obtenerConfiguracionInstitucion(institucionId?: string): Promise<ConfiguracionInstitucion> {
    const query = institucionId ? `?institucionId=${encodeURIComponent(institucionId)}` : '';
    return this.validarConfiguracionInstitucion(await this.solicitar('GET', `/institucion${query}`));
  }

  async crearInstitucion(input: GuardarInstitucionInput): Promise<ConfiguracionInstitucion> {
    return this.validarConfiguracionInstitucion(
      await this.solicitar('POST', '/instituciones', this.normalizarInstitucion(input))
    );
  }

  async actualizarInstitucion(
    institucionId: string,
    input: GuardarInstitucionInput
  ): Promise<ConfiguracionInstitucion> {
    return this.validarConfiguracionInstitucion(await this.solicitar(
      'PUT', `/instituciones/${encodeURIComponent(institucionId)}`, this.normalizarInstitucion(input)
    ));
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

  private normalizarInstitucion(input: GuardarInstitucionInput): GuardarInstitucionInput {
    return {
      nombre: input.nombre.trim(),
      nombreCorto: this.nullIfBlank(input.nombreCorto),
      direccion: this.nullIfBlank(input.direccion),
      telefono: this.nullIfBlank(input.telefono),
      correo: this.nullIfBlank(input.correo),
      logoUrl: this.nullIfBlank(input.logoUrl),
      identificadores: input.identificadores
    };
  }

  private nullIfBlank(value: string | null): string | null {
    const normalized = value?.trim();
    return normalized ? normalized : null;
  }

  private async solicitar(method: string, path: string, body?: unknown): Promise<unknown> {
    try {
      return await firstValueFrom(this.http.request<unknown>(method, `${this.baseUrl}${path}`, { body }));
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse) {
        const payload: unknown = error.error;
        const code = this.esRegistro(payload) && typeof payload['code'] === 'string'
          ? payload['code'] : (error.status === 403 ? '42501' : 'UNKNOWN');
        const message = this.esRegistro(payload) && typeof payload['error'] === 'string'
          ? payload['error'] : undefined;
        throw this.mapError({ code, message });
      }
      throw this.mapError({});
    }
  }

  private mapError(error: ConfiguracionApiError): ConfiguracionError {
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
