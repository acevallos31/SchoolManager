import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface DebugModeStatus {
  habilitado: boolean;
  expiraEn: string | null;
  requestId?: string;
}

export interface DebugDiagnostic {
  requestId: string;
  status: number;
  sqlState?: string | null;
  constraint?: string | null;
  technicalMessage?: string | null;
  endpoint?: string | null;
  source?: string | null;
  timestamp?: string | null;
}

@Injectable({ providedIn: 'root' })
export class DebugStateService {
  private readonly statusSubject = new BehaviorSubject<DebugModeStatus>({
    habilitado: false,
    expiraEn: null
  });
  private readonly diagnosticSubject = new BehaviorSubject<DebugDiagnostic | null>(null);

  readonly status$ = this.statusSubject.asObservable();
  readonly diagnostic$ = this.diagnosticSubject.asObservable();

  get status(): DebugModeStatus {
    return this.statusSubject.value;
  }

  get diagnostic(): DebugDiagnostic | null {
    return this.diagnosticSubject.value;
  }

  setStatus(status: DebugModeStatus): void {
    this.statusSubject.next(status);
  }

  captureDiagnostic(value: unknown): void {
    if (!this.isRecord(value) || typeof value['requestId'] !== 'string' || typeof value['status'] !== 'number') {
      return;
    }
    this.diagnosticSubject.next(value as unknown as DebugDiagnostic);
  }

  clearDiagnostic(): void {
    this.diagnosticSubject.next(null);
  }

  private isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
  }
}
