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

  it('obtiene configuración institucional tipada en una RPC', async () => {
    rpc.mockResolvedValue({ data: {
      multiplesInstituciones: false,
      institucion: {
        id: 'institucion-1', nombre: 'Centro', nombreCorto: null,
        direccion: null, telefono: null, correo: null, logoUrl: null
      },
      identificadores: {
        rneRequerido: true, identificacionCivilRequerida: false,
        codigoInternoRequerido: false, tiposIdentificacionPermitidos: ['identidad']
      }
    }, error: null });

    const resultado = await service.obtenerConfiguracionInstitucion();
    expect(resultado.identificadores?.rneRequerido).toBe(true);
    expect(rpc).toHaveBeenCalledWith('rpc_obtener_configuracion_institucion', undefined);
  });

  it('crear y actualizar institución usan RPC seguras', async () => {
    const respuesta = {
      multiplesInstituciones: false,
      institucion: {
        id: 'institucion-1', nombre: 'Centro', nombreCorto: null,
        direccion: null, telefono: null, correo: null, logoUrl: null
      },
      identificadores: {
        rneRequerido: false, identificacionCivilRequerida: false,
        codigoInternoRequerido: false, tiposIdentificacionPermitidos: ['identidad']
      }
    };
    rpc.mockResolvedValue({ data: respuesta, error: null });
    const input = {
      nombre: ' Centro ', nombreCorto: null, direccion: null, telefono: null,
      correo: null, logoUrl: null, identificadores: respuesta.identificadores
    };

    await service.crearInstitucion(input);
    await service.actualizarInstitucion('institucion-1', input);

    expect(rpc).toHaveBeenNthCalledWith(1, 'rpc_crear_institucion',
      expect.objectContaining({ p_nombre: 'Centro', p_rne_requerido: false }));
    expect(rpc).toHaveBeenNthCalledWith(2, 'rpc_actualizar_institucion',
      expect.objectContaining({ p_institucion_id: 'institucion-1', p_nombre: 'Centro' }));
  });
});
