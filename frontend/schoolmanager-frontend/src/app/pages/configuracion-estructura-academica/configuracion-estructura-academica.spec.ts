import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { CicloEscolarService } from '../../core/services/ciclo-escolar.service';
import { ConfiguracionService } from '../../core/services/configuracion.service';
import { EstructuraAcademicaService } from '../../core/services/estructura-academica.service';
import { ConfiguracionEstructuraAcademica } from './configuracion-estructura-academica';

describe('ConfiguracionEstructuraAcademica', () => {
  let fixture: ComponentFixture<ConfiguracionEstructuraAcademica>; let component: ConfiguracionEstructuraAcademica; let permisos: Set<string>; let service: Record<string, ReturnType<typeof vi.fn>>;
  beforeEach(async () => {
    permisos = new Set(['configuracion.grados.ver', 'configuracion.grados.crear', 'configuracion.grados.editar', 'configuracion.grados.desactivar', 'configuracion.jornadas.ver', 'configuracion.jornadas.crear', 'configuracion.jornadas.editar', 'configuracion.jornadas.desactivar', 'configuracion.secciones.ver', 'configuracion.secciones.crear', 'configuracion.secciones.editar', 'configuracion.secciones.desactivar']);
    service = { listarGrados: vi.fn().mockResolvedValue([]), crearGrado: vi.fn().mockResolvedValue('g1'), actualizarGrado: vi.fn(), desactivarGrado: vi.fn(), reactivarGrado: vi.fn(), listarJornadas: vi.fn().mockResolvedValue([]), crearJornada: vi.fn(), actualizarJornada: vi.fn(), desactivarJornada: vi.fn(), reactivarJornada: vi.fn(), listarSecciones: vi.fn().mockResolvedValue([]), crearSeccion: vi.fn(), actualizarSeccion: vi.fn(), desactivarSeccion: vi.fn(), reactivarSeccion: vi.fn() };
    await TestBed.configureTestingModule({ imports: [ConfiguracionEstructuraAcademica], providers: [provideRouter([]), { provide: AuthService, useValue: { tienePermiso: (permission: string) => permisos.has(permission) } }, { provide: ConfiguracionService, useValue: { obtenerContexto: vi.fn().mockResolvedValue({ multiplesInstituciones: false, institucion: { id: 'i1', nombre: 'Centro' } }) } }, { provide: CicloEscolarService, useValue: { listar: vi.fn().mockResolvedValue([{ id: 'c1', nombre: '2027', activo: true }]) } }, { provide: EstructuraAcademicaService, useValue: service }] }).compileComponents();
    fixture = TestBed.createComponent(ConfiguracionEstructuraAcademica); component = fixture.componentInstance; fixture.detectChanges(); await component.cargar(); fixture.detectChanges();
  });
  it('muestra el estado vacío de grados y permite crearlo', async () => { expect(fixture.nativeElement.textContent).toContain('No hay grados configurados.'); component.abrirGrado(); component.gradoForm = { nombre: 'Primero', orden: 0 }; await component.guardarGrado(); expect(service['crearGrado']).toHaveBeenCalledWith({ nombre: 'Primero', orden: 0 }, 'i1'); });
  it('oculta acciones sin permiso y permite crear secciones sin jornada', async () => { fixture.destroy(); permisos.clear(); fixture = TestBed.createComponent(ConfiguracionEstructuraAcademica); component = fixture.componentInstance; fixture.detectChanges(); await component.cargar(); fixture.detectChanges(); expect(fixture.nativeElement.textContent).not.toContain('Nuevo grado'); permisos.add('configuracion.secciones.crear'); component.cicloId = 'c1'; component.abrirSeccion(); component.seccionForm = { cicloId: 'c1', gradoId: 'g1', jornadaId: null, nombre: 'A', cupo: null }; await component.guardarSeccion(); expect(service['crearSeccion']).toHaveBeenCalledWith(component.seccionForm, 'i1'); });
  it('rechaza cupo cero y muestra errores del servicio', async () => { component.cicloId = 'c1'; component.abrirSeccion(); component.seccionForm = { cicloId: 'c1', gradoId: 'g1', jornadaId: null, nombre: 'A', cupo: 0 }; await component.guardarSeccion(); expect(component.mensaje).toContain('entero positivo'); });
});