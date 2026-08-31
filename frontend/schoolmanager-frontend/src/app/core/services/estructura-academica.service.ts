import { inject, Injectable } from '@angular/core';
import { SUPABASE_CLIENT } from './auth';

export interface Grado { id: string; nombre: string; orden: number; activo: boolean; }
export interface Jornada { id: string; nombre: string; activo: boolean; }
export interface Seccion {
  id: string; institucionId: string; cicloId: string; gradoId: string; gradoNombre: string;
  jornadaId: string | null; jornadaNombre: string | null; nombre: string; cupo: number | null;
  activo: boolean; fechaDesactivacion: string | null; motivoDesactivacion: string | null;
}
export interface GradoInput { nombre: string; orden: number; }
export interface JornadaInput { nombre: string; }
export interface SeccionInput { cicloId: string; gradoId: string; jornadaId: string | null; nombre: string; cupo: number | null; }

interface GradoRow { id: string; nombre: string; orden: number; activo: boolean; }
interface JornadaRow { id: string; nombre: string; activo: boolean; }
interface SeccionRow {
  id: string; institucion_id: string; ciclo_id: string; grado_id: string; grado_nombre: string;
  jornada_id: string | null; jornada_nombre: string | null; nombre: string; cupo: number | null;
  activo: boolean; fecha_desactivacion: string | null; motivo_desactivacion: string | null;
}
interface RpcError { code?: string; message?: string; }

export class EstructuraAcademicaError extends Error {
  constructor(message: string, public readonly code: string) { super(message); this.name = 'EstructuraAcademicaError'; }
}

@Injectable({ providedIn: 'root' })
export class EstructuraAcademicaService {
  private readonly supabase = inject(SUPABASE_CLIENT);

  async listarGrados(institucionId?: string): Promise<Grado[]> {
    const { data, error } = await this.supabase.rpc('rpc_listar_grados', this.contexto(institucionId));
    if (error) throw this.mapError(error);
    return (data ?? []) as GradoRow[];
  }
  async crearGrado(input: GradoInput, institucionId?: string): Promise<string> {
    return this.rpcId('rpc_crear_grado', { p_nombre: input.nombre.trim(), p_orden: input.orden, ...this.contexto(institucionId) });
  }
  async actualizarGrado(gradoId: string, input: GradoInput, institucionId?: string): Promise<void> {
    await this.rpcVoid('rpc_actualizar_grado', { p_grado_id: gradoId, p_nombre: input.nombre.trim(), p_orden: input.orden, ...this.contexto(institucionId) });
  }
  async desactivarGrado(gradoId: string, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_desactivar_grado', { p_grado_id: gradoId, ...this.contexto(institucionId) }); }
  async reactivarGrado(gradoId: string, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_reactivar_grado', { p_grado_id: gradoId, ...this.contexto(institucionId) }); }

  async listarJornadas(institucionId?: string): Promise<Jornada[]> {
    const { data, error } = await this.supabase.rpc('rpc_listar_jornadas', this.contexto(institucionId));
    if (error) throw this.mapError(error);
    return (data ?? []) as JornadaRow[];
  }
  async crearJornada(input: JornadaInput, institucionId?: string): Promise<string> { return this.rpcId('rpc_crear_jornada', { p_nombre: input.nombre.trim(), ...this.contexto(institucionId) }); }
  async actualizarJornada(jornadaId: string, input: JornadaInput, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_actualizar_jornada', { p_jornada_id: jornadaId, p_nombre: input.nombre.trim(), ...this.contexto(institucionId) }); }
  async desactivarJornada(jornadaId: string, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_desactivar_jornada', { p_jornada_id: jornadaId, ...this.contexto(institucionId) }); }
  async reactivarJornada(jornadaId: string, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_reactivar_jornada', { p_jornada_id: jornadaId, ...this.contexto(institucionId) }); }

  async listarSecciones(cicloId: string, institucionId?: string): Promise<Seccion[]> {
    const { data, error } = await this.supabase.rpc('rpc_listar_secciones', { p_ciclo_id: cicloId, ...this.contexto(institucionId) });
    if (error) throw this.mapError(error);
    return ((data ?? []) as SeccionRow[]).map(row => ({ id: row.id, institucionId: row.institucion_id, cicloId: row.ciclo_id, gradoId: row.grado_id, gradoNombre: row.grado_nombre, jornadaId: row.jornada_id, jornadaNombre: row.jornada_nombre, nombre: row.nombre, cupo: row.cupo, activo: row.activo, fechaDesactivacion: row.fecha_desactivacion, motivoDesactivacion: row.motivo_desactivacion }));
  }
  async crearSeccion(input: SeccionInput, institucionId?: string): Promise<string> { return this.rpcId('rpc_crear_seccion', { p_institucion_id: institucionId ?? null, p_ciclo_id: input.cicloId, p_grado_id: input.gradoId, p_jornada_id: input.jornadaId, p_nombre: input.nombre.trim(), p_cupo: input.cupo }); }
  async actualizarSeccion(seccionId: string, input: SeccionInput, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_actualizar_seccion', { p_seccion_id: seccionId, p_ciclo_id: input.cicloId, p_grado_id: input.gradoId, p_jornada_id: input.jornadaId, p_nombre: input.nombre.trim(), p_cupo: input.cupo, p_institucion_id: institucionId ?? null }); }
  async desactivarSeccion(seccionId: string, motivo: string, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_desactivar_seccion', { p_seccion_id: seccionId, p_motivo: motivo.trim(), p_institucion_id: institucionId ?? null }); }
  async reactivarSeccion(seccionId: string, institucionId?: string): Promise<void> { await this.rpcVoid('rpc_reactivar_seccion', { p_seccion_id: seccionId, p_institucion_id: institucionId ?? null }); }

  private contexto(institucionId?: string): Record<string, string | null> { return { p_institucion_id: institucionId ?? null }; }
  private async rpcId(nombre: string, parametros: Record<string, unknown>): Promise<string> { const { data, error } = await this.supabase.rpc(nombre, parametros); if (error) throw this.mapError(error); return data as string; }
  private async rpcVoid(nombre: string, parametros: Record<string, unknown>): Promise<void> { const { error } = await this.supabase.rpc(nombre, parametros); if (error) throw this.mapError(error); }
  private mapError(error: RpcError): EstructuraAcademicaError {
    const messages: Record<string, string> = { '42501': 'No tienes permiso para realizar esta acción.', '23505': 'Ya existe un registro con ese nombre o contexto.', '23503': 'El ciclo, grado o jornada no existe o está inactivo.', '23514': 'La sección tiene matrículas y no puede cambiar ciclo, grado ni jornada.', '22023': error.message || 'Los datos ingresados no son válidos.', 'SM003': 'Seleccione una institución para continuar.', 'P0002': 'El recurso no existe o no pertenece a la institución actual.' };
    return new EstructuraAcademicaError(messages[error.code ?? ''] ?? 'No se pudo completar la operación.', error.code ?? 'UNKNOWN');
  }
}