import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { AppShell } from './app-shell';

describe('AppShell', () => {
  let component: AppShell;
  let fixture: ComponentFixture<AppShell>;
  let logout: ReturnType<typeof vi.fn>;
  let navigate: ReturnType<typeof vi.fn>;
  let permisos: Set<string>;

  beforeEach(async () => {
    permisos = new Set(['academico.alumnos.ver']);
    logout = vi.fn().mockResolvedValue(undefined);
    navigate = vi.fn().mockResolvedValue(true);

    await TestBed.configureTestingModule({
      imports: [AppShell],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            tienePermiso: (p: string) => permisos.has(p),
            logout,
            usuarioActual$: new BehaviorSubject({ id: 'u1', personaId: 'p1', roles: ['admin'], permisos: [] })
          }
        }
      ]
    }).compileComponents();

    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const recrearComponente = async () => {
    fixture.destroy();
    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  };

  it('muestra el acceso conectado y los enlaces base', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).toContain('SchoolManager');
    expect(texto).toContain('Alumnos');
    expect(texto).toContain('Cerrar sesión');
  });

  it('oculta módulos sin sus permisos de lectura', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Responsables');
    expect(texto).not.toContain('Matrículas');
    expect(texto).not.toContain('Ciclos escolares');
    expect(texto).not.toContain('Estructura académica');
    expect(texto).not.toContain('Configuración');
  });

  it('muestra Responsables con permiso', async () => {
    permisos.add('academico.responsables.ver');
    await recrearComponente();
    expect(fixture.nativeElement.textContent).toContain('Responsables');
  });

  it('muestra Matrículas con permiso', async () => {
    permisos.add('academico.matriculas.ver');
    await recrearComponente();
    expect(fixture.nativeElement.textContent).toContain('Matrículas');
  });

  it('muestra Ciclos escolares con el permiso de aplicación', async () => {
    permisos.add('academico.ciclos.ver');
    await recrearComponente();
    expect(fixture.nativeElement.textContent).toContain('Ciclos escolares');
  });

  it('muestra Estructura académica con el permiso de aplicación', async () => {
    permisos.add('academico.estructura.ver');
    await recrearComponente();
    expect(fixture.nativeElement.textContent).toContain('Estructura académica');
  });

  it('no exige acceso general a Configuración para mostrar accesos académicos directos', async () => {
    permisos.add('academico.ciclos.ver');
    permisos.add('academico.estructura.ver');
    await recrearComponente();

    const texto = fixture.nativeElement.textContent;
    expect(texto).toContain('Ciclos escolares');
    expect(texto).toContain('Estructura académica');
    expect(texto).not.toContain('Configuración');
  });

  it('cierra sesión y navega a login', () => {
    component.logout();
    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('vuelve al panel principal', () => {
    component.volverAlPanel();
    expect(navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('alterna el drawer móvil y lo cierra al navegar', () => {
    expect(component.navAbierta).toBe(false);
    component.alternarNav();
    expect(component.navAbierta).toBe(true);
    component.alternarNav();
    expect(component.navAbierta).toBe(false);
    component.alternarNav();
    component.cerrarNav();
    expect(component.navAbierta).toBe(false);
  });

  it('marca activo el panel solo en su ruta exacta', () => {
    const panel = component.items.find(item => item.ruta === '/dashboard')!;
    const alumnos = component.items.find(item => item.ruta === '/alumnos')!;
    expect(component.esRutaActiva(panel)).toBe(false);
    expect(component.esRutaActiva(alumnos)).toBe(false);
  });

  it('muestra los enlaces sin permiso y filtra los que exigen permiso', () => {
    const panel = component.items.find(item => item.ruta === '/dashboard')!;
    const alumnos = component.items.find(item => item.ruta === '/alumnos')!;
    const matriculas = component.items.find(item => item.ruta === '/matriculas')!;
    const ciclos = component.items.find(item => item.ruta === '/configuracion/ciclos')!;
    const estructura = component.items.find(item => item.ruta === '/configuracion/estructura-academica')!;

    expect(component.mostrarItem(panel)).toBe(true);
    expect(component.mostrarItem(alumnos)).toBe(true);
    expect(component.mostrarItem(matriculas)).toBe(false);
    expect(component.mostrarItem(ciclos)).toBe(false);
    expect(component.mostrarItem(estructura)).toBe(false);
    expect(component.puedeVerConfiguracion).toBe(false);
  });

  it('expone el rol real del usuario autenticado', () => {
    expect(component.roles).toEqual(['admin']);
  });
});
