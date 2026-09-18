# HANDOFF — Bloque 049: Demo pública aislada por sesión

## Fecha

2026-09-18 (UTC-6).

## Estado

- Rama: `feature/demo-sandbox-049`.
- 049A: **cerrada**.
- 049B: **en implementación**.
- Base: `main`.
- Migración propuesta: `046_demo_sandbox_sesiones.sql`.
- Sin DDL ni escrituras de datos realizadas en producción.

## Decisión vigente

049 complementa `docs/handoffs/042-entorno-demo-aislado.md`:

**entorno Demo separado de producción + sandbox institucional por visitante dentro del
entorno Demo**.

No se autoriza una Demo pública sobre la base productiva.

Documento canónico:

`docs/decisiones/049-demo-sandbox-por-sesion.md`

## Verificaciones realizadas

- `045_operacion_vinculacion_identidad_autorizada.sql` está en `main`.
- 045 aparece aplicada en Supabase producción.
- Siguiente versión disponible del runner: 046.
- `personas` y `usuarios` son globales: deben clonarse/crearse por sandbox.
- `alumnos.rne` y la identificación normalizada de Persona tienen unicidad global:
  no copiar literalmente.
- `pagos.numero_recibo` es global: al clonar debe generarse uno nuevo.
- `codigo_interno` de alumno es único por institución y puede reutilizarse al clonar.
- `demo_viewer` existe pero es solo lectura; una Demo funcional necesita un rol
  institucional acotado, nunca `platform_admin`.

## 049B agregado en la rama

- `database/migrations/046_demo_sandbox_sesiones.sql`;
- validation;
- rollback;
- `DemoSandboxTests.cs`;
- catálogo de migraciones actualizado a 001→046.

La 046:

- agrega `instituciones.tipo` con default `normal`;
- agrega `demo_sessions`;
- exige `demo_template` + `demo_sandbox` activas;
- limita una sesión activa por identidad;
- protege cambio de tipo/desactivación mientras existan sesiones;
- habilita RLS;
- revoca acceso directo a `anon` y `authenticated`;
- no crea seed, Auth users, endpoints ni permisos públicos.

## Identidad Demo

Estrategia elegida para 049C:

- Supabase Anonymous Sign-In en el **proyecto Demo**;
- un Auth UID por navegador;
- validar `is_anonymous`;
- CAPTCHA/Turnstile + rate limit;
- sin tocar OAuth Google/Microsoft;
- sin service role en navegador.

Anonymous Sign-In todavía NO fue habilitado.

## Paralelo para Hermes

Prompt preparado:

`docs/agent-prompts/047b-hermes-documentos-financieros.md`

Alcance: documentos financieros de consulta (estado de cuenta / detalle imprimible)
reutilizando 047A, sin modificar invariantes 021 ni Demo 049.

## Próximos pasos

1. validar la 046 con DB Integration/CI;
2. corregir cualquier fallo sin debilitar los guards;
3. completar inventario de clonado;
4. implementar 049C en un checkpoint separado;
5. no aplicar 046 a producción ni crear infraestructura Demo sin revisión explícita.

## Nota de seguridad observada

Supabase Advisor marca `public.schema_migrations` por RLS deshabilitada. Verificación
read-only confirmó que `anon` y `authenticated` no tienen SELECT/INSERT/UPDATE/DELETE
sobre esa tabla. No se modifica dentro de 049 para evitar mezclar hardening ajeno al
alcance; queda como hallazgo para revisión separada.

## Guardrails

- no producción;
- no merge automático;
- no secretos;
- no compartir DB/credenciales con producción;
- no `platform_admin` para Demo;
- no lógica financiera duplicada;
- no acceso directo de negocio desde Angular a Supabase.
