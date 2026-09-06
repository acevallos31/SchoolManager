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

    // El Router real de provideRouter se espi�a para verificar navegaci�n.
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('muestra el acceso conectado y los enlaces base', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).toContain('SchoolManager');
    expect(texto).toContain('Alumnos');
    expect(texto).toContain('Cerrar sesión');
  });

  it('oculta Responsables, Matrículas y Configuración sin permisos', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Responsables');
    expect(texto).not.toContain('Matrículas');
    expect(texto).not.toContain('Configuración');
  });

  it('muestra Responsables con permiso', async () => {
    permisos.add('academico.responsables.ver');
    fixture.destroy();
    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Responsables');
  });

  it('muestra Matrículas con permiso', async () => {
    permisos.add('academico.matriculas.ver');
    fixture.destroy();
    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Matrículas');
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
    const panel = component.items[0]; // /dashboard
    const alumnos = component.items[1]; // /alumnos
    expect(component.esRutaActiva(panel)).toBe(false);
    expect(component.esRutaActiva(alumnos)).toBe(false);
  });

  it('muestra los enlaces sin permiso y filtra los que exigen permiso', () => {
    expect(component.mostrarItem(component.items[0])).toBe(true); // Panel
    expect(component.mostrarItem(component.items[1])).toBe(true); // Alumnos (mock tiene permiso)
    expect(component.mostrarItem(component.items[2])).toBe(false); // Matrículas sin permiso
    expect(component.mostrarItem(component.items[3])).toBe(false); // Responsables sin permiso
    expect(component.puedeVerConfiguracion).toBe(false);
  });

  it('expone el rol real del usuario autenticado', () => {
    expect(component.roles).toEqual(['admin']);
  });
});
