import { TestBed } from '@angular/core/testing';
import { SupabaseClient } from '@supabase/supabase-js';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AlumnoService } from './alumno.service';
import { SUPABASE_CLIENT } from './auth';

describe('AlumnoService', () => {
  let service: AlumnoService;
  let rpc: ReturnType<typeof vi.fn>;
  let from: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    rpc = vi.fn().mockResolvedValue({ data: null, error: null });
    from = vi.fn();
    TestBed.configureTestingModule({
      providers: [
        AlumnoService,
        { provide: SUPABASE_CLIENT, useValue: { rpc, from } as unknown as SupabaseClient }
      ]
    });
    service = TestBed.inject(AlumnoService);
  });

  it('crear usa RPC atómica y no INSERT directo', async () => {
    rpc.mockResolvedValue({ data: 'alumno-id', error: null });
    const id = await service.crear({
      institucionId: 'institucion-id', nombres: ' Ana ', apellidos: ' López ',
      tipoIdentificacion: 'identidad', numeroIdentificacion: '0801-2008',
      fechaNacimiento: '2008-01-01', rne: null, codigoInterno: null
    });

    expect(id).toBe('alumno-id');
    expect(rpc).toHaveBeenCalledWith(
      'rpc_crear_alumno_nueva_persona_con_documento',
      expect.objectContaining({ p_nombres: 'Ana', p_apellidos: 'López', p_numero_identificacion: '0801-2008' })
    );
    expect(from).not.toHaveBeenCalled();
  });

  it('desactivar y reactivar usan sus RPC seguras', async () => {
    await service.desactivar('alumno-id', ' Retiro solicitado ');
    await service.reactivar('alumno-id');

    expect(rpc).toHaveBeenNthCalledWith(1, 'rpc_desactivar_alumno', {
      p_alumno_id: 'alumno-id', p_motivo: 'Retiro solicitado'
    });
    expect(rpc).toHaveBeenNthCalledWith(2, 'rpc_reactivar_alumno', { p_alumno_id: 'alumno-id' });
  });

  it('lista y transforma alumno sin matrícula', async () => {
    const query = fluentQuery({
      data: [{
        id: 'alumno-id', persona_id: 'persona-id', rne: null, estado: 'activo',
        persona: { nombres: 'Ana', apellidos: 'López', numero_identificacion: '0801' },
        matriculas: []
      }], error: null
    });
    from.mockReturnValue(query);

    const alumnos = await service.listar();

    expect(from).toHaveBeenCalledWith('alumnos');
    expect(alumnos[0]).toMatchObject({ nombreCompleto: 'Ana López', identidad: '0801', matriculaActual: null });
  });

  it('listar incluye codigo_interno en la lectura y lo mapea a codigoInterno', async () => {
    const query = fluentQuery({
      data: [{
        id: 'alumno-id', persona_id: 'persona-id', rne: null, codigo_interno: 'CI-2026-0001', estado: 'activo',
        persona: { nombres: 'Ana', apellidos: 'López', numero_identificacion: '0801' },
        matriculas: []
      }], error: null
    });
    from.mockReturnValue(query);

    const alumnos = await service.listar();

    expect(alumnos[0].codigoInterno).toBe('CI-2026-0001');
    expect(query.select.mock.calls[0][0]).toContain('codigo_interno');
  });

  it('obtenerPorId mapea codigoInterno desde codigo_interno', async () => {
    const query = fluentQuery({
      data: {
        id: 'alumno-id', persona_id: 'persona-id', rne: null, codigo_interno: 'CI-2026-0001', estado: 'activo',
        persona: { nombres: 'Ana', apellidos: 'López', numero_identificacion: '0801' }
      }, error: null
    });
    from.mockReturnValue(query);

    const alumno = await service.obtenerPorId('alumno-id');

    expect(alumno?.codigoInterno).toBe('CI-2026-0001');
    expect(query.select.mock.calls[0][0]).toContain('codigo_interno');
  });

  it('buscarPaginado incluye codigo_interno en select y en la búsqueda por término', async () => {
    const query = fluentQuery({
      data: [{
        id: 'alumno-id', persona_id: 'persona-id', rne: null, codigo_interno: 'CI-2026-0001', estado: 'activo',
        persona: { nombres: 'Ana', apellidos: 'López', numero_identificacion: '0801' }
      }], error: null, count: 1
    });
    from.mockReturnValue(query);

    const { items } = await service.buscarPaginado({ termino: 'CI-2026', page: 1, pageSize: 10 });

    expect(items[0].codigoInterno).toBe('CI-2026-0001');
    expect(query.select.mock.calls[0][0]).toContain('codigo_interno');
    expect(query.or.mock.calls[0][0]).toContain('codigo_interno.ilike.%CI-2026%');
  });

  it('buscarPaginado sin término no aplica or (comportamiento existente intacto)', async () => {
    const query = fluentQuery({
      data: [{
        id: 'alumno-id', persona_id: 'persona-id', rne: null, codigo_interno: null, estado: 'activo',
        persona: { nombres: 'Ana', apellidos: 'López', numero_identificacion: '0801' }
      }], error: null, count: 1
    });
    from.mockReturnValue(query);

    const { items, total } = await service.buscarPaginado({ page: 1, pageSize: 10 });

    expect(total).toBe(1);
    expect(items[0].codigoInterno).toBeNull();
    expect(query.or).not.toHaveBeenCalled();
  });

});

function fluentQuery(result: unknown) {
  interface Fluent {
    select: ReturnType<typeof vi.fn>;
    eq: ReturnType<typeof vi.fn>;
    or: ReturnType<typeof vi.fn>;
    order: ReturnType<typeof vi.fn>;
    range: ReturnType<typeof vi.fn>;
    maybeSingle: ReturnType<typeof vi.fn>;
    then(onFulfilled?: (value: unknown) => unknown): Promise<unknown>;
  }
  const query = {
    select: vi.fn(),
    eq: vi.fn(),
    or: vi.fn(),
    order: vi.fn(),
    range: vi.fn(),
    maybeSingle: vi.fn()
  } as Fluent;
  query.select.mockReturnValue(query);
  query.eq.mockReturnValue(query);
  query.or.mockReturnValue(query);
  query.order.mockReturnValue(query);
  query.range.mockReturnValue(query);
  query.maybeSingle.mockReturnValue(query);
  query.then = (onFulfilled?: (value: unknown) => unknown) =>
    Promise.resolve(result).then(onFulfilled);
  return query;
}
