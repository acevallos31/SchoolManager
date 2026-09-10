import { Injectable } from '@angular/core';
import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandler,
  HttpInterceptor,
  HttpRequest
} from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { DebugStateService } from '../services/debug-state.service';

@Injectable()
export class DebugInterceptor implements HttpInterceptor {
  constructor(private readonly debugState: DebugStateService) {}

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && this.isRecord(error.error)) {
          const debug = error.error['debug'];
          if (debug) this.debugState.captureDiagnostic(debug);
        }
        return throwError(() => error);
      })
    );
  }

  private isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
  }
}
