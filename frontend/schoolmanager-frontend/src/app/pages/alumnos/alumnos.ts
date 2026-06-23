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
  usuariosAcceso: any[] = [];
  mostrarFormulario = false;
  busqueda = '';
  cargando = false;
  mensaje = '';
  mensajeTipo: 'success' | 'error' = 'success';
  alumnoEditandoId = '';

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
      const [data, usuarios] = await Promise.all([
        this.auth.apiRequest<any[]>('/alumnos'),
        this.auth.apiRequest<any[]>('/usuarios?rol=usuario&incluirInactivos=false')
      ]);
      this.alumnos = Array.isArray(data) ? [...data] : [];
      this.usuariosAcceso = Array.isArray(usuarios) ? [...usuarios] : [];
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
      !this.nuevoAlumno.direccion
    ) {
      this.mostrarMensaje('Nombres, apellidos, nacimiento, sexo, DNI, encargados y direccion son obligatorios', 'error');
      return;
    }

    if (!this.alumnoEditandoId && !this.nuevoAlumno.tutorId && !this.nuevoAlumno.correoAcceso) {
      this.mostrarMensaje('Selecciona un usuario existente o registra un correo de acceso', 'error');
      return;
    }

    if (!this.alumnoEditandoId && !this.nuevoAlumno.tutorId) {
      this.rellenarAccesoConDni();
      this.rellenarNombreUsuarioAcceso();
    }

    this.cargando = true;

    try {
      const url = this.alumnoEditandoId ? `/alumnos/${this.alumnoEditandoId}` : '/alumnos';
      await this.auth.apiRequest(url, {
        method: this.alumnoEditandoId ? 'PUT' : 'POST',
        body: JSON.stringify({ ...this.nuevoAlumno, estado: 'activo' })
      });

      this.mostrarMensaje(this.alumnoEditandoId ? 'Alumno actualizado correctamente' : 'Alumno registrado correctamente', 'success');
      this.cancelarEdicion();
      await this.cargarAlumnos();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'Error guardando alumno', 'error');
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

  async eliminarAlumno(id: string) {
    if (!confirm('Eliminar definitivamente este alumno? Esta accion puede fallar si tiene matriculas o pagos relacionados.')) {
      return;
    }

    try {
      await this.auth.apiRequest(`/alumnos/${id}?permanente=true`, {
        method: 'DELETE'
      });
      this.mostrarMensaje('Alumno eliminado correctamente', 'success');
      await this.cargarAlumnos();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo eliminar el alumno', 'error');
    }
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

  editarAlumno(alumno: any) {
    this.alumnoEditandoId = alumno.id;
    this.nuevoAlumno = {
      nombres: alumno.nombres || '',
      apellidos: alumno.apellidos || '',
      fechaNacimiento: alumno.fechaNacimiento || alumno.fecha_nacimiento || '',
      sexo: alumno.sexo || '',
      dni: alumno.dni || alumno.identidad || '',
      padresEncargados: alumno.padresEncargados || alumno.padres_encargados || '',
      direccion: alumno.direccion || '',
      usuarioAcceso: alumno.usuarioAcceso || alumno.usuario_acceso || '',
      nombreUsuarioAcceso: '',
      correoAcceso: alumno.correoAcceso || alumno.correo_acceso || '',
      passwordAcceso: '',
      tutorId: alumno.tutorId || alumno.tutor_id || ''
    };
    this.mostrarFormulario = true;
    this.cdr.detectChanges();
  }

  nombreUsuarioAlumno(alumno: any): string {
    const tutorId = alumno.tutorId || alumno.tutor_id;
    const usuario = this.usuariosAcceso.find(item => item.id === tutorId);
    return usuario?.usuario
      ? `${usuario.nombreCompleto || usuario.nombre_completo || usuario.nombre} - ${usuario.usuario}`
      : alumno.usuarioAcceso || alumno.usuario_acceso || '-';
  }

  abrirNuevo() {
    this.nuevoAlumno = this.crearFormularioVacio();
    this.alumnoEditandoId = '';
    this.mostrarFormulario = true;
    this.cdr.detectChanges();
  }

  cancelarEdicion() {
    this.nuevoAlumno = this.crearFormularioVacio();
    this.alumnoEditandoId = '';
    this.mostrarFormulario = false;
    this.cdr.detectChanges();
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

  rellenarNombreUsuarioAcceso() {
    if (this.nuevoAlumno.nombreUsuarioAcceso || !this.nuevoAlumno.nombres) {
      return;
    }

    const primerNombre = this.nuevoAlumno.nombres.trim().split(/\s+/)[0];
    this.nuevoAlumno.nombreUsuarioAcceso = primerNombre ? `Padre de ${primerNombre}` : '';
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
      nombreUsuarioAcceso: '',
      correoAcceso: '',
      passwordAcceso: '',
      tutorId: ''
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
