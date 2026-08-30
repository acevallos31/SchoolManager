import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import { CicloEscolar, CicloEscolarError, CicloEscolarService, CicloInput, PeriodoInput, PeriodoMatricula } from '../../core/services/ciclo-escolar.service';

@Component({selector:'app-configuracion-ciclos',standalone:true,imports:[CommonModule,FormsModule],templateUrl:'./configuracion-ciclos.html',styleUrl:'./configuracion-ciclos.css'})
export class ConfiguracionCiclos implements OnInit {
  ciclos:CicloEscolar[]=[]; periodos:PeriodoMatricula[]=[]; cicloSeleccionado:CicloEscolar|null=null;
  cicloForm:CicloInput={nombre:'',fechaInicio:'',fechaFin:''}; periodoForm:PeriodoInput={nombre:'',tipo:'',fechaInicio:'',fechaFin:''};
  editandoCiclo:CicloEscolar|null=null; editandoPeriodo:PeriodoMatricula|null=null; mostrarCiclo=false; mostrarPeriodo=false;
  cargando=false; guardando=false; mensaje=''; esError=false;
  constructor(private router:Router,private auth:AuthService,private service:CicloEscolarService,private cdr:ChangeDetectorRef){}
  get puedeCrearCiclo(){return this.auth.tienePermiso('configuracion.ciclos.crear');} get puedeEditarCiclo(){return this.auth.tienePermiso('configuracion.ciclos.editar');} get puedeDesactivarCiclo(){return this.auth.tienePermiso('configuracion.ciclos.desactivar');}
  get puedeCrearPeriodo(){return this.auth.tienePermiso('configuracion.periodos_matricula.crear');} get puedeEditarPeriodo(){return this.auth.tienePermiso('configuracion.periodos_matricula.editar');} get puedeDesactivarPeriodo(){return this.auth.tienePermiso('configuracion.periodos_matricula.desactivar');}
  async ngOnInit(){await this.cargarCiclos();}
  async cargarCiclos(){this.cargando=true;try{this.ciclos=await this.service.listar();}catch(e){this.error(e);}finally{this.cargando=false;this.cdr.detectChanges();}}
  abrirCiclo(c?:CicloEscolar){this.editandoCiclo=c??null;this.cicloForm=c?{nombre:c.nombre,fechaInicio:c.fechaInicio,fechaFin:c.fechaFin}:{nombre:'',fechaInicio:'',fechaFin:''};this.mostrarCiclo=true;}
  async guardarCiclo(){if(this.guardando||!this.validar(this.cicloForm))return;this.guardando=true;try{if(this.editandoCiclo)await this.service.actualizar(this.editandoCiclo,this.cicloForm);else await this.service.crear(this.cicloForm);this.mostrarCiclo=false;this.exito('Ciclo guardado correctamente.');await this.cargarCiclos();}catch(e){this.error(e);}finally{this.guardando=false;this.cdr.detectChanges();}}
  async cambiarCiclo(c:CicloEscolar){try{if(c.activo){const motivo=window.prompt('Motivo de desactivación:')?.trim();if(!motivo)return;await this.service.desactivar(c.id,motivo);}else await this.service.reactivar(c.id);await this.cargarCiclos();}catch(e){this.error(e);this.cdr.detectChanges();}}
  async gestionarPeriodos(c:CicloEscolar){this.cicloSeleccionado=c;this.cargando=true;try{this.periodos=await this.service.listarPeriodos(c.id);}catch(e){this.error(e);}finally{this.cargando=false;this.cdr.detectChanges();}}
  abrirPeriodo(p?:PeriodoMatricula){if(!this.cicloSeleccionado)return;this.editandoPeriodo=p??null;this.periodoForm=p?{nombre:p.nombre,tipo:p.tipo??'',fechaInicio:p.fechaInicio,fechaFin:p.fechaFin}:{nombre:'',tipo:'',fechaInicio:this.cicloSeleccionado.fechaInicio,fechaFin:this.cicloSeleccionado.fechaFin};this.mostrarPeriodo=true;}
  async guardarPeriodo(){const c=this.cicloSeleccionado;if(!c||this.guardando||!this.validar(this.periodoForm))return;if(this.periodoForm.fechaInicio<c.fechaInicio||this.periodoForm.fechaFin>c.fechaFin){this.error(new CicloEscolarError('El período debe estar dentro de las fechas del ciclo.','22023'));return;}this.guardando=true;try{if(this.editandoPeriodo)await this.service.actualizarPeriodo(this.editandoPeriodo,this.periodoForm);else await this.service.crearPeriodo(c.id,this.periodoForm);this.mostrarPeriodo=false;this.exito('Período guardado correctamente.');await this.gestionarPeriodos(c);}catch(e){this.error(e);}finally{this.guardando=false;this.cdr.detectChanges();}}
  async cambiarPeriodo(p:PeriodoMatricula){try{if(p.activo)await this.service.desactivarPeriodo(p.id);else await this.service.reactivarPeriodo(p.id);if(this.cicloSeleccionado)await this.gestionarPeriodos(this.cicloSeleccionado);}catch(e){this.error(e);this.cdr.detectChanges();}}
  volver(){void this.router.navigate(['/configuracion']);}
  private validar(v:{nombre:string;fechaInicio:string;fechaFin:string}){if(!v.nombre.trim()||!v.fechaInicio||!v.fechaFin||v.fechaInicio>v.fechaFin){this.error(new CicloEscolarError('Nombre y rango de fechas son obligatorios y deben ser válidos.','22023'));return false;}return true;}
  private error(e:unknown){this.mensaje=e instanceof CicloEscolarError?e.message:'No se pudo completar la operación.';this.esError=true;}
  private exito(m:string){this.mensaje=m;this.esError=false;}
}
