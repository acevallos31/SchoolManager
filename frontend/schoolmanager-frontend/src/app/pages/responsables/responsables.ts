import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth';
import { ConfiguracionService } from '../../core/services/configuracion.service';
import { ResponsablesError, ResponsablesService, Responsable, ResponsableVinculo, FiltroResponsables } from '../../core/services/responsables.service';

interface ResponsableForm {
  id: string | null;
  nombres: string;
  apellidos: string;
  tipoIdentificacion: string;
  numeroIdentificacion: string;
  telefono: string;
  correo: string;
}

@Component({
  selector: 'app-responsables',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './responsables.html',
  styleUrl: './responsables.css'
})
export class Responsables implements OnInit {
  lista: Responsable[] = [];
  institucionId?: string;
  termino = '';
  estado = '';
  page = 1;
  pageSize = 20;
  totalItems = 0;
  totalPages = 0;
  cargando = false;
  guardando = false;
  mensaje = '';
  esError = false;
  mostrarFormulario = false;
  editando: Responsable | null = null;
  form: ResponsableForm = this.formVacio();

  // Vista de vínculos de un alumno (integración Alumno -> Responsable).
  alumnoIdSeleccionado: string | null = null;
  vinculos: ResponsableVinculo[] = [];
  cargandoVinculos = false;

  // Flujo vincular responsable existente al alumno.
  mostrarVincular = false;
  buscarVinculable = '';
  buscandoVinculable = false;
  vinculablesCandidatos: Responsable[] = [];
  vincularForm = { responsableId: '', parentesco: '', esPrincipal: false, accesoFinanciero: false };

  // Edición de un vínculo existente.
  editandoVinculo: ResponsableVinculo | null = null;
  editarVinculoForm = { parentesco: '', esPrincipal: false, accesoFinanciero: false };

  readonly parentescos = ['Padre', 'Madre', 'Otro'];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly auth: AuthService,
    private readonly configuracion: ConfiguracionService,
    private readonly service: ResponsablesService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  get puedeVer(): boolean { return this.auth.tienePermiso('academico.responsables.ver'); }
  get puedeCrear(): boolean { return this.auth.tienePermiso('academico.responsables.crear'); }
  get puedeEditar(): boolean { return this.auth.tienePermiso('academico.responsables.editar'); }

  async ngOnInit() {
    if (!this.puedeVer) {
      await this.router.navigate(['/dashboard']);
      return;
    }
    this.alumnoIdSeleccionado = this.route.snapshot.queryParamMap.get('alumnoId');
    if (this.alumnoIdSeleccionado) await this.cargarVinculos();
    await this.cargar();
  }

  async cargar() {
    this.cargando = true;
    this.mensaje = '';
    try {
      const contexto = await this.configuracion.obtenerContexto();
      this.institucionId = contexto.institucion?.id;
      const filtro: FiltroResponsables = {
        institucionId: this.institucionId,
        termino: this.termino.trim() || undefined,
        estado: (this.estado as 'activo' | 'inactivo') || undefined,
        page: this.page,
        pageSize: this.pageSize
      };
      const resultado = await this.service.listar(filtro).toPromise();
      if (resultado) {
        this.lista = resultado.items;
        this.totalItems = resultado.totalItems;
        this.totalPages = resultado.totalPages;
      }
    } catch (error) {
      this.error(error);
    } finally {
      this.cargando = false;
      this.cdr.detectChanges();
    }
  }

  buscar() { this.page = 1; void this.cargar(); }
  limpiarFiltros() { this.termino = ''; this.estado = ''; this.page = 1; void this.cargar(); }

  // --- Integración Alumno -> Responsable ---
  get hayVinculosVisibles(): boolean { return !!this.alumnoIdSeleccionado && this.puedeVer; }

  async cargarVinculos() {
    if (!this.alumnoIdSeleccionado) return;
    this.cargandoVinculos = true;
    this.mensaje = '';
    try {
      this.vinculos = await this.service.listarDeAlumno(this.alumnoIdSeleccionado).toPromise() ?? [];
    } catch (error) {
      this.error(error);
    } finally {
      this.cargandoVinculos = false;
      this.cdr.detectChanges();
    }
  }

