import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth';
import { OAuthProviderService } from './oauth-provider.service';

describe('OAuthProviderService', () => {
  let service: OAuthProviderService;
  let loginWithGoogle: ReturnType<typeof vi.fn>;
  let loginWithMicrosoft: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    loginWithGoogle = vi.fn().mockResolvedValue(undefined);
    loginWithMicrosoft = vi.fn().mockResolvedValue(undefined);

    TestBed.configureTestingModule({
      providers: [
        OAuthProviderService,
        {
          provide: AuthService,
          useValue: { loginWithGoogle, loginWithMicrosoft }
        }
      ]
    });
    service = TestBed.inject(OAuthProviderService);
  });

  it('delega Google en la frontera central de autenticación', async () => {
    await service.continuarConGoogle();
    expect(loginWithGoogle).toHaveBeenCalledOnce();
  });

  it('delega Microsoft en la frontera central de autenticación', async () => {
    await service.continuarConMicrosoft();
    expect(loginWithMicrosoft).toHaveBeenCalledOnce();
  });
});
