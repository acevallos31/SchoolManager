import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-matriculas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './matriculas.html',
  styleUrl: './matriculas.css'
})
export class Matriculas implements OnInit {
  matriculas: any[] = [];
  alumnos: any[] = [];
  ciclos: any[] = [];
  mostrarFormulario = false;
  cargando = false;
  mensaje = '';

  nuevaMatricula = {
    alumno_id: '',
    ciclo_id: '',
    monto: 0,
    estado: 'pendiente'
  };

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

    const [{ data: matriculasData }, { data: alumnosData }, { data: ciclosData }] =
      await Promise.all([
        this.auth.supabase.from('matriculas').select('*, alumnos(nombre), ciclos_escolares(nombre)').order('created_at', { ascending: false }),
        this.auth.supabase.from('alumnos').select('id, nombre').eq('estado', 'activo').order('nombre'),
        this.auth.supabase.from('ciclos_escolares').select('*').eq('activo', true)
      ]);

    if (matriculasData) this.matriculas = [...matriculasData];
    if (alumnosData) this.alumnos = [...alumnosData];
    if (ciclosData) this.ciclos = [...ciclosData];

    this.cargando = false;
    this.cdr.detectChanges();
  }

  async guardarMatricula() {
    if (!this.nuevaMatricula.alumno_id || !this.nuevaMatricula.ciclo_id || !this.nuevaMatricula.monto) {
      this.mensaje = '❌ Todos los campos son obligatorios';
      return;
    }
    if (this.nuevaMatricula.monto <= 0) {
      this.mensaje = '❌ El monto debe ser mayor a cero';
      return;
    }

    this.cargando = true;
    const { error } = await this.auth.supabase
      .from('matriculas')
      .insert([{ ...this.nuevaMatricula }]);

    if (error) {
      this.mensaje = error.message.includes('unique')
        ? '❌ Este alumno ya tiene matrícula en este ciclo'
        : '❌ Error: ' + error.message;
    } else {
      this.mensaje = '✅ Matrícula registrada correctamente';
      this.nuevaMatricula = { alumno_id: '', ciclo_id: '', monto: 0, estado: 'pendiente' };
      this.mostrarFormulario = false;
      await this.cargarDatos();
    }

    this.cargando = false;
    this.cdr.detectChanges();
    setTimeout(() => { this.mensaje = ''; this.cdr.detectChanges(); }, 3000);
  }

  async marcarPagada(id: string) {
    await this.auth.supabase.from('matriculas').update({ estado: 'pagada' }).eq('id', id);
    await this.cargarDatos();
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }
}