  async desactivarVinculo(v: ResponsableVinculo) {
    if (!this.puedeEditar || this.guardando || v.estado !== 'activo') return;
    const motivo = window.prompt(`Motivo para desvincular a ${v.nombres} ${v.apellidos}:`)?.trim();
    if (!motivo) return;
    this.guardando = true;
    this.mensaje = '';
    try {
      await this.service.desactivarVinculo(v.id, { motivo }).toPromise();
      this.esError = false;
      await this.cargarVinculos();
      this.mensaje = 'Vínculo desactivado correctamente.';
    } catch (error) {
      this.error(error);
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  abrirVincular() {
    if (!this.puedeEditar || !this.alumnoIdSeleccionado) return;
    this.mostrarVincular = true;
    this.buscarVinculable = '';
    this.vincularForm = { responsableId: '', parentesco: '', esPrincipal: false, accesoFinanciero: false };
    this.vinculablesCandidatos = [];
    this.editandoVinculo = null;
    this.mensaje = '';
  }

  cerrarVincular() { if (!this.guardando) this.mostrarVincular = false; }

  async buscarCandidatos() {
    if (this.buscandoVinculable || !this.institucionId) return;
    this.buscandoVinculable = true;
    this.mensaje = '';
    try {
      const res = await this.service.listar({
        institucionId: this.institucionId,
        termino: this.buscarVinculable.trim() || undefined,
        estado: 'activo',
        page: 1,
        pageSize: 20
      }).toPromise();
      if (!res) return;
      const vinculados = new Set(this.vinculos.map(v => v.responsableId));
      this.vinculablesCandidatos = res.items.filter(r => !vinculados.has(r.id));
    } catch (error) {
      this.error(error);
    } finally {
      this.buscandoVinculable = false;
      this.cdr.detectChanges();
    }
  }

  async guardarVinculo() {
    if (!this.alumnoIdSeleccionado || this.guardando) return;
    if (!this.vincularForm.responsableId) return this.error(new ResponsablesError('Seleccione un responsable.', 400));
    if (!this.vincularForm.parentesco.trim()) return this.error(new ResponsablesError('Seleccione el parentesco.', 400));
    this.guardando = true;
    this.mensaje = '';
    try {
      await this.service.vincularAlumno(this.alumnoIdSeleccionado, {
        responsableId: this.vincularForm.responsableId,
        parentesco: this.vincularForm.parentesco.trim(),
        esPrincipal: this.vincularForm.esPrincipal,
        accesoFinanciero: this.vincularForm.accesoFinanciero
      }).toPromise();
      this.esError = false;
      this.mostrarVincular = false;
      await this.cargarVinculos();
      this.mensaje = 'Responsable vinculado correctamente.';
    } catch (error) {
      this.error(error);
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  abrirEditarVinculo(v: ResponsableVinculo) {
    if (this.puedeEditar && v.estado === 'activo') {
      this.mostrarVincular = false;
      this.editandoVinculo = v;
      this.editarVinculoForm = { parentesco: v.parentesco ?? '', esPrincipal: v.esPrincipal, accesoFinanciero: v.accesoFinanciero };
    }
  }

  async guardarEditarVinculo() {
    if (!this.editandoVinculo || this.guardando) return;
    this.guardando = true;
    this.mensaje = '';
    try {
      await this.service.editarVinculo(this.editandoVinculo.id, {
        parentesco: this.editarVinculoForm.parentesco.trim() || null,
        esPrincipal: this.editarVinculoForm.esPrincipal,
        accesoFinanciero: this.editarVinculoForm.accesoFinanciero
      }).toPromise();
      this.esError = false;
      this.editandoVinculo = null;
      await this.cargarVinculos();
      this.mensaje = 'Vínculo actualizado correctamente.';
    } catch (error) {
      this.error(error);
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  async reactivarVinculo(v: ResponsableVinculo) {
    if (!this.puedeEditar || this.guardando || v.estado !== 'inactivo') return;
    this.guardando = true;
    this.mensaje = '';
    try {
      await this.service.reactivarVinculo(v.id).toPromise();
      this.esError = false;
      await this.cargarVinculos();
      this.mensaje = 'Vínculo reactivado correctamente.';
    } catch (error) {
      this.error(error);
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  vinculoNombre(v: ResponsableVinculo) { return `${v.apellidos} ${v.nombres}`.trim(); }
  cerrarVinculos() { this.alumnoIdSeleccionado = null; this.vinculos = []; void this.router.navigate(['/responsables']); }
  irPagina(p: number) { if (p < 1 || p > this.totalPages || p === this.page) return; this.page = p; void this.cargar(); }
  paginas(): number[] { const total = Math.min(this.totalPages, 7); const inicio = Math.max(1, Math.min(this.page - 3, this.totalPages - total + 1)); return Array.from({ length: total }, (_, i) => inicio + i); }

  abrirNuevo() {
    this.editando = null;
    this.form = this.formVacio();
    this.mostrarFormulario = true;
  }

  abrirEditar(r: Responsable) {
    this.editando = r;
    this.form = {
      id: r.id,
      nombres: r.nombres,
      apellidos: r.apellidos,
      tipoIdentificacion: r.tipoIdentificacion ?? '',
      numeroIdentificacion: r.numeroIdentificacion ?? '',
      telefono: r.telefono ?? '',
      correo: r.correo ?? ''
    };
    this.mostrarFormulario = true;
  }

  async guardar() {
    if (this.guardando) return;
    if (!this.form.nombres.trim() || !this.form.apellidos.trim()) {
      return this.error(new ResponsablesError('Nombres y apellidos son obligatorios.', 400));
    }
    if (!this.editando && (!this.form.tipoIdentificacion || !this.form.numeroIdentificacion.trim())) {
      return this.error(new ResponsablesError('Tipo y número de identificación son obligatorios.', 400));
    }
    this.guardando = true;
    this.mensaje = '';
    try {
      let exito = '';
      if (this.editando) {
        await this.service.editar(this.editando.id, {
          nombres: this.form.nombres.trim(),
          apellidos: this.form.apellidos.trim(),
          telefono: this.form.telefono.trim() || null,
          correo: this.form.correo.trim() || null
        }).toPromise();
        exito = 'Responsable actualizado correctamente.';
      } else {
        await this.service.crear({
          institucionId: this.institucionId!,
          nombres: this.form.nombres.trim(),
          apellidos: this.form.apellidos.trim(),
          tipoIdentificacion: this.form.tipoIdentificacion,
          numeroIdentificacion: this.form.numeroIdentificacion.trim(),
          telefono: this.form.telefono.trim() || null,
          correo: this.form.correo.trim() || null
        }).toPromise();
        exito = 'Responsable creado correctamente.';
      }
      this.esError = false;
      this.mostrarFormulario = false;
      this.page = 1;
      await this.cargar();
      this.mensaje = exito;
    } catch (error) {
      this.error(error);
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  async desactivar(r: Responsable) {
    if (!r.estado || r.estado !== 'activo') return;
    const motivo = window.prompt('Motivo de desactivación (obligatorio):')?.trim();
    if (!motivo) return this.error(new ResponsablesError('El motivo de desactivación es obligatorio.', 400));
    this.guardando = true;
    this.mensaje = '';
    try {
      await this.service.cambiarEstado(r.id, { motivo }).toPromise();
      this.esError = false;
      await this.cargar();
      this.mensaje = 'Responsable desactivado correctamente.';
    } catch (error) {
      this.error(error);
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  async reactivar(r: Responsable) {
    if (r.estado !== 'inactivo') return;
    this.guardando = true;
    this.mensaje = '';
    try {
      await this.service.reactivar(r.id).toPromise();
      this.esError = false;
      await this.cargar();
      this.mensaje = 'Responsable reactivado correctamente.';
    } catch (error) {
      this.error(error);
    } finally {
      this.guardando = false;
      this.cdr.detectChanges();
    }
  }

  cancelar() { if (!this.guardando) this.mostrarFormulario = false; }
  volver() { void this.router.navigate(['/dashboard']); }
  nombreCompleto(r: Responsable) { return `${r.apellidos} ${r.nombres}`.trim(); }
  identificar(r: Responsable) { if (!r.tipoIdentificacion || !r.numeroIdentificacion) return '—'; return `${r.tipoIdentificacion} ${r.numeroIdentificacion}`.trim(); }

  private formVacio(): ResponsableForm { return { id: null, nombres: '', apellidos: '', tipoIdentificacion: '', numeroIdentificacion: '', telefono: '', correo: '' }; }
  private error(error: unknown) { this.mensaje = error instanceof ResponsablesError ? error.message : 'No se pudo completar la operación.'; this.esError = true; this.cdr.detectChanges(); return false; }
}