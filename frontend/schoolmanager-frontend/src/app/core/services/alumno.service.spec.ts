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

});

function fluentQuery(result: unknown) {
  const query = { select: vi.fn(), eq: vi.fn(), order: vi.fn() };
  query.select.mockReturnValue(query);
  query.eq.mockReturnValue(query);
  query.order.mockResolvedValue(result);
  return query;
}
