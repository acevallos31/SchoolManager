# Handoff 044D — dataset académico y aislamiento institucional E2E

## Objetivo

Cerrar la brecha funcional principal restante del E2E autenticado local: validar
permisos negativos y aislamiento real entre instituciones usando Supabase Auth,
PostgreSQL/RLS-RPC, API .NET y Angular, sin tocar producción.

## Alcance

044D extiende la foundation local ya cerrada en 044C. No agrega migraciones ni
cambia reglas de negocio productivas.

### Dataset local determinista

`e2e/support/seed-academic-local.py` se ejecuta después del seed 044C y:

- conserva el rechazo de cualquier Supabase remoto;
- agrega una segunda institución E2E activa;
- crea un segundo administrador institucional basado en `school_admin`;
- habilita `multiples_instituciones=true` únicamente dentro del stack local
  desechable;
- crea para las instituciones A y B un ciclo, período de matrícula, grado,
  jornada, sección, alumno y matrícula activa;
- usa UUID deterministas y correos bajo el TLD reservado `.test`;
- guarda las contraseñas aleatorias solo en `.env.e2e.local`, ignorado por Git.

No crea `platform_admin`, no usa usuarios reales y no comparte datos con
producción.

### Casos E2E

`e2e/tests/academic-isolation.spec.ts` cubre:

1. credenciales incorrectas -> sin sesión;
2. API sin token -> `401`;
3. usuario `demo_viewer` puede leer pero no ve acciones de escritura;
4. `demo_viewer` intentando crear alumno por API -> `403`;
5. admin A puede leer su alumno y no el alumno de B (`404` cross-institución);
6. admin B puede leer su alumno y no el alumno de A (`404` cross-institución);
7. la UI de A muestra su ciclo/grado/sección reales y no filtra el alumno B.

El `404` cross-institución es deliberado: evita confirmar la existencia de un
recurso fuera del contexto autorizado.

## Gate dedicado

`.github/workflows/e2e-044d-academic-isolation-gate.yml` levanta un stack local
desechable con las mismas versiones fijadas por SHA usadas en 044C, ejecuta los
seeds 044C + 044D y corre:

```bash
npm test -- auth.spec.ts academic-isolation.spec.ts
```

El cleanup del stack local se ejecuta siempre, aun si falla un test.

## Guardrails de cierre

044D solo puede considerarse cerrado cuando:

- CI estándar está verde en el HEAD final;
- SonarCloud Quality Gate está verde;
- el gate real 044D termina verde;
- no se versionan credenciales ni `.env.e2e.local`;
- no se ejecutan migraciones ni escrituras en Supabase de producción.

## Qué queda después de 044D

El último checkpoint del bloque E2E será **044E — automatización/hardening**:
workflow reutilizable manual/nightly, artifacts de Playwright en fallo y
actualización de la documentación/deuda técnica para cerrar formalmente la deuda
#13.

Fuera del E2E permanecen las deudas independientes de observabilidad (#5) y
baseline de carga del backend (#14).
