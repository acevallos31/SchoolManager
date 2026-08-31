import { inject, Injectable } from '@angular/core';
import { SUPABASE_CLIENT } from './auth';

export interface CicloEscolar { id:string; institucionId:string; nombre:string; fechaInicio:string; fechaFin:string; activo:boolean; motivoDesactivacion:string|null; }
export interface PeriodoMatricula { id:string; cicloId:string; nombre:string; tipo:string|null; fechaInicio:string; fechaFin:string; activo:boolean; }
export interface CicloInput { nombre:string; fechaInicio:string; fechaFin:string; }
export interface PeriodoInput { nombre:string; tipo:string|null; fechaInicio:string; fechaFin:string; }
interface ErrorRpc { code?:string; message?:string; }
interface CicloRow { id:string; institucion_id:string; nombre:string; fecha_inicio:string; fecha_fin:string; activo:boolean; motivo_desactivacion:string|null; }
interface PeriodoRow { id:string; ciclo_id:string; nombre:string; tipo:string|null; fecha_inicio:string; fecha_fin:string; activo:boolean; }

export class CicloEscolarError extends Error {
  constructor(message:string, public readonly code:string) { super(message); this.name='CicloEscolarError'; }
}

@Injectable({providedIn:'root'})
export class CicloEscolarService {
  private readonly supabase=inject(SUPABASE_CLIENT);

  async listar(institucionId?:string):Promise<CicloEscolar[]> {
    const {data,error}=await this.supabase.rpc('rpc_listar_ciclos_escolares',institucionId?{p_institucion_id:institucionId}:undefined);
    if(error) throw this.mapError(error);
    return ((data??[]) as CicloRow[]).map(c=>({id:c.id,institucionId:c.institucion_id,nombre:c.nombre,fechaInicio:c.fecha_inicio,fechaFin:c.fecha_fin,activo:c.activo,motivoDesactivacion:c.motivo_desactivacion}));
  }
  async crear(input:CicloInput,institucionId?:string):Promise<string> {
    const {data,error}=await this.supabase.rpc('rpc_crear_ciclo_escolar',{p_nombre:input.nombre.trim(),p_fecha_inicio:input.fechaInicio,p_fecha_fin:input.fechaFin,p_institucion_id:institucionId??null});
    if(error) throw this.mapError(error); return data as string;
  }
  async actualizar(ciclo:CicloEscolar,input:CicloInput):Promise<void> { await this.rpcVoid('rpc_actualizar_ciclo_escolar',{p_ciclo_id:ciclo.id,p_nombre:input.nombre.trim(),p_fecha_inicio:input.fechaInicio,p_fecha_fin:input.fechaFin,p_activo:ciclo.activo,p_motivo_desactivacion:null}); }
  async desactivar(id:string,motivo:string):Promise<void> { await this.rpcVoid('rpc_desactivar_ciclo_escolar',{p_ciclo_id:id,p_motivo:motivo.trim()}); }
  async reactivar(id:string):Promise<void> { await this.rpcVoid('rpc_reactivar_ciclo_escolar',{p_ciclo_id:id}); }
  async listarPeriodos(cicloId:string):Promise<PeriodoMatricula[]> {
    const {data,error}=await this.supabase.rpc('rpc_listar_periodos_matricula',{p_ciclo_id:cicloId});
    if(error) throw this.mapError(error);
    return ((data??[]) as PeriodoRow[]).map(p=>({id:p.id,cicloId:p.ciclo_id,nombre:p.nombre,tipo:p.tipo,fechaInicio:p.fecha_inicio,fechaFin:p.fecha_fin,activo:p.activo}));
  }
  async crearPeriodo(cicloId:string,input:PeriodoInput):Promise<string> { const {data,error}=await this.supabase.rpc('rpc_crear_periodo_matricula',{p_ciclo_id:cicloId,p_nombre:input.nombre.trim(),p_tipo:this.blank(input.tipo),p_fecha_inicio:input.fechaInicio,p_fecha_fin:input.fechaFin}); if(error) throw this.mapError(error); return data as string; }
  async actualizarPeriodo(periodo:PeriodoMatricula,input:PeriodoInput):Promise<void> { await this.rpcVoid('rpc_actualizar_periodo_matricula',{p_periodo_id:periodo.id,p_nombre:input.nombre.trim(),p_tipo:this.blank(input.tipo),p_fecha_inicio:input.fechaInicio,p_fecha_fin:input.fechaFin,p_activo:periodo.activo}); }
  async desactivarPeriodo(id:string):Promise<void> { await this.rpcVoid('rpc_desactivar_periodo_matricula',{p_periodo_id:id}); }
  async reactivarPeriodo(id:string):Promise<void> { await this.rpcVoid('rpc_reactivar_periodo_matricula',{p_periodo_id:id}); }
  private async rpcVoid(nombre:string,parametros:Record<string,unknown>):Promise<void> { const {error}=await this.supabase.rpc(nombre,parametros); if(error) throw this.mapError(error); }
  private blank(v:string|null):string|null { const x=v?.trim(); return x||null; }
  private mapError(e:ErrorRpc):CicloEscolarError { if(e.code==='42501') return new CicloEscolarError('No tienes permiso para realizar esta operación.',e.code); if(e.code==='SM003') return new CicloEscolarError('Seleccione una institución para continuar.',e.code); if(e.code==='23505') return new CicloEscolarError('Ya existe un registro con ese nombre.',e.code); if(e.code==='22023') return new CicloEscolarError(e.message||'Las fechas o datos no son válidos.',e.code); if(e.code==='P0002') return new CicloEscolarError('El ciclo o período no existe.',e.code); return new CicloEscolarError('No se pudo completar la operación.',e.code??'UNKNOWN'); }
}
