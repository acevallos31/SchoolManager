import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export const INVITACION_TOKEN_SESSION_KEY = 'schoolmanager-invitacion-token';

export interface InvitacionAceptada {
  invitacionId: string;
  usuarioId: string;
  institucionId: string;
  estado: string;
}

export class InvitacionAccesoError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
    this.name = 'InvitacionAccesoError';
  }
}

@Injectable({ providedIn: 'root' })
export class InvitacionAccesoService {
  private readonly http = inject(HttpClient);

  async aceptar(token: string): Promise<InvitacionAceptada> {
    try {
      return await firstValueFrom(this.http.post<InvitacionAceptada>(
        `${environment.apiUrl}/invitaciones/aceptar`,
        { token }
      ));
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const payload = error.error as { error?: unknown } | null;
        const backend = payload && typeof payload.error === 'string' ? payload.error : null;
        const message = backend ?? (error.status === 401
          ? 'Debes autenticarte para aceptar la invitación.'
          : 'No se pudo validar la invitación.');
        throw new InvitacionAccesoError(message, error.status);
      }
      throw new InvitacionAccesoError('No se pudo validar la invitación.', 0);
    }
  }
}
