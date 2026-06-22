import { Injectable } from '@angular/core';
import { createClient, SupabaseClient, Session } from '@supabase/supabase-js';
import { BehaviorSubject } from 'rxjs';

const SUPABASE_URL = 'https://nphvszugtwumeeegvahu.supabase.co';
const SUPABASE_KEY = 'sb_publishable_MYmRr445RYIKhQ6JK6Gv4Q_Q68L2Eas';

@Injectable({ providedIn: 'root' })
export class AuthService {
  public supabase: SupabaseClient;
  private sessionSubject = new BehaviorSubject<Session | null>(null);
  session$ = this.sessionSubject.asObservable();

  constructor() {
    this.supabase = createClient(SUPABASE_URL, SUPABASE_KEY, {
      auth: {
        persistSession: true,
        storageKey: 'schoolmanager-auth',
        storage: window.localStorage
      }
    });

    this.supabase.auth.getSession().then(({ data }) => {
      this.sessionSubject.next(data.session);
    });

    this.supabase.auth.onAuthStateChange((_, session) => {
      this.sessionSubject.next(session);
    });
  }

  async login(correo: string, password: string) {
    const { data, error } = await this.supabase.auth.signInWithPassword({
      email: correo,
      password
    });
    if (error) throw error;
    return data;
  }

  async logout() {
    await this.supabase.auth.signOut();
  }

  isLoggedIn(): boolean {
    return !!this.sessionSubject.value;
  }

  getToken(): string | null {
    return this.sessionSubject.value?.access_token ?? null;
  }

  async getUsuarioActual() {
    const session = this.sessionSubject.value;
    if (!session) return null;

    const { data, error } = await this.supabase
      .from('usuarios')
      .select('*')
      .eq('supabase_uid', session.user.id)
      .single();

    if (error) {
      console.error('Error obteniendo usuario:', error);
      return null;
    }
    return data;
  }
}