import { TestBed } from '@angular/core/testing';
import { SupabaseClient } from '@supabase/supabase-js';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SUPABASE_CLIENT } from './auth';
import { ConfiguracionError, ConfiguracionService } from './configuracion.service';

describe('ConfiguracionService', () => {
  let service: ConfiguracionService;
  let rpc: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    rpc = vi.fn();
    TestBed.configureTestingModule({ providers: [
      ConfiguracionService,
      { provide: SUPABASE_CLIENT, useValue: { rpc } as unknown as SupabaseClient }
    ] });
    service = TestBed.inject(ConfiguracionService);
  });

  it('carga contexto single con su institución', async () => {
    rpc.mockResolvedValue({ data: {
      multiplesInstituciones: false,
      institucion: { id: 'institucion-1', nombre: 'Centro educativo' }
    }, error: null });

    await expect(service.obtenerInstitucionActual()).resolves.toEqual({
      id: 'institucion-1', nombre: 'Centro educativo'
    });
    expect(rpc).toHaveBeenCalledWith('rpc_obtener_contexto_implementacion');
  });

  it('en modo multi exige contexto explícito', async () => {
    rpc.mockResolvedValue({ data: {
      multiplesInstituciones: true, institucion: null
    }, error: null });
    await expect(service.obtenerInstitucionActual()).rejects.toMatchObject({
      code: 'INSTITUTION_CONTEXT_REQUIRED'
    });
  });

  it('actualiza el modo mediante RPC segura', async () => {
    rpc.mockResolvedValue({ data: {
      multiplesInstituciones: true, institucion: null
    }, error: null });
    const contexto = await service.actualizarModo(true);
    expect(contexto.multiplesInstituciones).toBe(true);
    expect(rpc).toHaveBeenCalledWith('rpc_actualizar_multiples_instituciones', {
      p_multiples_instituciones: true
    });
  });

  it('traduce errores estables de configuración', async () => {
    rpc.mockResolvedValue({ data: null, error: { code: 'SM001' } });
    await expect(service.obtenerContexto()).rejects.toEqual(expect.objectContaining({
      code: 'SM001', message: 'No hay un centro educativo configurado.'
    } satisfies Partial<ConfiguracionError>));
  });
});
