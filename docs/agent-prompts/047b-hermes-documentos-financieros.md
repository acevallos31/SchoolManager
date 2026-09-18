# Hermes — paralelo financiero mientras 049 avanza

## Objetivo

Trabajar en un subbloque financiero **independiente de 049** y de bajo riesgo:
continuar la capa documental iniciada en 047A sin reescribir las reglas de cargos/pagos.

## Lectura obligatoria

1. `AGENTS.md`
2. `docs/AI_CONTEXT.md`
3. `docs/engineering-principles.md`
4. `docs/handoffs/047A-documentos-recibo-pago.md`
5. `docs/decisiones/021-pagos-cobranza-fase1-invariantes.md`

Confirma rama, HEAD y árbol limpio antes de editar.

## Alcance autorizado

Crear una rama nueva desde `main` para **047B — documentos financieros de consulta**.

Primero audita qué información autoritativa ya exponen API/RPC para generar, sin nueva
lógica financiera en Angular:

1. **Estado de cuenta del alumno**:
   - datos de institución;
   - alumno;
   - cargos;
   - monto original;
   - aplicado;
   - saldo derivado;
   - vencimiento;
   - estado;
   - totales autoritativos.

2. **Comprobante/detalle de cargo** solo si existe un contrato autoritativo suficiente
   sin inventar reglas nuevas.

La salida debe reutilizar la abstracción documental creada en 047A
(`DocumentoImprimible`, `LienzoPdf`, `ImpresionService`) y permitir **Imprimir** y
**Descargar PDF**.

## Regla principal

El frontend es solo presentación.

- No recalcular saldos financieros en Angular.
- No duplicar reglas de 021.
- No cambiar las invariantes de pagos/cargos.
- No crear tablas ni migraciones salvo que una necesidad real e inequívoca sea
  demostrada; si hace falta DDL/RPC nueva, documenta y DETENTE antes de implementarla.
- Preferir composición backend transaccional de lecturas/RPC existentes, como 047A.

## Fuera de alcance

- Demo/sandbox 049;
- OAuth/autenticación;
- RBAC;
- pasarela de pagos;
- facturación fiscal;
- saldo a favor;
- exportación JSON;
- cambios de producción;
- migraciones en Supabase;
- merge a `main`.

## Validación

Si implementas código:

- backend build;
- API integration tests;
- DB integration tests;
- frontend tests;
- frontend build;
- `git diff --check`;
- Sonar/CI del PR.

Agrega cobertura para permisos, institución incorrecta y contrato documental.

## Cierre esperado

- commits pequeños;
- push normal;
- PR contra `main`;
- actualizar/crear `docs/handoffs/047B-documentos-financieros.md`;
- reportar SHA, PR, pruebas y cualquier bloqueo;
- **NO hacer merge**.
