import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

type ConfigTab = 'ciclos' | 'jornadas' | 'niveles' | 'grados' | 'secciones' | 'planes';

@Component({
  selector: 'app-configuracion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './configuracion.html',
  styleUrl: './configuracion.css'
})
export class Configuracion implements OnInit {
  tabActual: ConfigTab = 'ciclos';
  ciclos: any[] = [];
  jornadas: any[] = [];
  niveles: any[] = [];
  grados: any[] = [];
  secciones: any[] = [];
  tiposPlanPago: any[] = [];
  planesPago: any[] = [];
  cargando = false;
  mensaje = '';
  mensajeTipo: 'success' | 'error' = 'success';

  formularioActivo: ConfigTab | '' = '';
  editandoId = '';
  ciclo = this.crearCicloVacio();
  catalogo = this.crearCatalogoVacio();
  seccion = this.crearSeccionVacia();
  plan = this.crearPlanVacio();

  readonly tabs = [
    { id: 'ciclos' as ConfigTab, label: 'Ciclos' },
    { id: 'jornadas' as ConfigTab, label: 'Jornadas' },
    { id: 'niveles' as ConfigTab, label: 'Niveles' },
    { id: 'grados' as ConfigTab, label: 'Grados' },
    { id: 'secciones' as ConfigTab, label: 'Secciones' },
    { id: 'planes' as ConfigTab, label: 'Planes' }
  ];

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    await this.cargarTodo();
  }

  async cargarTodo() {
    this.cargando = true;
    this.cdr.detectChanges();

    try {
      const [ciclos, jornadas, niveles, grados, secciones, tiposPlanPago, planesPago] = await Promise.all([
        this.auth.apiRequest<any[]>('/configuracion/ciclos'),
        this.auth.apiRequest<any[]>('/configuracion/jornadas'),
        this.auth.apiRequest<any[]>('/configuracion/niveles'),
        this.auth.apiRequest<any[]>('/configuracion/grados'),
        this.auth.apiRequest<any[]>('/configuracion/secciones'),
        this.auth.apiRequest<any[]>('/configuracion/tipos-plan-pago'),
        this.auth.apiRequest<any[]>('/planes-pago?incluirInactivos=true')
      ]);

      this.ciclos = this.asArray(ciclos);
      this.jornadas = this.asArray(jornadas);
      this.niveles = this.asArray(niveles);
      this.grados = this.asArray(grados);
      this.secciones = this.asArray(secciones);
      this.tiposPlanPago = this.asArray(tiposPlanPago);
      this.planesPago = this.asArray(planesPago);
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo cargar la configuracion.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  cambiarTab(tab: ConfigTab) {
    this.tabActual = tab;
    this.cancelarFormulario();
  }

  abrirNuevo(tipo: ConfigTab) {
    this.formularioActivo = tipo;
    this.editandoId = '';

    if (tipo === 'ciclos') this.ciclo = this.crearCicloVacio();
    if (tipo === 'jornadas' || tipo === 'niveles' || tipo === 'grados') this.catalogo = this.crearCatalogoVacio();
    if (tipo === 'secciones') this.seccion = this.crearSeccionVacia();
    if (tipo === 'planes') this.plan = this.crearPlanVacio();

    this.cdr.detectChanges();
  }

  editarCiclo(ciclo: any) {
    this.formularioActivo = 'ciclos';
    this.editandoId = ciclo.id;
    this.ciclo = {
      nombre: ciclo.nombre || '',
      fechaInicio: ciclo.fechaInicio || ciclo.fecha_inicio || '',
      fechaFin: ciclo.fechaFin || ciclo.fecha_fin || '',
      matriculaInicio: ciclo.matriculaInicio || ciclo.matricula_inicio || '',
      matriculaFin: ciclo.matriculaFin || ciclo.matricula_fin || '',
      activo: ciclo.activo !== false
    };
    this.cdr.detectChanges();
  }

  editarCatalogo(tipo: 'jornadas' | 'niveles' | 'grados', item: any) {
    this.formularioActivo = tipo;
    this.editandoId = item.id;
    this.catalogo = {
      nombre: item.nombre || '',
      orden: item.orden ?? 0,
      nivelId: item.nivelId || item.nivel_id || '',
      activo: item.activo !== false
    };
    this.cdr.detectChanges();
  }

  editarSeccion(item: any) {
    this.formularioActivo = 'secciones';
    this.editandoId = item.id;
    this.seccion = {
      nombre: item.nombre || '',
      gradoId: item.gradoId || item.grado_id || '',
      jornadaId: item.jornadaId || item.jornada_id || '',
      cupo: item.cupo ?? null,
      activo: item.activo !== false
    };
    this.cdr.detectChanges();
  }

  editarPlan(item: any) {
    this.formularioActivo = 'planes';
    this.editandoId = item.id;
    this.plan = {
      nombre: item.nombre || '',
      tipo: item.tipo || 'mensual',
      tipoPlanPagoId: item.tipoPlanPagoId || item.tipo_plan_pago_id || '',
      descripcion: item.descripcion || '',
      montoMatricula: item.montoMatricula ?? item.monto_matricula ?? 0,
      montoTotalAnual: item.montoTotalAnual ?? item.monto_total_anual ?? 0,
      cantidadCuotas: item.cantidadCuotas ?? item.cantidad_cuotas ?? 10,
      mesInicio: item.mesInicio ?? item.mes_inicio ?? 1,
      diaVencimiento: item.diaVencimiento ?? item.dia_vencimiento ?? 10,
      descuentoPorcentaje: item.descuentoPorcentaje ?? item.descuento_porcentaje ?? 0,
      activo: item.activo !== false
    };
    this.cdr.detectChanges();
  }

  async guardarCiclo() {
    if (!this.ciclo.nombre || !this.ciclo.fechaInicio || !this.ciclo.fechaFin || !this.ciclo.matriculaInicio || !this.ciclo.matriculaFin) {
      this.mostrarMensaje('Nombre, fechas del ciclo y periodo de matricula son obligatorios.', 'error');
      return;
    }

    if (this.ciclo.fechaInicio > this.ciclo.fechaFin) {
      this.mostrarMensaje('La fecha de inicio del ciclo no puede ser mayor a la fecha final.', 'error');
      return;
    }

    if (this.ciclo.matriculaInicio > this.ciclo.matriculaFin) {
      this.mostrarMensaje('La fecha de inicio de matricula no puede ser mayor a la fecha final.', 'error');
      return;
    }

    await this.guardar('/configuracion/ciclos', this.ciclo, 'Ciclo');
  }

  async guardarCatalogo(tipo: 'jornadas' | 'niveles' | 'grados') {
    if (!this.catalogo.nombre) {
      this.mostrarMensaje('El nombre es obligatorio.', 'error');
      return;
    }

    const payload: any = {
      nombre: this.catalogo.nombre,
      activo: this.catalogo.activo
    };

    if (tipo === 'niveles' || tipo === 'grados') {
      payload.orden = Number(this.catalogo.orden) || 0;
    }

    if (tipo === 'grados') {
      payload.nivelId = this.catalogo.nivelId || null;
    }

    await this.guardar(`/configuracion/${tipo}`, payload, this.nombreEntidad(tipo));
  }

  async guardarSeccion() {
    if (!this.seccion.nombre || !this.seccion.gradoId || !this.seccion.jornadaId) {
      this.mostrarMensaje('Nombre, grado y jornada son obligatorios para crear una seccion.', 'error');
      return;
    }

    await this.guardar('/configuracion/secciones', {
      nombre: this.seccion.nombre,
      gradoId: this.seccion.gradoId,
      jornadaId: this.seccion.jornadaId,
      cupo: this.seccion.cupo ? Number(this.seccion.cupo) : null,
      activo: this.seccion.activo
    }, 'Seccion');
  }

  async guardarPlan() {
    if (!this.plan.nombre || !this.plan.tipo) {
      this.mostrarMensaje('Nombre y tipo del plan son obligatorios.', 'error');
      return;
    }

    if (Number(this.plan.cantidadCuotas) <= 0) {
      this.mostrarMensaje('La cantidad de cuotas debe ser mayor a cero.', 'error');
      return;
    }

    await this.guardar('/planes-pago', {
      ...this.plan,
      tipoPlanPagoId: this.plan.tipoPlanPagoId || null,
      montoMatricula: Number(this.plan.montoMatricula) || 0,
      montoTotalAnual: Number(this.plan.montoTotalAnual) || 0,
      cantidadCuotas: Number(this.plan.cantidadCuotas) || 1,
      mesInicio: Number(this.plan.mesInicio) || 1,
      diaVencimiento: Number(this.plan.diaVencimiento) || 10,
      descuentoPorcentaje: Number(this.plan.descuentoPorcentaje) || 0
    }, 'Plan de pago');
  }

  async desactivar(catalogo: ConfigTab, id: string) {
    if (!confirm(`Desactivar ${this.nombreEntidad(catalogo).toLowerCase()}?`)) {
      return;
    }

    await this.eliminar(catalogo, id, false);
  }

  async eliminarPermanente(catalogo: ConfigTab, id: string) {
    if (!confirm(`Eliminar definitivamente ${this.nombreEntidad(catalogo).toLowerCase()}? Esta accion fallara si tiene registros relacionados.`)) {
      return;
    }

    await this.eliminar(catalogo, id, true);
  }

  cancelarFormulario() {
    this.formularioActivo = '';
    this.editandoId = '';
    this.ciclo = this.crearCicloVacio();
    this.catalogo = this.crearCatalogoVacio();
    this.seccion = this.crearSeccionVacia();
    this.plan = this.crearPlanVacio();
    this.cdr.detectChanges();
  }

  periodoEstado(ciclo: any): string {
    const inicio = ciclo.matriculaInicio || ciclo.matricula_inicio;
    const fin = ciclo.matriculaFin || ciclo.matricula_fin;
    if (!inicio || !fin) return 'Sin configurar';

    const hoy = new Date().toISOString().slice(0, 10);
    if (hoy < inicio) return 'Pendiente';
    if (hoy > fin) return 'Cerrado';
    return 'Abierto';
  }

  periodoClase(ciclo: any): string {
    return this.periodoEstado(ciclo).toLowerCase().split(' ')[0];
  }

  nombreCatalogo(items: any[], id: string): string {
    return items.find(item => item.id === id)?.nombre ?? '-';
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }

  private async guardar(urlBase: string, payload: any, entidad: string) {
    this.cargando = true;

    try {
      const url = this.editandoId ? `${urlBase}/${this.editandoId}` : urlBase;
      await this.auth.apiRequest(url, {
        method: this.editandoId ? 'PUT' : 'POST',
        body: JSON.stringify(payload)
      });

      this.mostrarMensaje(this.editandoId ? `${entidad} actualizado correctamente.` : `${entidad} creado correctamente.`, 'success');
      this.cancelarFormulario();
      await this.cargarTodo();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : `No se pudo guardar ${entidad.toLowerCase()}.`, 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  private async eliminar(catalogo: ConfigTab, id: string, permanente: boolean) {
    this.cargando = true;

    try {
      const base = catalogo === 'planes' ? '/planes-pago' : `/configuracion/${catalogo}`;
      const suffix = permanente ? '?permanente=true' : '';
      await this.auth.apiRequest(`${base}/${id}${suffix}`, { method: 'DELETE' });
      this.mostrarMensaje(permanente ? 'Registro eliminado correctamente.' : 'Registro desactivado correctamente.', 'success');
      await this.cargarTodo();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo completar la accion.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  private nombreEntidad(catalogo: ConfigTab | 'jornadas' | 'niveles' | 'grados'): string {
    const nombres: Record<string, string> = {
      ciclos: 'Ciclo escolar',
      jornadas: 'Jornada',
      niveles: 'Nivel',
      grados: 'Grado',
      secciones: 'Seccion',
      planes: 'Plan de pago'
    };

    return nombres[catalogo] ?? 'Registro';
  }

  private asArray(data: any): any[] {
    return Array.isArray(data) ? [...data] : [];
  }

  private crearCicloVacio() {
    return {
      nombre: '',
      fechaInicio: '',
      fechaFin: '',
      matriculaInicio: '',
      matriculaFin: '',
      activo: true
    };
  }

  private crearCatalogoVacio() {
    return {
      nombre: '',
      orden: 0,
      nivelId: '',
      activo: true
    };
  }

  private crearSeccionVacia() {
    return {
      nombre: '',
      gradoId: '',
      jornadaId: '',
      cupo: null as number | null,
      activo: true
    };
  }

  private crearPlanVacio() {
    return {
      nombre: '',
      tipo: 'mensual',
      tipoPlanPagoId: '',
      descripcion: '',
      montoMatricula: 0,
      montoTotalAnual: 0,
      cantidadCuotas: 10,
      mesInicio: 1,
      diaVencimiento: 10,
      descuentoPorcentaje: 0,
      activo: true
    };
  }

  private mostrarMensaje(texto: string, tipo: 'success' | 'error') {
    this.mensaje = texto;
    this.mensajeTipo = tipo;
    this.cdr.detectChanges();

    setTimeout(() => {
      this.mensaje = '';
      this.cdr.detectChanges();
    }, 4200);
  }
}
