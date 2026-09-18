# HANDOFF — 049C: clonación transaccional + API Demo

## Fecha

2026-09-18 (UTC-6).

## Rama

`feature/049c-demo-clonacion-api`

## Dependencias cerradas

- PR #121 / 049A-049B integrado en `main`.
- Migración 046 integrada en código.
- 046 **no se ha aplicado manualmente en Supabase producción** desde este bloque.

## Objetivo 049C

Construir el backend de una Demo pública aislada sin habilitarla todavía:

1. clonación transaccional de `demo_template` a `demo_sandbox`;
2. identidad interna ligada al Auth UID anónimo;
3. rol institucional funcional sin permisos de identidad/plataforma;
4. crear/reutilizar/resetear sesión;
5. API protegida por feature flag y claim `is_anonymous`;
6. rate limiting previo a 049D.

## Migración 047

Archivo:

`database/migrations/047_demo_sandbox_clonacion.sql`

### Plantilla `demo_operator`

Nueva plantilla global protegida.

Recibe permisos institucionales vigentes excepto:

- `identidad.*`;
- `configuracion.sistema.editar`;
- cualquier permiso de ámbito plataforma.

La plantilla no se asigna directamente. Cada sandbox recibe un rol institucional
`demo_operator` propio.

### RPC internas

- `public.rpc_crear_sandbox_demo(auth_user_id, plantilla_id)`
- `public.rpc_reset_sandbox_demo(auth_user_id, plantilla_id)`

Ambas son `SECURITY DEFINER`, con `search_path` endurecido y revocadas a
`public`, `anon` y `authenticated`. Solo `service_role` conserva EXECUTE
explícito.

El navegador nunca invoca estas RPC.

## Clonación

La operación es una sola transacción.

Se clonan/remapean:

- institución;
- configuración de identificadores;
- ciclo y período;
- grados, jornadas, secciones;
- conceptos;
- planes y cuotas;
- personas de alumnos/responsables;
- alumnos;
- responsables;
- vínculos;
- matrículas;
- cargos;
- pagos;
- aplicaciones.

No se clonan actores del template ni Auth users.

### Unicidades globales

- documento Persona: se regenera como `DEMO-...`;
- RNE: se regenera como `DEMO-...`;
- número de recibo: lo genera la secuencia del destino;
- referencia externa de pago: queda nula.

`codigo_interno` se conserva porque es único por institución.

## Usuario Demo

La primera creación para un Auth UID:

- crea Persona técnica `Visitante Demo`;
- crea `public.usuarios` vinculado al Auth UID;
- crea rol institucional `demo_operator`;
- lo asigna solo a la nueva sandbox.

Reutilizar la sesión devuelve la misma sandbox mientras siga vigente.

Reset/expiración:

- cierra sesión previa;
- retira el rol activo de la sandbox anterior;
- desactiva la institución anterior;
- crea una sandbox limpia.

## API

Archivos:

- `Services/DemoOptions.cs`;
- `Services/DemoSandboxService.cs`;
- `Controllers/DemoController.cs`.

Endpoints:

- `POST /api/demo/session`;
- `POST /api/demo/session/reset`.

Guardas:

1. JWT válido;
2. `Demo:Enabled=true`;
3. `Demo:TemplateInstitutionId` configurado;
4. claim `is_anonymous=true`;
5. rate limit por IP.

Configuración base:

```json
"Demo": {
  "Enabled": false
}
```

Producción permanece apagada por defecto.

## Rate limiting

Política `demo-session`:

- 12 requests/hora por IP;
- sin cola;
- 429 al exceder.

CAPTCHA/Turnstile se implementará en 049D junto al flujo frontend de Anonymous Sign-In.

## Pruebas agregadas

### DB

`DemoSandboxClonacionTests.cs`

Cubre:

- dos visitantes desde el mismo template;
- aislamiento de institución;
- RNE/documentos regenerados;
- recibos distintos;
- rol solo en sandbox propia;
- create idempotente;
- reset con sandbox limpia;
- cierre de rol/sandbox anterior;
- RPC no ejecutable por `authenticated`.

### API

`DemoControllerTests.cs`

Cubre:

- 401 sin JWT;
- 403 para usuario no anónimo;
- 200 para identidad anónima;
- reset;
- feature flag apagado -> 404.

## No realizado

- no aplicar 046/047 en Supabase producción;
- no habilitar Anonymous Sign-In;
- no crear proyecto Supabase Demo;
- no crear seed Demo real todavía;
- no frontend `Probar Demo`;
- no cleanup físico;
- no merge automático.

## Siguiente bloque

**049D — infraestructura Demo + frontend + E2E + cleanup**

Antes de publicar:

- proyecto Supabase Demo separado;
- aplicar migraciones allí;
- crear `demo_template` sintética;
- habilitar Anonymous Sign-In solo allí;
- Turnstile;
- deployment API/Angular Demo;
- `Demo__Enabled=true`;
- `Demo__TemplateInstitutionId=<UUID template>`;
- prueba E2E con dos browsers simultáneos;
- cleanup de sandboxes y usuarios Auth anónimos.
