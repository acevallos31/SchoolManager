import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
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

    try {
      const [matriculasData, alumnosData] = await Promise.all([
        this.auth.apiRequest<any[]>('/matriculas'),
        this.auth.apiRequest<any[]>('/alumnos')
      ]);

      this.matriculas = Array.isArray(matriculasData) ? [...matriculasData] : [];
      this.alumnos = Array.isArray(alumnosData) ? [...alumnosData] : [];
      this.ciclos = [];
    } catch (error) {
      console.error('Error cargando matriculas:', error);
      this.mensaje = 'No se pudieron cargar los datos.';
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  async guardarMatricula() {
    if (!this.nuevaMatricula.alumno_id || !this.nuevaMatricula.ciclo_id || !this.nuevaMatricula.monto) {
      this.mensaje = 'Todos los campos son obligatorios';
      return;
    }

    if (this.nuevaMatricula.monto <= 0) {
      this.mensaje = 'El monto debe ser mayor a cero';
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest('/matriculas', {
        method: 'POST',
        body: JSON.stringify({ ...this.nuevaMatricula })
      });

      this.mensaje = 'Matricula registrada correctamente';
      this.nuevaMatricula = { alumno_id: '', ciclo_id: '', monto: 0, estado: 'pendiente' };
      this.mostrarFormulario = false;
      await this.cargarDatos();
    } catch (error) {
      this.mensaje = error instanceof Error ? error.message : 'Error registrando matricula';
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
      setTimeout(() => {
        this.mensaje = '';
        this.cdr.detectChanges();
      }, 3000);
    }
  }

  async marcarPagada(id: string) {
    await this.auth.apiRequest(`/matriculas/${id}`, {
      method: 'PUT',
      body: JSON.stringify({ estado: 'pagada' })
    });
    await this.cargarDatos();
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }
}
