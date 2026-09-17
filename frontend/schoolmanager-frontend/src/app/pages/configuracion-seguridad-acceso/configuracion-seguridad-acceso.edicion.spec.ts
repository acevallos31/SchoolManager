import { vi } from 'vitest';
import { ConfiguracionSeguridadAcceso } from './configuracion-seguridad-acceso';
import { SeguridadAccesoError, UsuarioSeguridad } from '../../core/services/seguridad-acceso.service';

describe('ConfiguracionSeguridadAcceso edición de usuarios', () => {
  const usuarioEditable: UsuarioSeguridad = {
    id: 'usuario-1',
    nombre: 'Ana Pérez',
    nombres: 'Ana',
    apellidos: 'Pérez',
    correo: 'ana@example.com',
    activo: true,
    identidadVinculada: true,
    puedeEditar: true,
    roles: []
  };

  function crear() {
    const contexto = {
      institucionActual: vi.fn(() => ({ id: 'inst-1', nombre: 'Colegio Alfa' }))
    };
    const seguridad = {
      editarUsuario: vi.fn().mockResolvedValue(undefined),
      obtener: vi.fn().mockResolvedValue({
        institucionId: 'inst-1',
        capacidades: {
          rolesVer: true,
          rolesCrear: true,
          rolesEditar: true,
          rolesAsignarPermisos: true,
          usuariosVer: true,
          usuariosAsignarRoles: true
        },
        roles: [],
        plantillas: [],
        permisosDelegables: [],
        asignaciones: []
      }),
      obtenerUsuarios: vi.fn().mockResolvedValue([usuarioEditable])
    };
    const router = { navigate: vi.fn().mockResolvedValue(true) };
    const cdr = { detectChanges: vi.fn() };

    const component = new ConfiguracionSeguridadAcceso(
      contexto as any,
      seguridad as any,
      router as any,
      cdr as any
    );
    component.snapshot = {
      institucionId: 'inst-1',
      capacidades: {
        rolesVer: true,
        rolesCrear: true,
        rolesEditar: true,
        rolesAsignarPermisos: true,
        usuariosVer: true,
        usuariosAsignarRoles: true
      },
      roles: [],
      plantillas: [],
      permisosDelegables: [],
      asignaciones: []
    };
    component.usuarios = [usuarioEditable];

    return { component, contexto, seguridad, cdr };
  }

  it('abre y cancela la edición usando los datos internos de personas', () => {
    const { component } = crear();

    component.editarUsuario(usuarioEditable);
    expect(component.usuarioEditandoId).toBe('usuario-1');
    expect(component.edicionUsuario).toEqual({
      nombres: 'Ana',
      apellidos: 'Pérez',
      correo: 'ana@example.com'
    });

    component.cancelarEdicionUsuario();
    expect(component.usuarioEditandoId).toBe('');
    expect(component.edicionUsuario).toEqual({ nombres: '', apellidos: '', correo: '' });
  });

  it('no abre edición si el usuario no es editable o hay guardado en curso', () => {
    const { component } = crear();

    component.editarUsuario({ ...usuarioEditable, puedeEditar: false });
    expect(component.usuarioEditandoId).toBe('');

    component.guardando = true;
    component.editarUsuario(usuarioEditable);
    expect(component.usuarioEditandoId).toBe('');
  });

  it('valida nombres y apellidos antes de guardar', async () => {
    const { component, seguridad } = crear();
    component.editarUsuario(usuarioEditable);
    component.edicionUsuario = { nombres: ' ', apellidos: 'Pérez', correo: 'ana@example.com' };

    await component.guardarUsuario();

    expect(seguridad.editarUsuario).not.toHaveBeenCalled();
    expect(component.esError).toBe(true);
    expect(component.mensaje).toContain('Nombres y apellidos son obligatorios');
  });

  it('normaliza datos, guarda y no toca la identidad OAuth', async () => {
    const { component, seguridad } = crear();
    component.editarUsuario(usuarioEditable);
    component.edicionUsuario = {
      nombres: ' Ana María ',
      apellidos: ' Pérez López ',
      correo: ' ANA.NUEVA@EXAMPLE.COM '
    };

    await component.guardarUsuario();

    expect(seguridad.editarUsuario).toHaveBeenCalledWith('usuario-1', {
      institucionId: 'inst-1',
      nombres: 'Ana María',
      apellidos: 'Pérez López',
      correo: 'ana.nueva@example.com'
    });
    expect(component.usuarioEditandoId).toBe('');
    expect(component.esError).toBe(false);
    expect(component.mensaje).toContain('identidad Google/Microsoft no fue modificada');
    expect(component.guardando).toBe(false);
  });

  it('convierte correo vacío a null', async () => {
    const { component, seguridad } = crear();
    component.editarUsuario(usuarioEditable);
    component.edicionUsuario = { nombres: 'Ana', apellidos: 'Pérez', correo: '   ' };

    await component.guardarUsuario();

    expect(seguridad.editarUsuario).toHaveBeenCalledWith('usuario-1', {
      institucionId: 'inst-1',
      nombres: 'Ana',
      apellidos: 'Pérez',
      correo: null
    });
  });

  it('no guarda sin institución, usuario editable o durante otra operación', async () => {
    const { component, contexto, seguridad } = crear();
    component.usuarioEditandoId = 'usuario-1';

    contexto.institucionActual.mockReturnValue(null);
    await component.guardarUsuario();
    expect(seguridad.editarUsuario).not.toHaveBeenCalled();

    contexto.institucionActual.mockReturnValue({ id: 'inst-1', nombre: 'Colegio Alfa' });
    component.usuarios = [{ ...usuarioEditable, puedeEditar: false }];
    await component.guardarUsuario();
    expect(seguridad.editarUsuario).not.toHaveBeenCalled();

    component.usuarios = [usuarioEditable];
    component.guardando = true;
    await component.guardarUsuario();
    expect(seguridad.editarUsuario).not.toHaveBeenCalled();
  });

  it('muestra el error funcional del backend y libera el estado de guardado', async () => {
    const { component, seguridad } = crear();
    seguridad.editarUsuario.mockRejectedValueOnce(new SeguridadAccesoError('No tienes permiso.', 403));
    component.editarUsuario(usuarioEditable);
    component.edicionUsuario = { nombres: 'Ana', apellidos: 'Pérez', correo: 'ana@example.com' };

    await component.guardarUsuario();

    expect(component.esError).toBe(true);
    expect(component.mensaje).toBe('No tienes permiso.');
    expect(component.guardando).toBe(false);
  });
});
