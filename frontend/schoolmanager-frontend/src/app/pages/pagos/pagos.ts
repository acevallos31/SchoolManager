import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth';
import { AlumnoListado, AlumnoService } from '../../core/services/alumno.service';
import { Cargo, CargoError, CargosService } from '../../core/services/cargos.service';
import { AplicacionRequest, PagosService, Pago, PagoError } from '../../core/services/pagos.service';
import { ImpresionService } from '../../core/services/impresion.service';
import { ReciboPagoDocumento } from '../../core/documents/recibo-pago.documento';
import {
  inicializarVistaFinanciera,
  sincronizarAlumnoFinancieroEnUrl,
} from '../../core/utils/alumno-contexto-financiero';

// Vista de pagos/cobranza (Bloque 021). Permite registrar un pago aplicado a
// uno o varios cargos del alumno (parcial o total) y anular un pago
// registrado, dejando trazabilidad. Fuera de alcance 021: caja avanzada,
// conciliacion bancaria, facturacion fiscal y saldo a favor.
@Component({
  selector: 'app-pagos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pagos.html',
  styleUrl: './pagos.css',
})
export class Pagos implements OnInit {
  alumnoId: string | null = null;
  alumnos: AlumnoListado[] = [];
  cargos: Cargo[] = [];
  pagos: Pago[] = [];
  cargando = false;
  registrando = false;
  mensaje = '';
  esError = false;
  showFormulario = false;

  // Formulario de registro.
  metodoPago = '';
  referenciaExterna = '';
  // monto por cargo (clave = cargo.id). Inicializado al abrir el formulario.
  montos: Record<string, string> = {};
  detalle: Pago | null = null;
  aplicaciones: { concepto: string | null; monto: number }[] = [];
  anulandoPagoId: string | null = null;
  motivoAnulacion = '';
  imprimiendoPagoId: string | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly alumnoService: AlumnoService,
    private readonly pagosService: PagosService,
    private readonly cargosService: CargosService,
    private readonly impresion: ImpresionService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  get puedeVer(): boolean { return this.auth.tienePermiso('academico.pagos.ver'); }
  get puedeRegistrar(): boolean { return this.auth.tienePermiso('academico.pagos.registrar'); }
  get puedeAnular(): boolean { return this.auth.tienePermiso('academico.pagos.anular'); }

  get cargoCobrables(): Cargo[] {
    return this.cargos.filter((c) => c.estado === 'pendiente' || c.estado === 'parcial');
  }

  get totalMontoTotal(): number {
    const m = this.montos;
    return this.cargoCobrables.reduce((acc, c) => acc + (Number(m[c.id]) || 0), 0);
  }

  get totalSeleccionadoValido(): boolean {
    const cobrables = this.cargoCobrables;
    if (cobrables.length === 0) return false;
    const montoTotal = this.totalMontoTotal;
    if (montoTotal <= 0) return false;
    // Ninguna aplicacion puede superar el saldo del cargo (rechaza sobrepago).
    for (const c of cobrables) {
      const m = Number(this.montos[c.id]) || 0;
      if (m < 0 || m > c.saldo + 1e-6) return false;
    }
    return true;
  }

  async ngOnInit(): Promise<void> {
    await inicializarVistaFinanciera(
      this,
      this.puedeVer,
      this.router,
      this.route,
      this.alumnoService,
      this.cdr,
      (error) => this.error(error),
    );
  }

  async seleccionarAlumno(): Promise<void> {
    this.cargos = [];
    this.pagos = [];
    this.detalle = null;
    this.aplicaciones = [];
    this.cerrarFormulario();
    this.mensaje = '';
    this.esError = false;

    await sincronizarAlumnoFinancieroEnUrl(this.router, this.route, this.alumnoId);
    if (this.alumnoId) await this.cargar();
  }

