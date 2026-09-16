import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { vi } from 'vitest';
import { AuthService, InstitucionAcceso, UsuarioActual } from '../../core/services/auth';
import { ContextoInstitucionService } from '../../core/services/contexto-institucion.service';
import { AppShell } from './app-shell';

type UsuarioExtendido = UsuarioActual & {
  nombreCompleto?: string;
  institucionesAdministrables?: InstitucionAcceso[];
};

describe('AppShell', () => {
  let component: AppShell;
  let fixture: ComponentFixture<AppShell>;
  let logout: ReturnType<typeof vi.fn>;
  let navigate: ReturnType<typeof vi.fn>;
  let permisos: Set<string>;
  let usuario$: BehaviorSubject<UsuarioExtendido | null>;
  let session$: BehaviorSubject<any>;
  let contexto$: BehaviorSubject<InstitucionAcceso | null>;
  let seleccionarContexto: ReturnType<typeof vi.fn>;
  let limpiarContexto: ReturnType<typeof vi.fn>;

  const institucionA: InstitucionAcceso = {
    id: 'inst-a', nombre: 'Colegio Alfa', nombreCorto: 'Alfa',
    roles: ['secretaria'], permisos: ['academico.alumnos.ver']
  };
  const institucionB: InstitucionAcceso = {
    id: 'inst-b', nombre: 'Colegio Beta', nombreCorto: 'Beta',
    roles: ['caja'], permisos: ['academico.pagos.ver']
  };
  const institucionInactiva: InstitucionAcceso = {
    id: 'inst-c', nombre: 'Colegio Cerrado', nombreCorto: 'Cerrado',
    roles: [], permisos: [], activo: false
  };
  const usuarioBase: UsuarioExtendido = {
    id: 'u1', personaId: 'p1', nombreCompleto: 'Ana Prueba',
    roles: ['admin'], permisos: [], instituciones: []
  };

  const institucionesContexto = (usuario: UsuarioExtendido | null) => {
    if (!usuario) return [];
    const administrables = usuario.ambitoGlobal?.roles.includes('platform_admin')
      ? usuario.institucionesAdministrables ?? [] : [];
    const porId = new Map<string, InstitucionAcceso>();
    for (const item of administrables) porId.set(item.id, item);
    for (const item of usuario.instituciones ?? []) porId.set(item.id, item);
    return [...porId.values()];
  };

  beforeEach(async () => {
    permisos = new Set(['academico.alumnos.ver']);
    logout = vi.fn().mockResolvedValue(undefined);
    navigate = vi.fn().mockResolvedValue(true);
    usuario$ = new BehaviorSubject<UsuarioExtendido | null>(usuarioBase);
    session$ = new BehaviorSubject<any>({
      user: { email: 'ana@example.com', user_metadata: { full_name: 'Ana Supabase' } }
    });
    contexto$ = new BehaviorSubject<InstitucionAcceso | null>(null);
    seleccionarContexto = vi.fn((id: string) => {
      const institucion = institucionesContexto(usuario$.value)
        .find(item => item.id === id && item.activo !== false);
      if (!institucion) return false;
      contexto$.next(institucion);
      return true;
    });
    limpiarContexto = vi.fn(() => contexto$.next(null));

    await TestBed.configureTestingModule({
      imports: [AppShell],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            tienePermiso: (p: string) => permisos.has(p),
            esSuperadministrador: () => usuario$.value?.ambitoGlobal?.roles.includes('platform_admin') ?? false,
            logout,
            session$: session$.asObservable(),
            usuarioActual$: usuario$.asObservable()
          }
        },
        {
          provide: ContextoInstitucionService,
          useValue: {
            institucionActual$: contexto$.asObservable(),
            institucionesDisponibles: () => institucionesContexto(usuario$.value)
              .filter(item => item.activo !== false),
            institucionesVisibles: () => institucionesContexto(usuario$.value),
            seleccionar: seleccionarContexto,
            limpiar: limpiarContexto
          }
        }
      ]
    }).compileComponents();

    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const recrearComponente = async () => {
    fixture.destroy();
    fixture = TestBed.createComponent(AppShell);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
  };

  it('muestra identidad humana y los enlaces base', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).toContain('SchoolManager');
    expect(texto).toContain('Ana Prueba');
    expect(texto).toContain('(Administrador)');
    expect(texto).toContain('Alumnos');
    expect(texto).toContain('Cerrar sesión');
  });

  it('usa metadata o correo de sesión mientras el backend preview aún no entrega nombreCompleto', () => {
    usuario$.next({ ...usuarioBase, nombreCompleto: undefined });
    fixture.detectChanges();
    expect(component.nombreUsuario).toBe('Ana Supabase');

    session$.next({ user: { email: 'ana@example.com', user_metadata: {} } });
    fixture.detectChanges();
    expect(component.nombreUsuario).toBe('ana@example.com');
  });

  it('oculta módulos sin sus permisos de lectura', () => {
    const texto = fixture.nativeElement.textContent;
    expect(texto).not.toContain('Responsables');
    expect(texto).not.toContain('Matrículas');
    expect(texto).not.toContain('Ciclos escolares');
    expect(texto).not.toContain('Estructura académica');
    expect(texto).not.toContain('Configuración');
  });

  it('muestra módulos con sus permisos', async () => {
    permisos.add('academico.responsables.ver');
    permisos.add('academico.matriculas.ver');
    permisos.add('academico.ciclos.ver');
    permisos.add('academico.estructura.ver');
    await recrearComponente();
    const texto = fixture.nativeElement.textContent;
    expect(texto).toContain('Responsables');
    expect(texto).toContain('Matrículas');
    expect(texto).toContain('Ciclos escolares');
    expect(texto).toContain('Estructura académica');
  });

  it('muestra Configuración a un gestor de roles institucionales', async () => {
    permisos.add('identidad.roles.ver');
    await recrearComponente();
    expect(fixture.nativeElement.textContent).toContain('Configuración');
    expect(component.puedeVerConfiguracion).toBe(true);
  });

  it('muestra Superadministrador por encima del rol legacy y habilita Configuración', () => {
    usuario$.next({
      ...usuarioBase,
      roles: ['admin', 'platform_admin'],
      ambitoGlobal: { roles: ['admin', 'platform_admin'], permisos: [] }
    });
    fixture.detectChanges();
    expect(component.rolVisible).toBe('Superadministrador');
    expect(component.puedeVerConfiguracion).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('(Superadministrador)');
  });

  it('muestra la única institución activa sin exigir selector', () => {
    usuario$.next({ ...usuarioBase, instituciones: [institucionA] });
    contexto$.next(institucionA);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Alfa');
    expect(fixture.nativeElement.querySelector('select[aria-label="Seleccionar institución activa"]')).toBeNull();
    expect(component.requiereSeleccionInstitucion).toBe(false);
  });

  it('Superadministrador usa instituciones administrables sin fabricar membresía', () => {
    usuario$.next({
      ...usuarioBase,
      roles: ['platform_admin'],
      ambitoGlobal: { roles: ['platform_admin'], permisos: [] },
      instituciones: [],
      institucionesAdministrables: [institucionA]
    });
    contexto$.next(institucionA);
    fixture.detectChanges();
    expect(component.instituciones.map(item => item.id)).toEqual(['inst-a']);
    expect(component.institucionActual?.id).toBe('inst-a');
    expect(component.rolVisible).toBe('Superadministrador');
  });

  it('Superadministrador ve instituciones inactivas pero no puede seleccionarlas como contexto', () => {
    usuario$.next({
      ...usuarioBase,
      roles: ['platform_admin'],
      ambitoGlobal: { roles: ['platform_admin'], permisos: [] },
      instituciones: [],
      institucionesAdministrables: [institucionA, institucionInactiva]
    });
    contexto$.next(institucionA);
    fixture.detectChanges();

    const select = fixture.nativeElement.querySelector(
      'select[aria-label="Seleccionar institución activa"]'
    ) as HTMLSelectElement;
    expect(select).not.toBeNull();
    const cerrada = [...select.options].find(option => option.value === 'inst-c');
    expect(cerrada?.disabled).toBe(true);
    expect(cerrada?.textContent).toContain('Inactiva');

    component.seleccionarInstitucion({ target: { value: 'inst-c' } } as unknown as Event);
    expect(seleccionarContexto).toHaveBeenCalledWith('inst-c');
    expect(component.institucionActual?.id).toBe('inst-a');
  });

  it('con varias instituciones muestra selector y exige contexto hasta elegir una', () => {
    usuario$.next({ ...usuarioBase, instituciones: [institucionA, institucionB] });
    contexto$.next(null);
    fixture.detectChanges();
    const select = fixture.nativeElement.querySelector(
      'select[aria-label="Seleccionar institución activa"]'
    ) as HTMLSelectElement;
    expect(select).not.toBeNull();
    expect(component.requiereSeleccionInstitucion).toBe(true);
    component.seleccionarInstitucion({ target: { value: 'inst-b' } } as unknown as Event);
    fixture.detectChanges();
    expect(seleccionarContexto).toHaveBeenCalledWith('inst-b');
    expect(component.institucionActual?.id).toBe('inst-b');
  });

  it('cierra sesión, limpia contexto y navega a login', () => {
    component.logout();
    expect(limpiarContexto).toHaveBeenCalledOnce();
    expect(logout).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('alterna el drawer móvil y filtra enlaces por permiso', () => {
    component.alternarNav();
    expect(component.navAbierta).toBe(true);
    component.cerrarNav();
    expect(component.navAbierta).toBe(false);
    const panel = component.items.find(item => item.ruta === '/dashboard')!;
    const alumnos = component.items.find(item => item.ruta === '/alumnos')!;
    const matriculas = component.items.find(item => item.ruta === '/matriculas')!;
    expect(component.mostrarItem(panel)).toBe(true);
    expect(component.mostrarItem(alumnos)).toBe(true);
    expect(component.mostrarItem(matriculas)).toBe(false);
  });

  it('expone el rol real del usuario autenticado', () => {
    expect(component.roles).toEqual(['admin']);
  });
});
