import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
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

    const [{ data: mensData }, { data: alumnosData }, { data: ciclosData }] =
      await Promise.all([
        this.auth.supabase
          .from('mensualidades')
          .select('*, alumnos(nombre), ciclos_escolares(nombre)')
          .order('mes'),
        this.auth.supabase.from('alumnos').select('id, nombre').eq('estado', 'activo').order('nombre'),
        this.auth.supabase.from('ciclos_escolares').select('*').eq('activo', true)
      ]);

    if (mensData) this.mensualidades = [...mensData];
    if (alumnosData) this.alumnos = [...alumnosData];
    if (ciclosData) this.ciclos = [...ciclosData];

    this.cargando = false;
    this.cdr.detectChanges();
  }

  get mensualidadesFiltradas() {
    return this.mensualidades.filter(m => {
      const porEstado = this.filtroEstado ? m.estado === this.filtroEstado : true;
      const porAlumno = this.filtroAlumno ? m.alumno_id === this.filtroAlumno : true;
      return porEstado && porAlumno;
    });
  }

  abrirPago(m: any) {
    this.mensualidadSeleccionada = m;
    this.pago = { monto_pagado: m.monto_final, metodo_pago: 'efectivo' };
    this.mostrarPago = true;
    this.cdr.detectChanges();
  }

  async registrarPago() {
    if (!this.pago.monto_pagado || this.pago.monto_pagado <= 0) {
      this.mensaje = '❌ El monto debe ser mayor a cero';
      return;
    }

    this.cargando = true;
    const { error } = await this.auth.supabase
      .from('pagos')
      .insert([{
        mensualidad_id: this.mensualidadSeleccionada.id,
        monto_pagado: this.pago.monto_pagado,
        metodo_pago: this.pago.metodo_pago,
        fecha_pago: new Date().toISOString().split('T')[0]
      }]);

    if (!error) {
      await this.auth.supabase
        .from('mensualidades')
        .update({ estado: 'pagada' })
        .eq('id', this.mensualidadSeleccionada.id);

      this.mensaje = '✅ Pago registrado correctamente';
      this.mostrarPago = false;
      this.mensualidadSeleccionada = null;
      await this.cargarDatos();
    } else {
      this.mensaje = '❌ Error: ' + error.message;
    }

    this.cargando = false;
    this.cdr.detectChanges();
    setTimeout(() => { this.mensaje = ''; this.cdr.detectChanges(); }, 3000);
  }

  async generarMensualidades() {
    if (!this.ciclos.length) {
      this.mensaje = '❌ No hay ciclo escolar activo';
      return;
    }

    const cicloId = this.ciclos[0].id;
    const montoBase = 1500;
    const mesActual = new Date().getMonth() + 1;

    const inserts = this.alumnos.map(a => ({
      alumno_id: a.id,
      ciclo_id: cicloId,
      mes: mesActual,
      monto_original: montoBase,
      descuento: 0,
      estado: 'pendiente',
      fecha_limite: new Date(new Date().getFullYear(), mesActual, 0).toISOString().split('T')[0]
    }));

    const { error } = await this.auth.supabase.from('mensualidades').insert(inserts);

    if (!error) {
      this.mensaje = `✅ Mensualidades de ${this.meses[mesActual]} generadas correctamente`;
      await this.cargarDatos();
    } else {
      this.mensaje = error.message.includes('unique')
        ? '❌ Ya existen mensualidades para este mes'
        : '❌ Error: ' + error.message;
    }

    this.cdr.detectChanges();
    setTimeout(() => { this.mensaje = ''; this.cdr.detectChanges(); }, 4000);
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }
}