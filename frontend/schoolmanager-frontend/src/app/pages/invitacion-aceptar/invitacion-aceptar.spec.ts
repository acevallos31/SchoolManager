import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import {
  INVITACION_TOKEN_SESSION_KEY,
  InvitacionAccesoError,
  InvitacionAccesoService
} from '../../core/services/invitacion-acceso.service';
import { OAuthProviderService } from '../../core/services/oauth-provider.service';
import { InvitacionAceptar } from './invitacion-aceptar';

describe('InvitacionAceptar', () => {
  let fixture: ComponentFixture<InvitacionAceptar>;
  let component: InvitacionAceptar;
  let auth: { asegurarUsuarioInicial: ReturnType<typeof vi.fn>; getToken: ReturnType<typeof vi.fn> };
  let oauth: {
    continuarConGoogle: ReturnType<typeof vi.fn>;
    continuarConMicrosoft: ReturnType<typeof vi.fn>;
  };
  let invitaciones: { aceptar: ReturnType<typeof vi.fn> };
  let tokenParam: string | null;

  beforeEach(async () => {
    sessionStorage.clear();
    tokenParam = 'abcdefghijklmnopqrstuvwxyz1234567890TOKEN';

    auth = {
      asegurarUsuarioInicial: vi.fn().mockResolvedValue(undefined),
      getToken: vi.fn().mockReturnValue('jwt')
    };
    oauth = {
      continuarConGoogle: vi.fn().mockResolvedValue(undefined),
      continuarConMicrosoft: vi.fn().mockResolvedValue(undefined)
    };
    invitaciones = {
      aceptar: vi.fn().mockResolvedValue({
        invitacionId: 'inv-1', usuarioId: 'u-1', institucionId: 'i-1', estado: 'aceptada'
      })
    };

    await TestBed.configureTestingModule({
      imports: [InvitacionAceptar],
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: OAuthProviderService, useValue: oauth },
        { provide: InvitacionAccesoService, useValue: invitaciones },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              queryParamMap: {
                get: (name: string) => name === 'token' ? tokenParam : null
              }
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(InvitacionAceptar);
    component = fixture.componentInstance;
  });

  afterEach(() => sessionStorage.clear());

  it('acepta la invitación con una sesión existente y elimina el token temporal', async () => {
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('aceptada'));

    expect(auth.asegurarUsuarioInicial).toHaveBeenCalled();
    expect(invitaciones.aceptar).toHaveBeenCalledWith(tokenParam);
    expect(sessionStorage.getItem(INVITACION_TOKEN_SESSION_KEY)).toBeNull();
  });

  it('conserva temporalmente el token y ofrece OAuth cuando no hay sesión', async () => {
    auth.getToken.mockReturnValue(null);
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('autenticacion'));
    expect(sessionStorage.getItem(INVITACION_TOKEN_SESSION_KEY)).toBe(tokenParam);

    await component.continuarConGoogle();
    expect(oauth.continuarConGoogle).toHaveBeenCalled();
  });

  it('recupera el token desde el fragmento y limpia la URL antes de aceptar', async () => {
    tokenParam = null;
    history.replaceState(history.state, '', '/invitacion/aceptar#token=fragment-token-abcdefghijklmnopqrstuvwxyz123456');
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('aceptada'));
    expect(invitaciones.aceptar).toHaveBeenCalledWith('fragment-token-abcdefghijklmnopqrstuvwxyz123456');
    expect(window.location.hash).toBe('');
  });

  it('recupera el token temporal de sessionStorage después del retorno OAuth', async () => {
    tokenParam = null;
    sessionStorage.setItem(INVITACION_TOKEN_SESSION_KEY, 'session-token-abcdefghijklmnopqrstuvwxyz123456');
    history.replaceState(history.state, '', '/invitacion/aceptar');
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('aceptada'));
    expect(invitaciones.aceptar).toHaveBeenCalledWith('session-token-abcdefghijklmnopqrstuvwxyz123456');
    expect(sessionStorage.getItem(INVITACION_TOKEN_SESSION_KEY)).toBeNull();
  });

  it('permite iniciar Microsoft OAuth y conserva el token', async () => {
    auth.getToken.mockReturnValue(null);
    fixture.detectChanges();
    await vi.waitFor(() => expect(component.estado).toBe('autenticacion'));

    await component.continuarConMicrosoft();

    expect(oauth.continuarConMicrosoft).toHaveBeenCalled();
    expect(component.proveedorCargando).toBe('microsoft');
    expect(sessionStorage.getItem(INVITACION_TOKEN_SESSION_KEY)).toBe(tokenParam);
  });

  it('muestra error y libera el proveedor si OAuth falla', async () => {
    auth.getToken.mockReturnValue(null);
    oauth.continuarConGoogle.mockRejectedValueOnce(new Error('oauth failure'));
    fixture.detectChanges();
    await vi.waitFor(() => expect(component.estado).toBe('autenticacion'));

    await component.continuarConGoogle();

    expect(component.estado).toBe('error');
    expect(component.proveedorCargando).toBeNull();
    expect(component.mensaje).toContain('No se pudo iniciar');
  });

  it('vuelve a autenticación si el backend responde 401 al aceptar', async () => {
    invitaciones.aceptar.mockRejectedValueOnce(
      new InvitacionAccesoError('Debes autenticarte para aceptar la invitación.', 401)
    );
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('autenticacion'));
    expect(component.mensaje).toBe('');
    expect(sessionStorage.getItem(INVITACION_TOKEN_SESSION_KEY)).toBe(tokenParam);
  });

  it('muestra el error funcional y elimina el token si la invitación no es válida', async () => {
    invitaciones.aceptar.mockRejectedValueOnce(
      new InvitacionAccesoError('La invitación expiró.', 400)
    );
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('error'));
    expect(component.mensaje).toBe('La invitación expiró.');
    expect(sessionStorage.getItem(INVITACION_TOKEN_SESSION_KEY)).toBeNull();
  });

  it('usa fallback seguro ante un error no estándar del backend', async () => {
    invitaciones.aceptar.mockRejectedValueOnce('fallo-no-error');
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('error'));
    expect(component.mensaje).toBe('No se pudo validar la invitación.');
  });

  it('muestra error si no hay token en URL ni en la sesión del navegador', async () => {
    tokenParam = null;
    history.replaceState(history.state, '', '/invitacion/aceptar');
    fixture.detectChanges();

    await vi.waitFor(() => expect(component.estado).toBe('error'));
    expect(invitaciones.aceptar).not.toHaveBeenCalled();
    expect(component.mensaje).toContain('no contiene un token');
  });
});
