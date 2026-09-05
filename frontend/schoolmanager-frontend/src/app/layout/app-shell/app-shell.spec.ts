import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
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
            logout
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
    expect(texto).toContain('Matrículas');
    expect(texto).toContain('Cerrar sesión');
  });

  it('oculta Responsables y Configuración sin permisos', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Responsables');
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

  it('cierra sesión y navega a login', () => {
    component.logout();
    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('vuelve al panel principal', () => {
    component.volverAlPanel();
    expect(navigate).toHaveBeenCalledWith(['/dashboard']);
  });
});
