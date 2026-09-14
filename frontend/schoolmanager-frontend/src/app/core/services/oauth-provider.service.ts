import { inject, Injectable } from '@angular/core';
import { SUPABASE_CLIENT, AuthAppError } from './auth';

type OAuthProveedor = 'google' | 'azure';

@Injectable({ providedIn: 'root' })
export class OAuthProviderService {
  private readonly supabase = inject(SUPABASE_CLIENT);

  async continuarConGoogle(): Promise<void> {
    await this.iniciarOAuth('google', 'Google');
  }

  async continuarConMicrosoft(): Promise<void> {
    await this.iniciarOAuth('azure', 'Microsoft', 'email');
  }

  private async iniciarOAuth(
    provider: OAuthProveedor,
    etiqueta: 'Google' | 'Microsoft',
    scopes?: string
  ): Promise<void> {
    const redirectTo = `${window.location.origin}/auth/callback`;
    const { error } = await this.supabase.auth.signInWithOAuth({
      provider,
      options: {
        redirectTo,
        ...(scopes ? { scopes } : {}),
        queryParams: {
          // Evita reutilizar silenciosamente la última cuenta del navegador.
          prompt: 'select_account'
        }
      }
    });

    if (error) {
      throw new AuthAppError(`No se pudo iniciar el acceso con ${etiqueta}.`, 'UNKNOWN');
    }
  }
}
