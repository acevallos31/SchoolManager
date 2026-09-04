import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Responsables } from './responsables';
import { AuthService } from '../../core/services/auth';
import { ConfiguracionService } from '../../core/services/configuracion.service';
import { ResponsablesService, Responsable } from '../../core/services/responsables.service';

describe('Responsables (018D)', () => {
  const institucionId = '11111111-1111-1111-1111-111111111111';
  const ejemplo: Responsable = {
    id: 'r1',
    personaId: 'p1',
    institucionId,
    estado: 'activo',
    nombres: 'Ana',
    apellidos: 'Mendoza',
    tipoIdentificacion: 'CI',
    numeroIdentificacion: '9990001',
    telefono: '0999111222',
    correo: 'ana@test.com',
    createdAt: new Date().toISOString(),
    fechaDesactivacion: null,
    motivoDesactivacion: null
  };

  let f: ComponentFixture<Responsables>;
  let c: Responsables;
  let s: Record<string, ReturnType<typeof vi.fn>>;
  let permisos: Set<string>;
  let router: { navigate: ReturnType<typeof vi.fn> };
  let contexto: { institucion: { id: string } };

  beforeEach(async () => {
    permisos = new Set(['academico.responsables.ver', 'academico.responsables.crear', 'academico.responsables.editar']);
    contexto = { institucion: { id: institucionId } };
    router = { navigate: vi.fn().mockResolvedValue(true) };
    s = {
      listar: vi.fn().mockReturnValue(of({
        items: [ejemplo], page: 1, pageSize: 20, totalItems: 1, totalPages: 1
      })),
      crear: vi.fn().mockReturnValue(of({ id: 'r1' })),
      editar: vi.fn().mockReturnValue(of(void 0)),
      cambiarEstado: vi.fn().mockReturnValue(of(void 0)),
      reactivar: vi.fn().mockReturnValue(of(void 0)),
      listarDeAlumno: vi.fn().mockReturnValue(of([])),
      desactivarVinculo: vi.fn().mockReturnValue(of(void 0)),
      vincularAlumno: vi.fn().mockReturnValue(of({ id: 'v2' })),
      editarVinculo: vi.fn().mockReturnValue(of(void 0)),
      reactivarVinculo: vi.fn().mockReturnValue(of(void 0))
    };

    await TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Responsables],
      providers: [
        { provide: Router, useValue: router },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
        { provide: AuthService, useValue: { tienePermiso: (x: string) => permisos.has(x) } },
        { provide: ConfiguracionService, useValue: { obtenerContexto: vi.fn().mockResolvedValue(contexto) } },
        { provide: ResponsablesService, useValue: s }
      ]
    });
    await TestBed.compileComponents();
    f = TestBed.createComponent(Responsables);
    c = f.componentInstance;
    f.detectChanges();
    await vi.waitFor(() => expect(c.cargando).toBe(false));
  });

  it('crea el componente', () => {
    expect(c).toBeTruthy();
  });

  it('redirige al dashboard si no tiene permiso de ver', async () => {
    s['listar'].mockClear();
    router.navigate.mockClear();
    permisos.clear();
    await c.ngOnInit();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
    expect(s['listar']).not.toHaveBeenCalled();
  });

  it('carga la lista usando la institución del contexto', async () => {
    const filtro = s['listar'].mock.calls[0][0];
    expect(filtro.institucionId).toBe(institucionId);
    expect(c.lista).toHaveLength(1);
    expect(c.totalItems).toBe(1);
  });

  it('aplica el término y estado al filtrar', async () => {
    c.termino = 'Mendoza';
    c.estado = 'activo';
    await c.cargar();
    const llamada = s['listar'].mock.calls.at(-1);
    expect(llamada && llamada[0].termino).toBe('Mendoza');
    expect(llamada && llamada[0].estado).toBe('activo');
  });

  it('crea un responsable y recarga la lista', async () => {
    c.institucionId = institucionId;
    c.form = { id: null, nombres: 'Luis', apellidos: 'Ramos', tipoIdentificacion: 'CI', numeroIdentificacion: '1717', telefono: '', correo: '' };
    c.mostrarFormulario = true;
    await c.guardar();
    expect(s['crear']).toHaveBeenCalledWith(expect.objectContaining({ nombres: 'Luis', institucionId }));
    expect(c.mensaje).toContain('creado');
    expect(c.mostrarFormulario).toBe(false);
  });

  it('rechaza el formulario sin apellidos', async () => {
    c.form = { id: null, nombres: 'Luis', apellidos: '', tipoIdentificacion: 'CI', numeroIdentificacion: '1717', telefono: '', correo: '' };
    await c.guardar();
    expect(s['crear']).not.toHaveBeenCalled();
    expect(c.esError).toBe(true);
  });

  it('desactiva un responsable solicitando motivo', async () => {
    const promptSpy = vi.spyOn(window, 'prompt').mockReturnValue('Cese por solicitud');
    try {
      await c.desactivar(ejemplo);
      expect(s['cambiarEstado']).toHaveBeenCalledWith('r1', { motivo: 'Cese por solicitud' });
      expect(c.mensaje).toContain('desactivado');
    } finally {
      promptSpy.mockRestore();
    }
  });

  it('rechaza desactivar sin motivo', async () => {
    const promptSpy = vi.spyOn(window, 'prompt').mockReturnValue(null);
    try {
      await c.desactivar(ejemplo);
      expect(s['cambiarEstado']).not.toHaveBeenCalled();
      expect(c.esError).toBe(true);
    } finally {
      promptSpy.mockRestore();
    }
  });

  it('reactiva un responsable inactivo', async () => {
    const inactivo = { ...ejemplo, estado: 'inactivo' as const };
    await c.reactivar(inactivo);
    expect(s['reactivar']).toHaveBeenCalledWith('r1');
  });

  it('no navega fuera de rango en la paginación', () => {
    c.page = 3;
    c.totalPages = 3;
    c.irPagina(5);
    expect(c.page).toBe(3);
    c.irPagina(3);
    expect(c.page).toBe(3);
  });

  it('carga los vínculos del alumno indicado por query param', async () => {
    s['listarDeAlumno'] = vi.fn().mockReturnValue(of([{
      id: 'v1', responsableId: 'r1', parentesco: 'Padre', esPrincipal: true,
      accesoFinanciero: true, estado: 'activo' as const, nombres: 'Ana', apellidos: 'Mendoza',
      telefono: null, correo: null
    }]));
    c.alumnoIdSeleccionado = 'a1';
    await c.cargarVinculos();
    expect(s['listarDeAlumno']).toHaveBeenCalledWith('a1');
    expect(c.vinculos).toHaveLength(1);
  });

  it('desactiva un vínculo solicitando motivo', async () => {
    const vinculo = { id: 'v1', responsableId: 'r1', parentesco: 'Padre', esPrincipal: true, accesoFinanciero: true, estado: 'activo' as const, nombres: 'Ana', apellidos: 'Mendoza', telefono: null, correo: null };
    c.vinculos = [vinculo];
    const promptSpy = vi.spyOn(window, 'prompt').mockReturnValue('Traslado de encargado');
    try {
      await c.desactivarVinculo(vinculo);
      expect(s['desactivarVinculo']).toHaveBeenCalledWith('v1', { motivo: 'Traslado de encargado' });
      expect(c.mensaje).toContain('desactivado');
    } finally {
      promptSpy.mockRestore();
    }
  });

  it('cierra la vista de vínculos y vuelve a la lista', () => {
    c.alumnoIdSeleccionado = 'a1';
    c.vinculos = [{ id: 'v1', responsableId: 'r1', parentesco: null, esPrincipal: false, accesoFinanciero: false, estado: 'activo' as const, nombres: 'A', apellidos: 'B', telefono: null, correo: null }];
    c.cerrarVinculos();
    expect(c.alumnoIdSeleccionado).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/responsables']);
  });

  it('define hayVinculosVisibles solo cuando hay alumno seleccionado y permiso', () => {
    c.alumnoIdSeleccionado = null;
    expect(c.hayVinculosVisibles).toBe(false);
    c.alumnoIdSeleccionado = 'a1';
    expect(c.hayVinculosVisibles).toBe(true);
    permisos.clear();
    expect(c.hayVinculosVisibles).toBe(false);
  });

  const vinculo = (id = 'v1', responsableId = 'r1', estado = 'activo') => ({
    id, responsableId, parentesco: 'Padre', esPrincipal: true, accesoFinanciero: true,
    estado: estado as 'activo' | 'inactivo', nombres: 'Ana', apellidos: 'Mendoza', telefono: null, correo: null
  });

  it('busca candidatos ocultando los ya vinculados (paginación server-side)', async () => {
    c.institucionId = institucionId;
    c.vinculos = [vinculo()];
    const otro = { ...ejemplo, id: 'r2', nombres: 'Luis', apellidos: 'Ramos' };
    s['listar'] = vi.fn().mockReturnValue(of({ items: [ejemplo, otro], page: 1, pageSize: 20, totalItems: 2, totalPages: 1 }));
    c.buscarVinculable = 'Ra';
    await c.buscarCandidatos();
    expect(s['listar']).toHaveBeenCalledWith(expect.objectContaining({ institucionId, estado: 'activo', page: 1, pageSize: 20 }));
    expect(c.vinculablesCandidatos.map((x) => x.id)).toEqual(['r2']);
  });

  it('vincula un responsable existente con parentesco, principal y acceso financiero', async () => {
    c.institucionId = institucionId;
    c.alumnoIdSeleccionado = 'a1';
    c.cargarVinculos = vi.fn().mockResolvedValue(void 0);
    c.vincularForm = { responsableId: 'r2', parentesco: 'Padre', esPrincipal: true, accesoFinanciero: true };
    await c.guardarVinculo();
    expect(s['vincularAlumno']).toHaveBeenCalledWith('a1', {
      responsableId: 'r2', parentesco: 'Padre', esPrincipal: true, accesoFinanciero: true
    });
    expect(c.mostrarVincular).toBe(false);
    expect(c.mensaje).toContain('vinculado');
  });

  it('rechaza vincular sin seleccionar responsable', async () => {
    c.alumnoIdSeleccionado = 'a1';
    c.vincularForm = { responsableId: '', parentesco: 'Padre', esPrincipal: false, accesoFinanciero: false };
    await c.guardarVinculo();
    expect(s['vincularAlumno']).not.toHaveBeenCalled();
    expect(c.esError).toBe(true);
  });

  it('edita un vínculo existente', async () => {
    c.editandoVinculo = vinculo();
    c.editarVinculoForm = { parentesco: 'Madre', esPrincipal: false, accesoFinanciero: true };
    await c.guardarEditarVinculo();
    expect(s['editarVinculo']).toHaveBeenCalledWith('v1', { parentesco: 'Madre', esPrincipal: false, accesoFinanciero: true });
    expect(c.editandoVinculo).toBeNull();
    expect(c.mensaje).toContain('actualizado');
  });

  it('reactiva un vínculo inactivo', async () => {
    const v = vinculo('v1', 'r1', 'inactivo');
    await c.reactivarVinculo(v);
    expect(s['reactivarVinculo']).toHaveBeenCalledWith('v1');
    expect(c.mensaje).toContain('reactivado');
  });

  it('maneja el error al vincular y lo muestra', async () => {
    s['vincularAlumno'] = vi.fn().mockReturnValue(of(void 0).pipe());
    s['vincularAlumno'].mockImplementationOnce(() => { throw new Error('boom'); });
    c.alumnoIdSeleccionado = 'a1';
    c.vincularForm = { responsableId: 'r2', parentesco: 'Padre', esPrincipal: false, accesoFinanciero: false };
    await c.guardarVinculo();
    expect(c.esError).toBe(true);
    expect(c.mensaje).toBeTruthy();
  });
});