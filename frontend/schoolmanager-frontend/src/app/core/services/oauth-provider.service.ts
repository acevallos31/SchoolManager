import { inject, Injectable } from '@angular/core';
import { AuthService } from './auth';

@Injectable({ providedIn: 'root' })
export class OAuthProviderService {
  private readonly auth = inject(AuthService);

  async continuarConGoogle(): Promise<void> {
    await this.auth.loginWithGoogle();
  }

  async continuarConMicrosoft(): Promise<void> {
    await this.auth.loginWithMicrosoft();
  }
}
