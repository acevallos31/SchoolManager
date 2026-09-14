import { TestBed } from '@angular/core/testing';
import { SupabaseClient } from '@supabase/supabase-js';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { SUPABASE_CLIENT } from './auth';
import { OAuthProviderService } from './oauth-provider.service';

describe('OAuthProviderService', () => {
  let service: OAuthProviderService;
  let signInWithOAuth: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    signInWithOAuth = vi.fn().mockResolvedValue({ data: { provider: null, url: null }, error: null });
    const supabase = {
      auth: { signInWithOAuth }
    } as unknown as SupabaseClient;

    TestBed.configureTestingModule({
      providers: [OAuthProviderService, { provide: SUPABASE_CLIENT, useValue: supabase }]
    });
    service = TestBed.inject(OAuthProviderService);
  });

  it('Google fuerza selector de cuenta y conserva callback', async () => {
    await service.continuarConGoogle();

    expect(signInWithOAuth).toHaveBeenCalledWith({
      provider: 'google',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`,
        queryParams: { prompt: 'select_account' }
      }
    });
  });

  it('Microsoft usa Azure, scope email y selector de cuenta', async () => {
    await service.continuarConMicrosoft();

    expect(signInWithOAuth).toHaveBeenCalledWith({
      provider: 'azure',
      options: {
        redirectTo: `${window.location.origin}/auth/callback`,
        scopes: 'email',
        queryParams: { prompt: 'select_account' }
      }
    });
  });
});
