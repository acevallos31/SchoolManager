import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../environments/environment';
import { EstructuraAcademicaService, EstructuraAcademicaError } from './estructura-academica.service';

const BASE = `${environment.apiUrl}/estructura-academica`;

const GRADO = { id: 'g1', nombre: 'Primero', orden: 0, activo: true };
const JORNADA = { id: 'j1', nombre: 'Matutina', activo: true };
const SECCION = {
  id: 's1', institucionId: 'i1', cicloId: 'c1', gradoId: 'g1', gradoNombre: 'Primero',
  jornadaId: null, jornadaNombre: null, nombre: 'A', cupo: 20, activo: true,
  fechaDesactivacion: null, motivoDesactivacion: null
};

describe('EstructuraAcademicaService', () => {
  let service: EstructuraAcademicaService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(EstructuraAcademicaService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('listarGrados hace GET a /grados con institucionId y devuelve Grado[] (sin acceso directo Supabase)', async () => {
    const promesa = service.listarGrados('i1');
    const req = http.expectOne(`${BASE}/grados?institucionId=i1`);
    expect(req.request.method).toBe('GET');
    req.flush([GRADO]);

    const grados = await promesa;
    expect(grados).toHaveLength(1);
    expect(grados[0]).toMatchObject({ nombre: 'Primero', activo: true });
  });

  it('crearGrado hace POST a /grados con nombre recortado y devuelve id', async () => {
    const promesa = service.crearGrado({ nombre: '  Segundo  ', orden: 1 }, 'i1');
    const req = http.expectOne(`${BASE}/grados`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ nombre: 'Segundo', orden: 1, institucionId: 'i1' });
    req.flush({ id: 'g9' });
    await expect(promesa).resolves.toBe('g9');
  });

  it('actualizarGrado hace PUT a /grados/{gradoId} con query de institucionId', async () => {
    const promesa = service.actualizarGrado('g1', { nombre: 'Tercero', orden: 2 }, 'i1');
    const req = http.expectOne(`${BASE}/grados/g1?institucionId=i1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ nombre: 'Tercero', orden: 2 });
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('desactivarGrado hace POST a /grados/{gradoId}/desactivar sin body', async () => {
    const promesa = service.desactivarGrado('g1', 'i1');
    const req = http.expectOne(`${BASE}/grados/g1/desactivar`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({});
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('reactivarGrado hace POST a /grados/{gradoId}/reactivar', async () => {
    const promesa = service.reactivarGrado('g1', 'i1');
    const req = http.expectOne(`${BASE}/grados/g1/reactivar`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('listarJornadas hace GET a /jornadas con institucionId y devuelve Jornada[]', async () => {
    const promesa = service.listarJornadas('i1');
    const req = http.expectOne(`${BASE}/jornadas?institucionId=i1`);
    expect(req.request.method).toBe('GET');
    req.flush([JORNADA]);

    const jornadas = await promesa;
    expect(jornadas).toHaveLength(1);
    expect(jornadas[0]).toMatchObject({ nombre: 'Matutina', activo: true });
  });

  it('crearJornada hace POST a /jornadas con nombre recortado y devuelve id', async () => {
    const promesa = service.crearJornada({ nombre: '  Vespertina  ' }, 'i1');
    const req = http.expectOne(`${BASE}/jornadas`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ nombre: 'Vespertina', institucionId: 'i1' });
    req.flush({ id: 'j9' });
    await expect(promesa).resolves.toBe('j9');
  });

  it('listarSecciones hace GET a /secciones con cicloId y devuelve Seccion[] en camelCase', async () => {
    const promesa = service.listarSecciones('c1', 'i1');
    const req = http.expectOne(`${BASE}/secciones?cicloId=c1&institucionId=i1`);
    expect(req.request.method).toBe('GET');
    req.flush([SECCION]);

    const secciones = await promesa;
    expect(secciones).toHaveLength(1);
    expect(secciones[0]).toMatchObject({ cicloId: 'c1', gradoId: 'g1', nombre: 'A', cupo: 20 });
  });

  it('crearSeccion hace POST a /secciones con todos los campos y devuelve id', async () => {
    const promesa = service.crearSeccion(
      { cicloId: 'c1', gradoId: 'g1', jornadaId: 'j1', nombre: ' B ', cupo: 30 }, 'i1');
    const req = http.expectOne(`${BASE}/secciones`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      cicloId: 'c1', gradoId: 'g1', jornadaId: 'j1', nombre: 'B', cupo: 30, institucionId: 'i1'
    });
    req.flush({ id: 's9' });
    await expect(promesa).resolves.toBe('s9');
  });

  it('actualizarSeccion hace PUT a /secciones/{seccionId} con query de institucionId', async () => {
    const promesa = service.actualizarSeccion('s1',
      { cicloId: 'c1', gradoId: 'g1', jornadaId: null, nombre: 'C', cupo: 25 }, 'i1');
    const req = http.expectOne(`${BASE}/secciones/s1?institucionId=i1`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ cicloId: 'c1', gradoId: 'g1', jornadaId: null, nombre: 'C', cupo: 25 });
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('desactivarSeccion hace POST a /secciones/{seccionId}/desactivar con motivo recortado', async () => {
    const promesa = service.desactivarSeccion('s1', '  Cierre  ', 'i1');
    const req = http.expectOne(`${BASE}/secciones/s1/desactivar`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ motivo: 'Cierre' });
    req.flush(null);
    await expect(promesa).resolves.toBeUndefined();
  });

  it('mapea un 403 del servidor a EstructuraAcademicaError con code 403', async () => {
    const rechazo = service.listarGrados('i1');
    http.expectOne(`${BASE}/grados?institucionId=i1`).flush({}, { status: 403, statusText: 'Forbidden' });
    await expect(rechazo).rejects.toBeInstanceOf(EstructuraAcademicaError);
    await expect(rechazo).rejects.toMatchObject({ code: '403', message: expect.stringContaining('permiso') });
  });
});
