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
  mensajeTipo: 'success' | 'error' = 'success';

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
      this.mostrarMensaje('No se pudieron cargar los alumnos.', 'error');
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
      !this.nuevoAlumno.direccion ||
      !this.nuevoAlumno.correoAcceso
    ) {
      this.mostrarMensaje('Nombres, apellidos, nacimiento, sexo, DNI, encargados, direccion y correo de acceso son obligatorios', 'error');
      return;
    }

    this.rellenarAccesoConDni();

    this.cargando = true;

    try {
      await this.auth.apiRequest('/alumnos', {
        method: 'POST',
        body: JSON.stringify({ ...this.nuevoAlumno, estado: 'activo' })
      });

      this.mostrarMensaje('Alumno registrado correctamente', 'success');
      this.nuevoAlumno = this.crearFormularioVacio();
      this.mostrarFormulario = false;
      await this.cargarAlumnos();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'Error registrando alumno', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
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

  rellenarAccesoConDni() {
    const dni = this.nuevoAlumno.dni?.trim();
    if (!dni) {
      return;
    }

    if (!this.nuevoAlumno.usuarioAcceso) {
      this.nuevoAlumno.usuarioAcceso = dni;
    }

    if (!this.nuevoAlumno.passwordAcceso) {
      this.nuevoAlumno.passwordAcceso = dni;
    }
  }

  private crearFormularioVacio() {
    return {
      nombres: '',
      apellidos: '',
      fechaNacimiento: '',
      sexo: '',
      dni: '',
      padresEncargados: '',
      direccion: '',
      usuarioAcceso: '',
      correoAcceso: '',
      passwordAcceso: ''
    };
  }

  private mostrarMensaje(texto: string, tipo: 'success' | 'error') {
    this.mensaje = texto;
    this.mensajeTipo = tipo;
    this.cdr.detectChanges();

    setTimeout(() => {
      this.mensaje = '';
      this.cdr.detectChanges();
    }, 4200);
  }
}
