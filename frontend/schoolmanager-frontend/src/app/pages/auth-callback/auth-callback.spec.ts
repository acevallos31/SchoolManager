import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';

import { AuthService } from '../../core/services/auth';
import { AuthCallback } from './auth-callback';

describe('AuthCallback', () => {
  let component: AuthCallback;
  let fixture: ComponentFixture<AuthCallback>;
  let auth: {
    asegurarUsuarioInicial: ReturnType<typeof vi.fn>;
    isLoggedIn: ReturnType<typeof vi.fn>;
    tieneRol: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    auth = {
      asegurarUsuarioInicial: vi.fn().mockResolvedValue(undefined),
      isLoggedIn: vi.fn(),
      tieneRol: vi.fn()
    };
    router = { navigate: vi.fn().mockResolvedValue(true) };

    await TestBed.configureTestingModule({
      imports: [AuthCallback],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AuthCallback);
    component = fixture.componentInstance;
  });

  it('redirige al login cuando no existe una sesión válida', async () => {
    auth.isLoggedIn.mockReturnValue(false);

    await component.ngOnInit();

    expect(auth.asegurarUsuarioInicial).toHaveBeenCalledOnce();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(component.mensaje).toContain('No se pudo validar');
  });

  it('redirige al portal cuando la sesión pertenece a un padre', async () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tieneRol.mockImplementation((rol: string) => rol === 'padre');

    await component.ngOnInit();

    expect(router.navigate).toHaveBeenCalledWith(['/portal-padre']);
  });

  it('redirige al dashboard para una sesión administrativa', async () => {
    auth.isLoggedIn.mockReturnValue(true);
    auth.tieneRol.mockReturnValue(false);

    await component.ngOnInit();

    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });
});