  async cargar(): Promise<void> {
    if (!this.alumnoId) return;
    this.cargando = true;
    this.mensaje = '';
    this.esError = false;
    try {
      const [cargos, pagos] = await Promise.all([
        this.cargosService.listarCargosAlumno(this.alumnoId),
        this.pagosService.listarPagosAlumno(this.alumnoId),
      ]);
      this.cargos = cargos;
      this.pagos = pagos;
    } catch (e: unknown) { this.error(e); }
    finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  volver(): void {
    if (this.alumnoId) {
      void this.router.navigate(['/cargos'], { queryParams: { alumnoId: this.alumnoId } });
    } else {
      void this.router.navigate(['/alumnos']);
    }
  }

  abrirFormulario(): void {
    this.showFormulario = true;
    this.metodoPago = '';
    this.referenciaExterna = '';
    this.montos = {};
    this.mensaje = '';
    this.esError = false;
  }

  cerrarFormulario(): void {
    this.showFormulario = false;
    this.montos = {};
  }

  async registrar(): Promise<void> {
    if (!this.alumnoId || !this.totalSeleccionadoValido || !this.puedeRegistrar) return;
    this.registrando = true;
    this.mensaje = '';
    this.esError = false;
    const aplicaciones: AplicacionRequest[] = this.cargoCobrables
      .map((c) => ({ cargoId: c.id, monto: Number(this.montos[c.id]) || 0 }))
      .filter((a) => a.monto > 0);
    try {
      await this.pagosService.registrarPago(this.alumnoId, {
        montoTotal: this.totalMontoTotal,
        aplicaciones,
        metodoPago: this.metodoPago || null,
        referenciaExterna: this.referenciaExterna || null,
      });
      this.cerrarFormulario();
      await this.cargar();
      this.mensaje = 'Pago registrado correctamente.';
      this.esError = false;
    } catch (e: unknown) { this.error(e); }
    finally { this.registrando = false; this.cdr.detectChanges(); }
  }

  async verDetalle(p: Pago): Promise<void> {
    if (!this.puedeVer) return;
    try {
      const apps = await this.pagosService.obtenerAplicaciones(p.id);
      this.detalle = p;
      this.aplicaciones = apps.map((a) => ({ concepto: a.conceptoNombre, monto: a.montoAplicado }));
    } catch (e: unknown) { this.error(e); }
  }

  cerrarDetalle(): void {
    this.detalle = null;
    this.aplicaciones = [];
  }

  // Imprime el recibo en una ventana nueva. El documento se construye a partir
  // del DTO autoritativo del backend (ReciboPago): el frontend solo presenta.
  async imprimirRecibo(p: Pago): Promise<void> {
    if (!this.puedeVer) return;
    try {
      const documento = await this.obtenerDocumentoRecibo(p);
      this.impresion.imprimir(documento);
    } catch (e: unknown) { this.error(e); }
  }

  // Descarga el recibo como PDF generado a partir del mismo DTO.
  async descargarReciboPdf(p: Pago): Promise<void> {
    if (!this.puedeVer || this.imprimiendoPagoId === p.id) return;
    this.imprimiendoPagoId = p.id;
    this.mensaje = '';
    this.esError = false;
    try {
      const documento = await this.obtenerDocumentoRecibo(p);
      await this.impresion.descargarPdf(documento);
    } catch (e: unknown) { this.error(e); }
    finally { this.imprimiendoPagoId = null; this.cdr.detectChanges(); }
  }

  private async obtenerDocumentoRecibo(p: Pago): Promise<ReciboPagoDocumento> {
    const recibo = await this.pagosService.obtenerRecibo(p.id);
    return new ReciboPagoDocumento(recibo);
  }

  async anular(p: Pago): Promise<void> {
    if (!this.puedeAnular || this.anulandoPagoId === p.id) return;
    const motivo = this.motivoAnulacion.trim();
    if (!motivo) {
      this.mensaje = 'El motivo de anulación es obligatorio.';
      this.esError = true;
      return;
    }
    this.anulandoPagoId = p.id;
    this.mensaje = '';
    this.esError = false;
    try {
      await this.pagosService.anularPago(p.id, motivo);
      this.motivoAnulacion = '';
      await this.cargar();
      this.mensaje = 'Pago anulado; los saldos de los cargos fueron restablecidos.';
      this.esError = false;
    } catch (e: unknown) { this.error(e); }
    finally { this.anulandoPagoId = null; this.cdr.detectChanges(); }
  }

  private error(e: unknown): void {
    this.mensaje = e instanceof PagoError || e instanceof CargoError
      ? e.message
      : 'No se pudo completar la operación.';
    this.esError = true;
  }
}
