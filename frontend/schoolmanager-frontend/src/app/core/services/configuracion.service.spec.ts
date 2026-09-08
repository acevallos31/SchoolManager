import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { ConfiguracionService, GuardarInstitucionInput } from './configuracion.service';

const contexto = { multiplesInstituciones: false, institucion: { id: 'institucion-1', nombre: 'Centro' } };
const identificadores = {
  rneRequerido: true, identificacionCivilRequerida: false,
  codigoInternoRequerido: false, tiposIdentificacionPermitidos: ['identidad']
};
const configuracion = {
  ...contexto,
  institucion: { ...contexto.institucion, nombreCorto: null, direccion: null, telefono: null, correo: null, logoUrl: null },
  identificadores
};

describe('ConfiguracionService', () => {
  let service: ConfiguracionService;
  let http: HttpTestingController;
  const url = `${environment.apiUrl}/configuracion`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ConfiguracionService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('carga contexto single por API', async () => {
    const result = service.obtenerInstitucionActual();
    const request = http.expectOne(`${url}/contexto`);
    expect(request.request.method).toBe('GET');
    request.flush(contexto);
    await expect(result).resolves.toEqual(contexto.institucion);
  });

  it('en modo multi exige contexto explícito', async () => {
    const result = service.obtenerInstitucionActual();
    const assertion = expect(result).rejects.toMatchObject({ code: 'INSTITUTION_CONTEXT_REQUIRED' });
    http.expectOne(`${url}/contexto`).flush({ multiplesInstituciones: true, institucion: null });
    await assertion;
  });

  it('consulta el modo', async () => {
    const result = service.esMultiInstitucion();
    http.expectOne(`${url}/contexto`).flush(contexto);
    await expect(result).resolves.toBe(false);
  });

  it('actualiza modo por PUT', async () => {
    const result = service.actualizarModo(true);
    const request = http.expectOne(`${url}/modo`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ multiplesInstituciones: true });
    request.flush({ multiplesInstituciones: true, institucion: null });
    await expect(result).resolves.toMatchObject({ multiplesInstituciones: true });
  });

  it.each([undefined, 'institucion-1'])('obtiene configuración con contexto %s', async id => {
    const result = service.obtenerConfiguracionInstitucion(id);
    const request = http.expectOne(`${url}/institucion${id ? `?institucionId=${id}` : ''}`);
    expect(request.request.method).toBe('GET');
    request.flush(configuracion);
    await expect(result).resolves.toEqual(configuracion);
  });

  it('permite configuración todavía vacía', async () => {
    const result = service.obtenerConfiguracionInstitucion();
    const vacia = { multiplesInstituciones: false, institucion: null, identificadores: null };
    http.expectOne(`${url}/institucion`).flush(vacia);
    await expect(result).resolves.toEqual(vacia);
  });

  it.each(['POST', 'PUT'])('guarda por %s preservando normalización y contrato', async method => {
    const input: GuardarInstitucionInput = {
      nombre: ' Centro ', nombreCorto: ' ', direccion: ' Calle ', telefono: null,
      correo: null, logoUrl: null, identificadores
    };
    const result = method === 'POST' ? service.crearInstitucion(input) : service.actualizarInstitucion('institucion-1', input);
    const request = http.expectOne(`${url}/instituciones${method === 'PUT' ? '/institucion-1' : ''}`);
    expect(request.request.method).toBe(method);
    expect(request.request.body).toEqual({ ...input, nombre: 'Centro', nombreCorto: null, direccion: 'Calle' });
    request.flush(configuracion);
    await expect(result).resolves.toEqual(configuracion);
    expect(input.nombre).toBe(' Centro ');
  });

  it.each(['SM001', 'SM002', 'SM003', 'SM004', '42501', '23505', 'P0002', '22023'])('conserva código estable %s', async code => {
    const result = service.obtenerContexto();
    const assertion = expect(result).rejects.toMatchObject({ name: 'ConfiguracionError', code });
    http.expectOne(`${url}/contexto`).flush({ code, error: 'Error de prueba' }, { status: 400, statusText: 'Bad Request' });
    await assertion;
  });

  it('traduce 403 de policy sin payload SQL', async () => {
    const result = service.actualizarModo(true);
    const assertion = expect(result).rejects.toMatchObject({ code: '42501' });
    http.expectOne(`${url}/modo`).flush(null, { status: 403, statusText: 'Forbidden' });
    await assertion;
  });

  it('traduce errores de red', async () => {
    const result = service.obtenerContexto();
    const assertion = expect(result).rejects.toMatchObject({ code: 'UNKNOWN' });
    http.expectOne(`${url}/contexto`).error(new ProgressEvent('error'));
    await assertion;
  });

  it.each([
    {},
    { multiplesInstituciones: false, institucion: {} }
  ])('rechaza contexto inválido %j', async payload => {
    const result = service.obtenerContexto();
    const assertion = expect(result).rejects.toMatchObject({ code: 'INVALID_RESPONSE' });
    http.expectOne(`${url}/contexto`).flush(payload);
    await assertion;
  });

  it.each([
    {},
    { ...configuracion, identificadores: null },
    { ...configuracion, identificadores: { ...identificadores, tiposIdentificacionPermitidos: [3] } }
  ])('rechaza configuración inválida %j', async payload => {
    const result = service.obtenerConfiguracionInstitucion();
    const assertion = expect(result).rejects.toMatchObject({ code: 'INVALID_RESPONSE' });
    http.expectOne(`${url}/institucion`).flush(payload);
    await assertion;
  });
});
