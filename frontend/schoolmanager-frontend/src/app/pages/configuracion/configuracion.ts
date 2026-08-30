import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  ConfiguracionError,
  ConfiguracionService,
  ContextoImplementacion
} from '../../core/services/configuracion.service';
import { AuthService } from '../../core/services/auth';

@Component({
  selector: 'app-configuracion',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './configuracion.html',
  styleUrl: './configuracion.css'
})
export class Configuracion implements OnInit {
  contexto: ContextoImplementacion | null = null;
  cargando = false;
  guardando = false;
  mensaje = '';
  esError = false;

  constructor(
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly configuracionService: ConfiguracionService
  ) {}

  get puedeEditar(): boolean {
    return this.auth.tienePermiso('configuracion.sistema.editar');
  }

  async ngOnInit(): Promise<void> {
    this.cargando = true;
    try {
      this.contexto = await this.configuracionService.obtenerContexto();
    } catch (error) {
      this.mostrarError(error);
    } finally {
      this.cargando = false;
    }
  }

  async cambiarModo(habilitado: boolean): Promise<void> {
    if (!this.puedeEditar || this.guardando || !this.contexto) return;
    const anterior = this.contexto.multiplesInstituciones;
    this.contexto = { ...this.contexto, multiplesInstituciones: habilitado };
    this.guardando = true;
    this.mensaje = '';
    try {
      this.contexto = await this.configuracionService.actualizarModo(habilitado);
      this.mensaje = 'Configuración actualizada correctamente.';
      this.esError = false;
    } catch (error) {
      this.contexto = { ...this.contexto, multiplesInstituciones: anterior };
      this.mostrarError(error);
    } finally {
      this.guardando = false;
    }
  }

  volver(): void {
    void this.router.navigate(['/dashboard']);
  }

  private mostrarError(error: unknown): void {
    this.mensaje = error instanceof ConfiguracionError
      ? error.message
      : 'No se pudo cargar la configuración.';
    this.esError = true;
  }
}
