import { TestBed } from '@angular/core/testing';
import { SupabaseClient } from '@supabase/supabase-js';
import { vi } from 'vitest';
import { SUPABASE_CLIENT } from './auth';
import { EstructuraAcademicaService } from './estructura-academica.service';

describe('EstructuraAcademicaService', () => {
  it('usa las RPC 016 con los parámetros correctos', async () => {
    const rpc = vi.fn().mockResolvedValue({ data: [], error: null });
    TestBed.configureTestingModule({ providers: [EstructuraAcademicaService, { provide: SUPABASE_CLIENT, useValue: { rpc } as unknown as SupabaseClient }] });
    const service = TestBed.inject(EstructuraAcademicaService);
    await service.listarGrados('i1'); await service.crearGrado({ nombre: ' A ', orden: 0 }, 'i1'); await service.actualizarGrado('g1', { nombre: 'B', orden: 1 }, 'i1'); await service.desactivarGrado('g1', 'i1'); await service.reactivarGrado('g1', 'i1');
    await service.listarJornadas('i1'); await service.crearJornada({ nombre: ' Matutina ' }, 'i1'); await service.actualizarJornada('j1', { nombre: 'Diurna' }, 'i1'); await service.desactivarJornada('j1', 'i1'); await service.reactivarJornada('j1', 'i1');
    await service.listarSecciones('c1', 'i1'); await service.crearSeccion({ cicloId: 'c1', gradoId: 'g1', jornadaId: null, nombre: ' A ', cupo: null }, 'i1'); await service.actualizarSeccion('s1', { cicloId: 'c1', gradoId: 'g1', jornadaId: 'j1', nombre: 'B', cupo: 20 }, 'i1'); await service.desactivarSeccion('s1', 'Cierre', 'i1'); await service.reactivarSeccion('s1', 'i1');
    expect(rpc).toHaveBeenCalledWith('rpc_crear_grado', { p_nombre: 'A', p_orden: 0, p_institucion_id: 'i1' });
    expect(rpc).toHaveBeenCalledWith('rpc_crear_jornada', { p_nombre: 'Matutina', p_institucion_id: 'i1' });
    expect(rpc).toHaveBeenCalledWith('rpc_listar_secciones', { p_ciclo_id: 'c1', p_institucion_id: 'i1' });
    expect(rpc).toHaveBeenCalledWith('rpc_crear_seccion', { p_institucion_id: 'i1', p_ciclo_id: 'c1', p_grado_id: 'g1', p_jornada_id: null, p_nombre: 'A', p_cupo: null });
    expect(rpc).toHaveBeenCalledWith('rpc_actualizar_seccion', { p_seccion_id: 's1', p_ciclo_id: 'c1', p_grado_id: 'g1', p_jornada_id: 'j1', p_nombre: 'B', p_cupo: 20, p_institucion_id: 'i1' });
    expect(rpc).toHaveBeenCalledWith('rpc_desactivar_seccion', { p_seccion_id: 's1', p_motivo: 'Cierre', p_institucion_id: 'i1' });
    expect(rpc).toHaveBeenCalledWith('rpc_reactivar_seccion', { p_seccion_id: 's1', p_institucion_id: 'i1' });
  });
});