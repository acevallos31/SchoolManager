import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import {
  ConfiguracionFinancieraError, ConfiguracionFinancieraService,
  ConceptoFinanciero, PlanCuota, PlanInput, PlanPago,
} from '../../core/services/configuracion-financiera.service';

@Component({
  selector: 'app-configuracion-planes-pago',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './configuracion-planes-pago.html',
  styleUrl: './configuracion-planes-pago.css',
})
export class ConfiguracionPlanesPago implements OnInit {
  planes: PlanPago[] = [];
  conceptos: ConceptoFinanciero[] = [];
  planForm: PlanInput = { nombre: '', descripcion: null, cuotas: [] };
  editando: PlanPago | null = null;
  mostrarForm = false;
  cargando = false;
  guardando = false;
  mensaje = '';
  esError = false;

  constructor(
    private router: Router,
    private auth: AuthService,
    private service: ConfiguracionFinancieraService,
    private cdr: ChangeDetectorRef,
  ) {}

  get puedeCrear() { return this.auth.tienePermiso('configuracion.planes_pago.crear'); }
  get puedeEditar() { return this.auth.tienePermiso('configuracion.planes_pago.editar'); }
  get puedeDesactivar() { return this.auth.tienePermiso('configuracion.planes_pago.desactivar'); }
  get totalForm(): number { return this.planForm.cuotas.reduce((s, c) => s + (c.monto || 0), 0); }

  async ngOnInit(): Promise<void> {
    await Promise.all([this.cargarPlanes(), this.cargarConceptos()]);
  }

  async cargarPlanes(): Promise<void> {
    this.cargando = true;
    try { this.planes = await this.service.listarPlanes(); }
    catch (e) { this.error(e); }
    finally { this.cargando = false; this.cdr.detectChanges(); }
  }

  async cargarConceptos(): Promise<void> {
    try { this.conceptos = await this.service.listarConceptos(); }
    catch { /* el select de cuotas se muestra sin conceptos si falla */ }
    finally { this.cdr.detectChanges(); }
  }

  async abrirForm(p?: PlanPago): Promise<void> {
    this.editando = p ?? null;
    this.mostrarForm = true;
    this.mensaje = '';
    if (p) {
      try {
        const detalle = await this.service.obtenerPlan(p.id);
        this.planForm = {
          nombre: detalle.nombre,
          descripcion: detalle.descripcion,
          cuotas: detalle.cuotas.map(c => ({
            orden: c.orden, conceptoId: c.conceptoId, descripcion: c.descripcion,
            monto: c.monto, vencimientoDias: c.vencimientoDias,
          })),
        };
      } catch (e) { this.error(e); this.mostrarForm = false; }
    } else {
      this.planForm = { nombre: '', descripcion: null, cuotas: this.nuevaCuota() };
    }
    this.cdr.detectChanges();
  }

  private nuevaCuota(): PlanCuota[] {
    return [{ orden: 1, conceptoId: null, descripcion: null, monto: 0, vencimientoDias: 30 }];
  }

  agregarCuota(): void {
    const proximo = this.planForm.cuotas.length
      ? Math.max(...this.planForm.cuotas.map(c => c.orden)) + 1 : 1;
    this.planForm.cuotas.push({ orden: proximo, conceptoId: null, descripcion: null, monto: 0, vencimientoDias: 30 });
  }

  quitarCuota(i: number): void { this.planForm.cuotas.splice(i, 1); }

  async guardar(): Promise<void> {
    if (this.guardando || !this.validar()) return;
    this.guardando = true;
    try {
      // Reordena las cuotas según su posición en el formulario.
      this.planForm.cuotas = this.planForm.cuotas.map((c, i) => ({ ...c, orden: i + 1 }));
      if (this.editando) await this.service.actualizarPlan(this.editando.id, this.planForm);
      else await this.service.crearPlan(this.planForm);
      this.mostrarForm = false;
      this.exito('Plan de pago guardado correctamente.');
      await this.cargarPlanes();
    } catch (e) { this.error(e); }
    finally { this.guardando = false; this.cdr.detectChanges(); }
  }

  async cambiarEstado(p: PlanPago): Promise<void> {
    try {
      if (p.activo) {
        const motivo = window.prompt('Motivo de desactivación:')?.trim();
        if (!motivo) return;
        await this.service.desactivarPlan(p.id, motivo);
      } else {
        await this.service.reactivarPlan(p.id);
      }
      await this.cargarPlanes();
    } catch (e) { this.error(e); this.cdr.detectChanges(); }
  }

  volver(): void { void this.router.navigate(['/configuracion']); }

  // expuesto para testing y reutilización desde la plantilla
  validar(): boolean {
    if (!this.planForm.nombre.trim() || this.planForm.cuotas.length === 0) {
      this.error(new ConfiguracionFinancieraError('El nombre es obligatorio y el plan debe incluir al menos una cuota.'));
      return false;
    }
    if (this.planForm.cuotas.some(c => c.monto < 0 || c.vencimientoDias < 0)) {
      this.error(new ConfiguracionFinancieraError('El monto y el vencimiento de cada cuota no pueden ser negativos.'));
      return false;
    }
    return true;
  }

  private error(e: unknown): void {
    this.mensaje = e instanceof ConfiguracionFinancieraError ? e.message : 'No se pudo completar la operación.';
    this.esError = true;
  }

  private exito(m: string): void { this.mensaje = m; this.esError = false; }
}