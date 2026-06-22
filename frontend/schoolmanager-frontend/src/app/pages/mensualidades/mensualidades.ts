import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-mensualidades',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './mensualidades.html',
  styleUrl: './mensualidades.css'
})
export class Mensualidades implements OnInit {
  mensualidades: any[] = [];
  alumnos: any[] = [];
  ciclos: any[] = [];
  filtroEstado = '';
  filtroAlumno = '';
  cargando = false;
  mensaje = '';
  mostrarPago = false;
  mensualidadSeleccionada: any = null;
  mostrarDescuento = false;
  mensualidadDescuento: any = null;
  montoDescuento = 0;

  pago = {
    monto_pagado: 0,
    metodo_pago: 'efectivo'
  };

  meses = ['', 'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
    'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    await this.cargarDatos();
  }

  async cargarDatos() {
    this.cargando = true;
    this.cdr.detectChanges();

    try {
      const [mensData, alumnosData] = await Promise.all([
        this.auth.apiRequest<any[]>('/mensualidades'),
        this.auth.apiRequest<any[]>('/alumnos')
      ]);

      this.mensualidades = Array.isArray(mensData) ? [...mensData] : [];
      this.alumnos = Array.isArray(alumnosData) ? [...alumnosData] : [];
      this.ciclos = [];
    } catch (error) {
      console.error('Error cargando mensualidades:', error);
      this.mensaje = 'No se pudieron cargar los datos.';
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  get mensualidadesFiltradas() {
    return this.mensualidades.filter(m => {
      const porEstado = this.filtroEstado ? m.estado === this.filtroEstado : true;
      const porAlumno = this.filtroAlumno ? m.alumno_id === this.filtroAlumno : true;
      return porEstado && porAlumno;
    });
  }

  nombreAlumno(id: string) {
    const alumno = this.alumnos.find(a => a.id === id);
    return (alumno?.nombre ?? `${alumno?.nombres ?? ''} ${alumno?.apellidos ?? ''}`.trim()) || '-';
  }

  abrirPago(m: any) {
    this.mensualidadSeleccionada = m;
    this.pago = { monto_pagado: m.monto_final, metodo_pago: 'efectivo' };
    this.mostrarPago = true;
    this.cdr.detectChanges();
  }

  async registrarPago() {
    if (!this.pago.monto_pagado || this.pago.monto_pagado <= 0) {
      this.mensaje = 'El monto debe ser mayor a cero';
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest('/pagos', {
        method: 'POST',
        body: JSON.stringify({
          cargoId: this.mensualidadSeleccionada.id,
          montoPagado: this.pago.monto_pagado,
          metodoPago: this.pago.metodo_pago
        })
      });

      this.mensaje = 'Pago registrado correctamente';
      this.mostrarPago = false;
      this.mensualidadSeleccionada = null;
      await this.cargarDatos();
    } catch (error) {
      this.mensaje = error instanceof Error ? error.message : 'Error registrando pago';
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
      setTimeout(() => {
        this.mensaje = '';
        this.cdr.detectChanges();
      }, 3000);
    }
  }

  abrirDescuento(m: any) {
    this.mensualidadDescuento = m;
    this.montoDescuento = m.descuento || 0;
    this.mostrarDescuento = true;
    this.cdr.detectChanges();
  }

  async aplicarDescuento() {
    if (this.montoDescuento < 0) {
      this.mensaje = 'El descuento no puede ser negativo';
      return;
    }

    if (this.montoDescuento > this.mensualidadDescuento.monto_original) {
      this.mensaje = 'El descuento no puede superar el monto original';
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest(`/mensualidades/${this.mensualidadDescuento.id}`, {
        method: 'PUT',
        body: JSON.stringify({ descuento: this.montoDescuento })
      });

      this.mensaje = 'Descuento aplicado correctamente';
      this.mostrarDescuento = false;
      this.mensualidadDescuento = null;
      await this.cargarDatos();
    } catch (error) {
      this.mensaje = error instanceof Error ? error.message : 'Error aplicando descuento';
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
      setTimeout(() => {
        this.mensaje = '';
        this.cdr.detectChanges();
      }, 3000);
    }
  }

  async generarMensualidades() {
    this.mensaje = 'La generacion masiva debe implementarse en el backend.';
    this.cdr.detectChanges();
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }
}
