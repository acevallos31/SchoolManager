import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
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

  nuevoAlumno = this.crearFormularioVacio();

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

    try {
      const data = await this.auth.apiRequest<any[]>('/alumnos');
      this.alumnos = Array.isArray(data) ? [...data] : [];
    } catch (error) {
      console.error('Error cargando alumnos:', error);
      this.mensaje = 'No se pudieron cargar los alumnos.';
      this.alumnos = [];
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  get alumnosFiltrados() {
    if (!this.busqueda) {
      return this.alumnos;
    }

    const busqueda = this.busqueda.toLowerCase();
    return this.alumnos.filter(a =>
      String(a.nombre ?? '').toLowerCase().includes(busqueda) ||
      String(a.identidad ?? '').toLowerCase().includes(busqueda) ||
      String(a.dni ?? '').toLowerCase().includes(busqueda)
    );
  }

  async guardarAlumno() {
    if (
      !this.nuevoAlumno.nombres ||
      !this.nuevoAlumno.apellidos ||
      !this.nuevoAlumno.fechaNacimiento ||
      !this.nuevoAlumno.sexo ||
      !this.nuevoAlumno.dni ||
      !this.nuevoAlumno.padresEncargados ||
      !this.nuevoAlumno.direccion
    ) {
      this.mensaje = 'Nombres, apellidos, nacimiento, sexo, DNI, encargados y direccion son obligatorios';
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest('/alumnos', {
        method: 'POST',
        body: JSON.stringify({ ...this.nuevoAlumno, estado: 'activo' })
      });

      this.mensaje = 'Alumno registrado correctamente';
      this.nuevoAlumno = this.crearFormularioVacio();
      this.mostrarFormulario = false;
      await this.cargarAlumnos();
    } catch (error) {
      this.mensaje = error instanceof Error ? error.message : 'Error registrando alumno';
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
      setTimeout(() => {
        this.mensaje = '';
        this.cdr.detectChanges();
      }, 3000);
    }
  }

  async desactivarAlumno(id: string) {
    if (!confirm('Desactivar este alumno?')) {
      return;
    }

    await this.auth.apiRequest(`/alumnos/${id}`, {
      method: 'DELETE'
    });
    await this.cargarAlumnos();
  }

  async activarAlumno(id: string) {
    if (!confirm('Activar este alumno?')) {
      return;
    }

    await this.auth.apiRequest(`/alumnos/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ estado: 'activo' })
    });
    await this.cargarAlumnos();
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }

  private crearFormularioVacio() {
    return {
      nombres: '',
      apellidos: '',
      fechaNacimiento: '',
      sexo: '',
      dni: '',
      padresEncargados: '',
      direccion: ''
    };
  }
}
