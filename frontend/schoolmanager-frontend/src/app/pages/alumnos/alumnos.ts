import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-alumnos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './alumnos.html',
  styleUrl: './alumnos.css'
})
export class Alumnos implements OnInit {
  alumnos: any[] = [];
  mostrarFormulario = false;
  busqueda = '';
  cargando = false;
  mensaje = '';

  nuevoAlumno = {
    nombre: '',
    identidad: '',
    grado: '',
    seccion: '',
    fecha_nacimiento: ''
  };

  grados = ['1er Grado', '2do Grado', '3er Grado', '4to Grado',
            '5to Grado', '6to Grado', '7mo Grado', '8vo Grado',
            '9no Grado'];

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    await this.cargarAlumnos();
  }

  async cargarAlumnos() {
    this.cargando = true;
    this.cdr.detectChanges();
    const { data, error } = await this.auth.supabase
      .from('alumnos')
      .select('*')
      .order('nombre');
    if (!error && data) this.alumnos = [...data];
    this.cargando = false;
    this.cdr.detectChanges();
  }

  get alumnosFiltrados() {
    if (!this.busqueda) return this.alumnos;
    const b = this.busqueda.toLowerCase();
    return this.alumnos.filter(a =>
      a.nombre.toLowerCase().includes(b) ||
      a.identidad.toLowerCase().includes(b) ||
      a.grado.toLowerCase().includes(b)
    );
  }

  async guardarAlumno() {
    if (!this.nuevoAlumno.nombre || !this.nuevoAlumno.identidad || !this.nuevoAlumno.grado) {
      this.mensaje = '❌ Nombre, identidad y grado son obligatorios';
      return;
    }
    this.cargando = true;
    const { error } = await this.auth.supabase
      .from('alumnos')
      .insert([{ ...this.nuevoAlumno, estado: 'activo' }]);
    if (error) {
      this.mensaje = error.message.includes('unique')
        ? '❌ Ya existe un alumno con esa identidad'
        : '❌ Error: ' + error.message;
    } else {
      this.mensaje = '✅ Alumno registrado correctamente';
      this.nuevoAlumno = { nombre: '', identidad: '', grado: '', seccion: '', fecha_nacimiento: '' };
      this.mostrarFormulario = false;
      await this.cargarAlumnos();
    }
    this.cargando = false;
    this.cdr.detectChanges();
    setTimeout(() => { this.mensaje = ''; this.cdr.detectChanges(); }, 3000);
  }

  async desactivarAlumno(id: string) {
    if (!confirm('¿Desactivar este alumno?')) return;
    await this.auth.supabase.from('alumnos').update({ estado: 'inactivo' }).eq('id', id);
    await this.cargarAlumnos();
  }

  async activarAlumno(id: string) {
    if (!confirm('¿Activar este alumno?')) return;
    await this.auth.supabase.from('alumnos').update({ estado: 'activo' }).eq('id', id);
    await this.cargarAlumnos();
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }
}