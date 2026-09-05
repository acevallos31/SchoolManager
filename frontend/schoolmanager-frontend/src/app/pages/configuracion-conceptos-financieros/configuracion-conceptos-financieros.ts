import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import {
  ConceptoFinanciero, ConceptoInput, ConfiguracionFinancieraError,
  ConfiguracionFinancieraService,
} from '../../core/services/configuracion-financiera.service';

@Component({
  selector: 'app-configuracion-conceptos-financieros',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './configuracion-conceptos-financieros.html',
  styleUrl: './configuracion-conceptos-financieros.css',
})
export class ConfiguracionConceptosFinancieros implements OnInit {
  conceptos: ConceptoFinanciero[] = [];
  conceptoForm: ConceptoInput = { nombre: '', monto: 0, descripcion: null };
  editando: ConceptoFinanciero | null = null;
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

  get puedeCrear() { return this.auth.tienePermiso('configuracion.conceptos_financieros.crear'); }
  get puedeEditar() { return this.auth.tienePermiso('configuracion.conceptos_financieros.editar'); }
  get puedeDesactivar() { return this.auth.tienePermiso('configuracion.conceptos_financieros.desactivar'); }

  async ngOnInit(): Promise<void> { await this.cargar(); }

  async cargar(): Promise<void> {
    this.cargando = true;
    try {
      this.conceptos = await this.service.listarConceptos();
    } catch (e) { this.error(e); }
    finally { this.cargando = false; this.cdr.detectChanges(); }
  }

  abrirForm(c?: ConceptoFinanciero): void {
    this.editando = c ?? null;
    this.conceptoForm = c
      ? { nombre: c.nombre, monto: c.monto, descripcion: c.descripcion }
      : { nombre: '', monto: 0, descripcion: null };
    this.mostrarForm = true;
  }

  async guardar(): Promise<void> {
    if (this.guardando || !this.validar()) return;
    this.guardando = true;
    try {
      if (this.editando) await this.service.actualizarConcepto(this.editando.id, this.conceptoForm);
      else await this.service.crearConcepto(this.conceptoForm);
      this.mostrarForm = false;
      this.exito('Concepto guardado correctamente.');
      await this.cargar();
    } catch (e) { this.error(e); }
    finally { this.guardando = false; this.cdr.detectChanges(); }
  }

  async cambiarEstado(c: ConceptoFinanciero): Promise<void> {
    try {
      if (c.activo) {
        const motivo = window.prompt('Motivo de desactivación:')?.trim();
        if (!motivo) return;
        await this.service.desactivarConcepto(c.id, motivo);
      } else {
        await this.service.reactivarConcepto(c.id);
      }
      await this.cargar();
    } catch (e) { this.error(e); this.cdr.detectChanges(); }
  }

  volver(): void { void this.router.navigate(['/configuracion']); }

  // expuesto para testing y reutilización desde la plantilla
  validar(): boolean {
    if (!this.conceptoForm.nombre.trim() || this.conceptoForm.monto < 0) {
      this.error(new ConfiguracionFinancieraError('El nombre es obligatorio y el monto no puede ser negativo.'));
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