import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { AuthService } from '../../core/services/auth';
import { AccesoPendiente } from './acceso-pendiente';

describe('AccesoPendiente', () => {
  it('cierra la sesión válida sin pantalla y vuelve al login', async () => {
    const logout = vi.fn().mockResolvedValue(undefined);
    await TestBed.configureTestingModule({
      imports: [AccesoPendiente],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: { logout } }
      ]
    }).compileComponents();

    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AccesoPendiente);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('todavía no tiene una pantalla habilitada');
    await fixture.componentInstance.cerrarSesion();

    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
