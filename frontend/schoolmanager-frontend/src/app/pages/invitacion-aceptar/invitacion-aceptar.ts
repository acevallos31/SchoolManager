import { CommonModule, isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectorRef,
  Component,
  inject,
  OnInit,
  PLATFORM_ID
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services/auth';
import {
  INVITACION_TOKEN_SESSION_KEY,
  InvitacionAccesoError,
  InvitacionAccesoService
} from '../../core/services/invitacion-acceso.service';
import { OAuthProviderService } from '../../core/services/oauth-provider.service';

type EstadoInvitacion = 'cargando' | 'autenticacion' | 'enviando' | 'aceptada' | 'error';

@Component({
  selector: 'app-invitacion-aceptar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <main class="invitation-shell">
      <section class="sm-card invitation-card" aria-live="polite">
        <p class="sm-eyebrow">SchoolManager · Invitación de acceso</p>
        <h1 class="sm-page-title">Confirmar identidad</h1>

        @if (estado === 'cargando' || estado === 'enviando') {
          <div class="sm-state">
            <span class="sm-spinner" aria-hidden="true"></span>
            <p>{{ estado === 'cargando' ? 'Preparando la invitación...' : 'Validando tu identidad...' }}</p>
          </div>
        }

        @if (estado === 'autenticacion') {
          <p class="sm-helper">
            Para demostrar que esta identidad te pertenece, inicia sesión con tu proveedor.
            SchoolManager no vinculará la cuenta únicamente por coincidencia de correo.
          </p>
          <div class="actions">
            <button type="button" class="sm-btn sm-btn--primary"
              (click)="continuarConGoogle()" [disabled]="proveedorCargando !== null">
              {{ proveedorCargando === 'google' ? 'Abriendo Google...' : 'Continuar con Google' }}
            </button>
            <button type="button" class="sm-btn sm-btn--secondary"
              (click)="continuarConMicrosoft()" [disabled]="proveedorCargando !== null">
              {{ proveedorCargando === 'microsoft' ? 'Abriendo Microsoft...' : 'Continuar con Microsoft' }}
            </button>
          </div>
        }

        @if (estado === 'aceptada') {
          <div class="sm-alert sm-alert--success" role="status">
            Tu identidad fue verificada. La solicitud quedó pendiente de aprobación por un administrador de la institución.
          </div>
          <p class="sm-helper">
            Cuando sea aprobada, podrás volver a iniciar sesión y SchoolManager cargará tus roles y permisos.
          </p>
        }

        @if (estado === 'error') {
          <div class="sm-alert sm-alert--error" role="alert">{{ mensaje }}</div>
          <p class="sm-helper">
            Si el enlace expiró o ya fue utilizado, solicita una nueva invitación a la institución.
          </p>
        }
      </section>
    </main>
  `,
  styles: [`
    .invitation-shell {
      min-height: 100vh;
      display: grid;
      place-items: center;
      padding: 24px;
      background: var(--sm-color-bg, #f4f7fb);
    }

    .invitation-card {
      width: min(100%, 620px);
      display: grid;
      gap: 18px;
    }

    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
    }
  `]
})
export class InvitacionAceptar implements OnInit {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);
  private readonly oauth = inject(OAuthProviderService);
  private readonly invitaciones = inject(InvitacionAccesoService);
  private readonly cdr = inject(ChangeDetectorRef);

  estado: EstadoInvitacion = 'cargando';
  mensaje = '';
  proveedorCargando: 'google' | 'microsoft' | null = null;
  private token = '';

  async ngOnInit(): Promise<void> {
    if (!isPlatformBrowser(this.platformId)) return;

    const tokenUrl = this.extraerTokenUrl();
    if (tokenUrl) {
      sessionStorage.setItem(INVITACION_TOKEN_SESSION_KEY, tokenUrl);
      history.replaceState(history.state, '', '/invitacion/aceptar');
    }

    this.token = tokenUrl
      ?? sessionStorage.getItem(INVITACION_TOKEN_SESSION_KEY)
      ?? '';

    if (!this.token) {
      this.estado = 'error';
      this.mensaje = 'El enlace de invitación no contiene un token válido.';
      this.cdr.markForCheck();
      return;
    }

    await this.auth.asegurarUsuarioInicial();

    if (!this.auth.getToken()) {
      this.estado = 'autenticacion';
      this.cdr.markForCheck();
      return;
    }

    await this.aceptar();
  }

  async continuarConGoogle(): Promise<void> {
    await this.iniciarOAuth('google');
  }

  async continuarConMicrosoft(): Promise<void> {
    await this.iniciarOAuth('microsoft');
  }

  private async iniciarOAuth(proveedor: 'google' | 'microsoft'): Promise<void> {
    if (!this.token || this.proveedorCargando) return;

    sessionStorage.setItem(INVITACION_TOKEN_SESSION_KEY, this.token);
    this.proveedorCargando = proveedor;
    this.mensaje = '';
    this.cdr.markForCheck();

    try {
      if (proveedor === 'google') {
        await this.oauth.continuarConGoogle();
      } else {
        await this.oauth.continuarConMicrosoft();
      }
    } catch {
      this.proveedorCargando = null;
      this.estado = 'error';
      this.mensaje = 'No se pudo iniciar la autenticación externa. Intenta nuevamente.';
      this.cdr.markForCheck();
    }
  }

  private async aceptar(): Promise<void> {
    this.estado = 'enviando';
    this.cdr.markForCheck();

    try {
      await this.invitaciones.aceptar(this.token);
      sessionStorage.removeItem(INVITACION_TOKEN_SESSION_KEY);
      this.token = '';
      this.estado = 'aceptada';
      this.mensaje = '';
    } catch (error) {
      if (error instanceof InvitacionAccesoError && error.status === 401) {
        this.estado = 'autenticacion';
        this.mensaje = '';
      } else {
        sessionStorage.removeItem(INVITACION_TOKEN_SESSION_KEY);
        this.estado = 'error';
        this.mensaje = error instanceof Error
          ? error.message
          : 'No se pudo validar la invitación.';
      }
    } finally {
      this.cdr.markForCheck();
    }
  }

  private extraerTokenUrl(): string | null {
    const query = this.route.snapshot.queryParamMap.get('token')?.trim();
    if (query) return query;

    const fragment = window.location.hash.startsWith('#')
      ? window.location.hash.slice(1)
      : window.location.hash;
    const tokenFragmento = new URLSearchParams(fragment).get('token')?.trim();
    return tokenFragmento || null;
  }
}
