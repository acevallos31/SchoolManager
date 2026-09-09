import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth';
import { AlumnoListado, AlumnoService } from '../../core/services/alumno.service';
import {
  Cargo, CargoError, CargosService, ResumenFinanciero,
} from '../../core/services/cargos.service';

@Component({
  selector: 'app-cargos',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './cargos.html',
  styleUrl: './cargos.css',
})
export class Cargos implements OnInit {
  alumnoId: string | null = null;
  alumnos: AlumnoListado[] = [];
  cargos: Cargo[] = [];
  resumen: ResumenFinanciero | null = null;
  cargando = false;
  mensaje = '';
  esError = false;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly alumnoService: AlumnoService,
    private readonly service: CargosService,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  get puedeVer(): boolean {
    return this.auth.tienePermiso('academico.cargos.ver');
  }

  get estadoLabel(): Record<string, string> {
    return {
      pendiente: 'Pendiente',
      parcial: 'Parcial',
      pagado: 'Pagado',
      anulado: 'Anulado',
    };
  }

  get totalPendiente(): number {
    return this.cargos
      .filter((c) => c.estado === 'pendiente' || c.estado === 'parcial')
      .reduce((acc, c) => acc + c.saldo, 0);
  }

  get hayCargosVencidos(): boolean {
    return this.cargos.some((c) => c.estado === 'pendiente' && c.esVencido);
  }

  irAPagos(): void {
    if (this.alumnoId) {
      void this.router.navigate(['/pagos'], { queryParams: { alumnoId: this.alumnoId } });
    }
  }

  async ngOnInit(): Promise<void> {
    if (!this.puedeVer) {
      await this.router.navigate(['/dashboard']);
      return;
    }

    try {
      this.alumnos = await this.alumnoService.listar();
    } catch (e: unknown) {
      this.error(e);
    } finally {
      // Angular 22 zoneless: asignar un array después de await no programa por sí
      // solo un render. Sin esto el select existe pero queda visualmente vacío
      // hasta el siguiente evento del navegador.
      this.cdr.detectChanges();
    }

    const id = this.route.snapshot.queryParamMap.get('alumnoId');
    if (id) this.alumnoId = id;
    if (this.alumnoId) await this.cargar();
  }

  async seleccionarAlumno(): Promise<void> {
    this.cargos = [];
    this.resumen = null;
    this.mensaje = '';
    this.esError = false;

    await this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { alumnoId: this.alumnoId || null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });

    if (this.alumnoId) await this.cargar();
  }

  async cargar(): Promise<void> {
    if (!this.alumnoId) return;
    this.cargando = true;
    this.mensaje = '';
    this.esError = false;
    try {
      const [cargos, resumen] = await Promise.all([
        this.service.listarCargosAlumno(this.alumnoId),
        this.service.obtenerResumenAlumno(this.alumnoId),
      ]);
      this.cargos = cargos;
      this.resumen = resumen;
    } catch (e: unknown) { this.error(e); }
    finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  volver(): void {
    void this.router.navigate(['/alumnos']);
  }

  private error(e: unknown): void {
    this.mensaje = e instanceof CargoError ? e.message : 'No se pudo cargar la información financiera.';
    this.esError = true;
  }
}
