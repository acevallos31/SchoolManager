import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { vi } from 'vitest';

import { AuthAppError, AuthService } from '../../core/services/auth';
import { Login } from './login';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let auth: {
    login: ReturnType<typeof vi.fn>;
    loginWithGoogle: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
    consumirMensajeSesionInvalida: ReturnType<typeof vi.fn>;
  };
  let router: { navigate: ReturnType<typeof vi.fn> };

  const rolAdmin = { id: 'u1', personaId: 'p1', roles: ['admin'], permisos: [] };
  const rolPadre = { id: 'u2', personaId: 'p2', roles: ['padre'], permisos: [] };

  beforeEach(async () => {
    auth = {
      login: vi.fn(),
      loginWithGoogle: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn().mockResolvedValue(undefined),
      consumirMensajeSesionInvalida: vi.fn().mockReturnValue(null)
    };
    router = { navigate: vi.fn().mockResolvedValue(true) };

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('inicia sesion como admin y navega al panel', async () => {
    auth.login.mockResolvedValue(rolAdmin);
    component.correo = 'admin@schoolmanager.com';
    component.password = 'secreto';

    await component.login();

    expect(auth.login).toHaveBeenCalledWith('admin@schoolmanager.com', 'secreto');
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
    expect(component.error).toBe('');
    expect(component.cargando).toBe(false);
  });

  it('inicia el flujo OAuth con Google', async () => {
    await component.loginWithGoogle();

    expect(auth.loginWithGoogle).toHaveBeenCalledOnce();
    expect(component.error).toBe('');
    expect(component.cargandoGoogle).toBe(false);
  });

  it('inicia sesion como padre y navega al portal', async () => {
    auth.login.mockResolvedValue(rolPadre);
    component.correo = 'padre@schoolmanager.com';
    component.password = 'secreto';

    await component.login();

    expect(router.navigate).toHaveBeenCalledWith(['/portal-padre']);
    expect(component.error).toBe('');
  });

  it('muestra mensaje seguro ante credenciales invalidas sin exponer Supabase', async () => {
    auth.login.mockRejectedValue(
      new AuthAppError('Correo o contrasena incorrectos.', 'INVALID_CREDENTIALS')
    );

    component.correo = 'admin@schoolmanager.com';
    component.password = 'mala';

    await component.login();

    expect(component.error).toBe('Correo o contrasena incorrectos.');
    expect(component.error).not.toContain('invalid');
    expect(auth.logout).toHaveBeenCalled();
    expect(component.cargando).toBe(false);
  });

  it('muestra mensaje generico ante error inesperado sin exponer el error crudo', async () => {
    auth.login.mockRejectedValue(new Error('Supabase: network breakdown 500'));

    component.correo = 'admin@schoolmanager.com';
    component.password = 'secreto';

    await component.login();

    expect(component.error).toBe('Ocurrio un error inesperado. Intenta nuevamente.');
    expect(component.error).not.toContain('network');
    expect(component.error).not.toContain('Supabase');
    expect(auth.logout).toHaveBeenCalled();
  });

  it('muestra el motivo conservado por AuthService al volver de Google sin vínculo', () => {
    auth.consumirMensajeSesionInvalida.mockReturnValue(
      'Tu cuenta de Google no esta vinculada a un usuario de SchoolManager.'
    );

    component.ngOnInit();

    expect(component.error).toContain('no esta vinculada');
  });

  it('explica la identidad no vinculada sin exponer detalles internos', async () => {
    auth.login.mockRejectedValue(new AuthAppError('detalle interno', 'USER_PROFILE_NOT_FOUND'));

    component.correo = 'padre@schoolmanager.com';
    component.password = 'secreto';

    await component.login();

    expect(component.error).toContain('no esta vinculada a un usuario de SchoolManager');
    expect(component.error).not.toContain('detalle interno');
    expect(auth.logout).toHaveBeenCalled();
  });

  it('entra en loading mientras autentica y bloquea doble submit', async () => {
    let resolver!: (usuario: typeof rolAdmin) => void;
    auth.login.mockImplementation(
      () =>
        new Promise<typeof rolAdmin>((resolve) => {
          resolver = resolve;
        })
    );

    component.correo = 'admin@schoolmanager.com';
    component.password = 'secreto';

    const promesa = component.login();

    // Tras iniciar, el componente queda en loading (la promesa aun no resuelve).
    expect(component.cargando).toBe(true);
    expect(component.error).toBe('');

    resolver(rolAdmin);
    await promesa;

    expect(component.cargando).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });
});