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
  grados: any[] = [];
  secciones: any[] = [];
  planesPago: any[] = [];
  mostrarFormulario = false;
  cargando = false;
  mensaje = '';

  nuevaMatricula = this.crearFormularioVacio();

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
      const [matriculasData, alumnosData, ciclosData, gradosData, seccionesData, planesPagoData] = await Promise.all([
        this.auth.apiRequest<any[]>('/matriculas'),
        this.auth.apiRequest<any[]>('/alumnos'),
        this.auth.apiRequest<any[]>('/catalogos/ciclos'),
        this.auth.apiRequest<any[]>('/catalogos/grados'),
        this.auth.apiRequest<any[]>('/catalogos/secciones'),
        this.auth.apiRequest<any[]>('/planes-pago')
      ]);

      this.matriculas = Array.isArray(matriculasData) ? [...matriculasData] : [];
      this.alumnos = Array.isArray(alumnosData) ? [...alumnosData] : [];
      this.ciclos = Array.isArray(ciclosData) ? [...ciclosData] : [];
      this.grados = Array.isArray(gradosData) ? [...gradosData] : [];
      this.secciones = Array.isArray(seccionesData) ? [...seccionesData] : [];
      this.planesPago = Array.isArray(planesPagoData) ? [...planesPagoData] : [];
    } catch (error) {
      console.error('Error cargando matriculas:', error);
      this.mensaje = 'No se pudieron cargar los datos.';
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  async guardarMatricula() {
    if (
      !this.nuevaMatricula.alumnoId ||
      !this.nuevaMatricula.cicloId ||
      !this.nuevaMatricula.gradoId ||
      !this.nuevaMatricula.seccionId ||
      !this.nuevaMatricula.planPagoId
    ) {
      this.mensaje = 'Alumno, ciclo, grado, seccion y plan de pago son obligatorios';
      return;
    }

    if (this.nuevaMatricula.monto < 0) {
      this.mensaje = 'El monto no puede ser negativo';
      return;
    }

    if (this.existeMatriculaAlumnoCiclo(this.nuevaMatricula.alumnoId, this.nuevaMatricula.cicloId)) {
      this.mensaje = 'Este alumno ya tiene una matricula registrada para el ciclo seleccionado.';
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest('/matriculas', {
        method: 'POST',
        body: JSON.stringify({ ...this.nuevaMatricula })
      });

      this.mensaje = 'Matricula registrada correctamente. Las facturas del plan fueron generadas en el estado de cuenta.';
      this.nuevaMatricula = this.crearFormularioVacio();
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

  nombreAlumno(id: string) {
    return this.alumnos.find(a => a.id === id)?.nombre ?? '-';
  }

  nombreCatalogo(items: any[], id: string) {
    return items.find(item => item.id === id)?.nombre ?? '-';
  }

  get seccionesDisponibles() {
    if (!this.nuevaMatricula.gradoId) {
      return this.secciones;
    }

    return this.secciones.filter(item => (item.gradoId || item.grado_id) === this.nuevaMatricula.gradoId);
  }

  cambiarGrado() {
    this.nuevaMatricula.seccionId = '';
  }

  seleccionarPlan() {
    const plan = this.planesPago.find(item => item.id === this.nuevaMatricula.planPagoId);
    this.nuevaMatricula.monto = plan?.montoMatricula ?? plan?.monto_matricula ?? 0;
  }

  private existeMatriculaAlumnoCiclo(alumnoId: string, cicloId: string): boolean {
    return this.matriculas.some(item =>
      (item.alumnoId || item.alumno_id) === alumnoId &&
      (item.cicloId || item.ciclo_id) === cicloId
    );
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }

  private crearFormularioVacio() {
    return {
      alumnoId: '',
      cicloId: '',
      gradoId: '',
      seccionId: '',
      planPagoId: '',
      monto: 0,
      estado: 'pendiente'
    };
  }
}
