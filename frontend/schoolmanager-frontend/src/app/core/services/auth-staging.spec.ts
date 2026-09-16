import { TestBed } from '@angular/core/testing';
import { Session, SupabaseClient } from '@supabase/supabase-js';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../environments/environment';
import { AuthService, SUPABASE_CLIENT } from './auth';

type StagingAwareEnvironment = typeof environment & { edgeSessionEnabled?: boolean };
const stagingEnvironment = environment as StagingAwareEnvironment;

describe('AuthService en staging local', () => {
  const originalEdgeSessionEnabled = stagingEnvironment.edgeSessionEnabled;
  const session = {
    access_token: 'token-staging-local',
    user: { id: 'auth-e2e-local' }
  } as unknown as Session;

  beforeEach(() => {
    stagingEnvironment.edgeSessionEnabled = false;
    const supabase = {
      auth: {
        onAuthStateChange: vi.fn().mockReturnValue({
          data: { subscription: { unsubscribe: vi.fn() } }
        }),
        signInWithPassword: vi.fn().mockResolvedValue({
          data: { session, user: session.user },
          error: null
        }),
        signOut: vi.fn().mockResolvedValue({ error: null })
      }
    } as unknown as SupabaseClient;

    TestBed.configureTestingModule({
      providers: [AuthService, { provide: SUPABASE_CLIENT, useValue: supabase }]
    });
  });

  afterEach(() => {
    if (originalEdgeSessionEnabled === undefined) {
      delete stagingEnvironment.edgeSessionEnabled;
    } else {
      stagingEnvironment.edgeSessionEnabled = originalEdgeSessionEnabled;
    }
    vi.restoreAllMocks();
    TestBed.resetTestingModule();
  });

  it('autentica contra Supabase + API sin llamar la función Vercel de sesión', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      new Response(
        JSON.stringify({
          id: 'usuario-e2e-local',
          personaId: 'persona-e2e-local',
          roles: ['e2e_school_admin'],
          permisos: ['academico.alumnos.ver']
        }),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    const service = TestBed.inject(AuthService);

    await expect(service.login('admin.e2e@schoolmanager.test', 'clave-local')).resolves.toMatchObject({
      id: 'usuario-e2e-local'
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(String(fetchMock.mock.calls[0][0])).toContain('/auth/me');
    expect(fetchMock.mock.calls.some(call => String(call[0]) === '/api/auth/session')).toBe(false);
  });
});
