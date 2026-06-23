import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-configuracion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './configuracion.html',
  styleUrl: './configuracion.css'
})
export class Configuracion implements OnInit {
  ciclos: any[] = [];
  cargando = false;
  mensaje = '';
  mensajeTipo: 'success' | 'error' = 'success';
  mostrarFormularioCiclo = false;
  cicloEditandoId = '';

  ciclo = this.crearCicloVacio();

  constructor(
    private router: Router,
    private auth: AuthService,
    private cdr: ChangeDetectorRef
  ) {}

  async ngOnInit() {
    await this.cargarCiclos();
  }

  async cargarCiclos() {
    this.cargando = true;
    this.cdr.detectChanges();

    try {
      const data = await this.auth.apiRequest<any[]>('/configuracion/ciclos');
      this.ciclos = Array.isArray(data) ? [...data] : [];
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudieron cargar los ciclos.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  abrirNuevoCiclo() {
    this.ciclo = this.crearCicloVacio();
    this.cicloEditandoId = '';
    this.mostrarFormularioCiclo = true;
    this.cdr.detectChanges();
  }

  editarCiclo(ciclo: any) {
    this.cicloEditandoId = ciclo.id;
    this.ciclo = {
      nombre: ciclo.nombre || '',
      fechaInicio: ciclo.fechaInicio || ciclo.fecha_inicio || '',
      fechaFin: ciclo.fechaFin || ciclo.fecha_fin || '',
      matriculaInicio: ciclo.matriculaInicio || ciclo.matricula_inicio || '',
      matriculaFin: ciclo.matriculaFin || ciclo.matricula_fin || '',
      activo: ciclo.activo !== false
    };
    this.mostrarFormularioCiclo = true;
    this.cdr.detectChanges();
  }

  async guardarCiclo() {
    if (!this.ciclo.nombre || !this.ciclo.fechaInicio || !this.ciclo.fechaFin || !this.ciclo.matriculaInicio || !this.ciclo.matriculaFin) {
      this.mostrarMensaje('Nombre, fechas del ciclo y periodo de matricula son obligatorios.', 'error');
      return;
    }

    if (this.ciclo.fechaInicio > this.ciclo.fechaFin) {
      this.mostrarMensaje('La fecha de inicio del ciclo no puede ser mayor a la fecha final.', 'error');
      return;
    }

    if (this.ciclo.matriculaInicio > this.ciclo.matriculaFin) {
      this.mostrarMensaje('La fecha de inicio de matricula no puede ser mayor a la fecha final.', 'error');
      return;
    }

    this.cargando = true;

    try {
      const url = this.cicloEditandoId
        ? `/configuracion/ciclos/${this.cicloEditandoId}`
        : '/configuracion/ciclos';

      await this.auth.apiRequest(url, {
        method: this.cicloEditandoId ? 'PUT' : 'POST',
        body: JSON.stringify(this.ciclo)
      });

      this.mostrarMensaje(this.cicloEditandoId ? 'Ciclo actualizado correctamente.' : 'Ciclo creado correctamente.', 'success');
      this.cancelarCiclo();
      await this.cargarCiclos();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo guardar el ciclo.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  async desactivarCiclo(id: string) {
    if (!confirm('Desactivar este ciclo escolar?')) {
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest(`/configuracion/ciclos/${id}`, {
        method: 'DELETE'
      });
      this.mostrarMensaje('Ciclo desactivado correctamente.', 'success');
      await this.cargarCiclos();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo desactivar el ciclo.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  async eliminarCiclo(id: string) {
    if (!confirm('Eliminar definitivamente este ciclo escolar? Esta accion fallara si tiene matriculas relacionadas.')) {
      return;
    }

    this.cargando = true;

    try {
      await this.auth.apiRequest(`/configuracion/ciclos/${id}?permanente=true`, {
        method: 'DELETE'
      });
      this.mostrarMensaje('Ciclo eliminado correctamente.', 'success');
      await this.cargarCiclos();
    } catch (error) {
      this.mostrarMensaje(error instanceof Error ? error.message : 'No se pudo eliminar el ciclo.', 'error');
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  cancelarCiclo() {
    this.ciclo = this.crearCicloVacio();
    this.cicloEditandoId = '';
    this.mostrarFormularioCiclo = false;
    this.cdr.detectChanges();
  }

  periodoEstado(ciclo: any): string {
    const inicio = ciclo.matriculaInicio || ciclo.matricula_inicio;
    const fin = ciclo.matriculaFin || ciclo.matricula_fin;
    if (!inicio || !fin) {
      return 'Sin configurar';
    }

    const hoy = new Date().toISOString().slice(0, 10);
    if (hoy < inicio) {
      return 'Pendiente';
    }

    if (hoy > fin) {
      return 'Cerrado';
    }

    return 'Abierto';
  }

  periodoClase(ciclo: any): string {
    return this.periodoEstado(ciclo).toLowerCase().split(' ')[0];
  }

  volver() {
    this.router.navigate(['/dashboard']);
  }

  private crearCicloVacio() {
    return {
      nombre: '',
      fechaInicio: '',
      fechaFin: '',
      matriculaInicio: '',
      matriculaFin: '',
      activo: true
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
