import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import jsPDF from 'jspdf';

@Component({
  selector: 'app-portal-padre',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './portal-padre.html',
  styleUrl: './portal-padre.css'
})
export class PortalPadre implements OnInit {
  hijos: any[] = [];
  mensualidadesPorHijo: { [key: string]: any[] } = {};
  pagosPorHijo: { [key: string]: any[] } = {};
  cargando = true;
  nombrePadre = '';
  hijoSeleccionadoId: string = '';

  vistaActual: 'resumen' | 'pendientes' | 'historial' = 'resumen';

  mostrarPago = false;
  mensualidadAPagar: any = null;
  procesandoPago = false;
  mensaje = '';

  tarjeta = {
    numero: '',
    nombre: '',
    vencimiento: '',
    cvv: ''
  };

  meses = ['', 'Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio',
           'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

  constructor(
    private auth: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    await this.cargarDatos();
  }

  async cargarDatos() {
    this.cargando = true;
    this.cdr.detectChanges();

    const usuario = await this.auth.getUsuarioActual();
    if (!usuario) {
      this.router.navigate(['/login']);
      return;
    }
    this.nombrePadre = usuario.nombre;

    const { data: hijosData } = await this.auth.supabase
      .from('alumnos')
      .select('*')
      .eq('tutor_id', usuario.id);

    this.hijos = hijosData || [];
    if (this.hijos.length > 0 && !this.hijoSeleccionadoId) {
      this.hijoSeleccionadoId = this.hijos[0].id;
    }

    for (const hijo of this.hijos) {
      const { data: mensData } = await this.auth.supabase
        .from('mensualidades')
        .select('*')
        .eq('alumno_id', hijo.id)
        .order('mes');
      this.mensualidadesPorHijo[hijo.id] = mensData || [];

      const mensIds = (mensData || []).map(m => m.id);
      if (mensIds.length > 0) {
        const { data: pagosData } = await this.auth.supabase
          .from('pagos')
          .select('*')
          .in('mensualidad_id', mensIds)
          .order('fecha_pago', { ascending: false });
        this.pagosPorHijo[hijo.id] = pagosData || [];
      } else {
        this.pagosPorHijo[hijo.id] = [];
      }
    }

    this.cargando = false;
    this.cdr.detectChanges();
  }

  get hijoSeleccionado() {
    return this.hijos.find(h => h.id === this.hijoSeleccionadoId);
  }

  get mensualidadesHijoActual() {
    return this.mensualidadesPorHijo[this.hijoSeleccionadoId] || [];
  }

  get pendientesHijoActual() {
    return this.mensualidadesHijoActual.filter(m => m.estado !== 'pagada');
  }

  get pagosHijoActual() {
    return this.pagosPorHijo[this.hijoSeleccionadoId] || [];
  }

  get totalPendiente(): number {
    return this.pendientesHijoActual.reduce((sum, m) => sum + Number(m.monto_final), 0);
  }

  get totalPagado(): number {
    return this.mensualidadesHijoActual
      .filter(m => m.estado === 'pagada')
      .reduce((sum, m) => sum + Number(m.monto_final), 0);
  }

  get cantidadVencidas(): number {
    return this.mensualidadesHijoActual.filter(m => m.estado === 'vencida').length;
  }

  seleccionarHijo(id: string) {
    this.hijoSeleccionadoId = id;
    this.cdr.detectChanges();
  }

  cambiarVista(vista: 'resumen' | 'pendientes' | 'historial') {
    this.vistaActual = vista;
    this.cdr.detectChanges();
  }

  diasDeMora(fechaLimite: string): number {
    const hoy = new Date();
    const limite = new Date(fechaLimite);
    const diff = Math.floor((hoy.getTime() - limite.getTime()) / (1000 * 60 * 60 * 24));
    return diff > 0 ? diff : 0;
  }

  abrirPago(m: any) {
    this.mensualidadAPagar = m;
    this.tarjeta = { numero: '', nombre: '', vencimiento: '', cvv: '' };
    this.mostrarPago = true;
    this.cdr.detectChanges();
  }

  cerrarPago() {
    this.mostrarPago = false;
    this.mensualidadAPagar = null;
    this.cdr.detectChanges();
  }

  async confirmarPago() {
    if (!this.tarjeta.numero || !this.tarjeta.nombre || !this.tarjeta.vencimiento || !this.tarjeta.cvv) {
      this.mensaje = '❌ Completá todos los datos de la tarjeta';
      return;
    }
    if (this.tarjeta.numero.replace(/\s/g, '').length < 13) {
      this.mensaje = '❌ Número de tarjeta inválido';
      return;
    }

    this.procesandoPago = true;
    this.cdr.detectChanges();

    // Simulación de procesamiento
    await new Promise(resolve => setTimeout(resolve, 1500));

    const { error } = await this.auth.supabase
      .from('pagos')
      .insert([{
        mensualidad_id: this.mensualidadAPagar.id,
        monto_pagado: this.mensualidadAPagar.monto_final,
        metodo_pago: 'tarjeta',
        fecha_pago: new Date().toISOString().split('T')[0]
      }]);

    if (!error) {
      await this.auth.supabase
        .from('mensualidades')
        .update({ estado: 'pagada' })
        .eq('id', this.mensualidadAPagar.id);

      this.mensaje = '✅ Pago realizado correctamente';
      this.mostrarPago = false;
      this.mensualidadAPagar = null;
      await this.cargarDatos();
    } else {
      this.mensaje = '❌ Error al procesar el pago: ' + error.message;
    }

    this.procesandoPago = false;
    this.cdr.detectChanges();
    setTimeout(() => { this.mensaje = ''; this.cdr.detectChanges(); }, 4000);
  }

  formatearNumeroTarjeta() {
    let valor = this.tarjeta.numero.replace(/\D/g, '').slice(0, 16);
    this.tarjeta.numero = valor.replace(/(\d{4})(?=\d)/g, '$1 ');
  }

  formatearVencimiento() {
    let valor = this.tarjeta.vencimiento.replace(/\D/g, '').slice(0, 4);
    if (valor.length >= 3) {
      this.tarjeta.vencimiento = valor.slice(0, 2) + '/' + valor.slice(2);
    } else {
      this.tarjeta.vencimiento = valor;
    }
  }

  descargarFactura(m: any, pago?: any) {
    const doc = new jsPDF();
    const hijo = this.hijoSeleccionado;

    doc.setFillColor(26, 115, 232);
    doc.rect(0, 0, 210, 35, 'F');
    doc.setTextColor(255, 255, 255);
    doc.setFontSize(20);
    doc.text('SchoolManager', 14, 18);
    doc.setFontSize(11);
    doc.text('Comprobante de Pago', 14, 27);

    doc.setTextColor(0, 0, 0);
    doc.setFontSize(11);
    let y = 50;

    doc.setFont('helvetica', 'bold');
    doc.text('Datos del alumno', 14, y);
    doc.setFont('helvetica', 'normal');
    y += 8;
    doc.text(`Nombre: ${hijo?.nombre || ''}`, 14, y);
    y += 7;
    doc.text(`Grado: ${hijo?.grado || ''} - Sección ${hijo?.seccion || ''}`, 14, y);
    y += 7;
    doc.text(`Identidad: ${hijo?.identidad || ''}`, 14, y);

    y += 15;
    doc.setFont('helvetica', 'bold');
    doc.text('Detalle del pago', 14, y);
    doc.setFont('helvetica', 'normal');
    y += 8;
    doc.text(`Mes: ${this.meses[m.mes]}`, 14, y);
    y += 7;
    doc.text(`Monto: L. ${Number(m.monto_final).toFixed(2)}`, 14, y);
    y += 7;
    doc.text(`Estado: ${m.estado === 'pagada' ? 'PAGADA' : m.estado.toUpperCase()}`, 14, y);

    if (pago) {
      y += 7;
      doc.text(`Fecha de pago: ${pago.fecha_pago}`, 14, y);
      y += 7;
      doc.text(`Método de pago: ${pago.metodo_pago}`, 14, y);
    }

    y += 20;
    doc.setFontSize(9);
    doc.setTextColor(120, 120, 120);
    doc.text('Este es un comprobante generado automáticamente por SchoolManager.', 14, y);
    doc.text(`Generado el: ${new Date().toLocaleDateString('es-HN')}`, 14, y + 6);

    doc.save(`Comprobante_${hijo?.nombre?.replace(/\s/g, '_')}_${this.meses[m.mes]}.pdf`);
  }

  async logout() {
    await this.auth.logout();
    this.router.navigate(['/login']);
  }
}