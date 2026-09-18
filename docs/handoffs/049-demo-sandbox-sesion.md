# HANDOFF — Bloque 049: Demo pública aislada por sesión

## Fecha

2026-09-18 (UTC-6).

## Estado

- Rama: `feature/demo-sandbox-049`.
- Fase actual: **049A — contrato/arquitectura**.
- Base: `main`.
- No hay migración nueva todavía.
- No se ha modificado producción.
- El PR #119 / 048 mantiene pendiente la migración 045; 049 no debe generar conflicto
  de numeración ni depender de DDL no integrado.

## Decisión aprobada

SchoolManager usará **sandbox por sesión Demo** en lugar de un dataset mutable
compartido.

Cada visitante obtiene una institución temporal aislada, clonada desde una plantilla
Demo protegida. Puede probar flujos académicos y financieros reales sin afectar a otros
visitantes.

Documento canónico de la decisión:

`docs/decisiones/049-demo-sandbox-por-sesion.md`

## Objetivo técnico inmediato

Cerrar 049A y preparar 049B sin tocar todavía el esquema:

1. inventariar qué tablas/RPC deben clonarse para un dataset coherente;
2. identificar orden de dependencias/FK;
3. definir el límite entre datos clonables y configuración global no clonable;
4. preparar casos de prueba de aislamiento/concurrencia;
5. esperar que 045 quede integrada en `main` antes de numerar la migración Demo.

## Guardrails

- No tocar OAuth Google/Microsoft.
- No auto-link por correo.
- No usar `service_role` en navegador.
- No ejecutar DDL ni escribir datos en producción.
- No permitir que Demo llegue a `/acceso-pendiente` como flujo normal.
- No crear un bypass de API/RPC para Demo.
- No hacer merge automático a `main`.
- No modificar el núcleo financiero para implementar aislamiento: Demo reutiliza las
  mismas operaciones de cargos/pagos bajo su propio `institucion_id`.

## Próximo checkpoint de implementación

**049B — persistencia y clonación**, una vez 048/045 esté integrado.

Entregables previstos:

- persistencia de `demo_sessions`;
- clasificación segura `DEMO_TEMPLATE` / `DEMO_SANDBOX`;
- clonación transaccional;
- expiración;
- validation/rollback;
- tests DB/API que demuestren que Demo A y Demo B no comparten alumnos, responsables,
  matrículas, cargos ni pagos.
