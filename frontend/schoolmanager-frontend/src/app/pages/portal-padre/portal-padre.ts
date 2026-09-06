import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import {
  AplicacionPago, Cargo, MisAlumno, Pago, PortalResponsableService,
  ResumenFinanciero,
} from '../../core/services/portal-responsable.service';

// Bloque 022: portal del responsable financiero (padre). SOLO LECTURA.
// No hay botones de pago, tarjeta ni ninguna escritura: solo consume los
// endpoints dedicados de PortalResponsableController contra la API .NET.
@Component({
  selector: 'app-portal-padre',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './portal-padre.html',
  styleUrl: './portal-padre.css'
})
export class PortalPadre implements OnInit {
  hijos: MisAlumno[] = [];
  hijoSeleccionadoId = '';
  cargandoInicial = true;
  errorInicial: string | null = null;

  tab: 'resumen' | 'cargos' | 'pagos' = 'resumen';

  resumen: ResumenFinanciero | null = null;
  cargos: Cargo[] = [];
  pagos: Pago[] = [];
  aplicacionesPorPago: Record<string, AplicacionPago[]> = {};
  pagoAbiertoId: string | null = null;

  cargandoTab = false;
  errorTab: string | null = null;

  constructor(
    private auth: AuthService,
    private portal: PortalResponsableService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit(): Promise<void> {
    await this.cargarHijos();
  }

  get hayErrorInicial(): boolean {
    return !!this.errorInicial;
  }

  get hijoSeleccionado(): MisAlumno | undefined {
    return this.hijos.find((h) => h.id === this.hijoSeleccionadoId);
  }

  async cargarHijos(): Promise<void> {
    this.cargandoInicial = true;
    this.errorInicial = null;
    try {
      const usuario = await this.auth.getUsuarioActual();
      if (!usuario) {
        this.router.navigate(['/login']);
        return;
      }
      this.hijos = await this.portal.misAlumnos();
      if (this.hijos.length > 0 && !this.hijoSeleccionadoId) {
        await this.seleccionarHijo(this.hijos[0].id);
      }
    } catch (e: unknown) {
      this.errorInicial = this.mensajeDe(e);
    } finally {
      this.cargandoInicial = false;
      this.cdr.detectChanges();
    }
  }

  async seleccionarHijo(id: string): Promise<void> {
    this.hijoSeleccionadoId = id;
    this.resumen = null;
    this.cargos = [];
    this.pagos = [];
    this.aplicacionesPorPago = {};
    this.pagoAbiertoId = null;
    this.errorTab = null;
    await this.cargarAlumno();
  }

  async cargarAlumno(): Promise<void> {
    if (!this.hijoSeleccionadoId) return;
    this.cargandoTab = true;
    this.errorTab = null;
    try {
      const [resumen, cargos, pagos] = await Promise.all([
        this.portal.resumenAlumno(this.hijoSeleccionadoId),
        this.portal.cargosAlumno(this.hijoSeleccionadoId),
        this.portal.pagosAlumno(this.hijoSeleccionadoId),
      ]);
      this.resumen = resumen;
      this.cargos = cargos;
      this.pagos = pagos;
    } catch (e: unknown) {
      this.errorTab = this.mensajeDe(e);
    } finally {
      this.cargandoTab = false;
      this.cdr.detectChanges();
    }
  }

  cambiarTab(tab: 'resumen' | 'cargos' | 'pagos'): void {
    this.tab = tab;
    this.cdr.detectChanges();
  }

  async alternarAplicaciones(pago: Pago): Promise<void> {
    if (this.pagoAbiertoId === pago.id) {
      this.pagoAbiertoId = null;
      this.cdr.detectChanges();
      return;
    }
    this.pagoAbiertoId = pago.id;
    if (!this.aplicacionesPorPago[pago.id]) {
      try {
        this.aplicacionesPorPago[pago.id] = await this.portal.aplicacionesPago(pago.id);
      } catch (e: unknown) {
        this.aplicacionesPorPago[pago.id] = [];
      }
    }
    this.cdr.detectChanges();
  }

  async logout(): Promise<void> {
    await this.auth.logout();
    this.router.navigate(['/login']);
  }

  // Formato monetario HNL (sin pipe de moneda, consistente con el resto).
  fmt(x: number | null | undefined): string {
    return `L. ${(x ?? 0).toFixed(2)}`;
  }

  nombreCompleto(hijo: MisAlumno): string {
    return `${hijo.nombres ?? ''} ${hijo.apellidos ?? ''}`.trim() || 'Sin nombre';
  }

  private mensajeDe(e: unknown): string {
    if (e instanceof Error) return e.message;
    return 'No se pudo cargar la información.';
  }
}
