# Migración Fase 1C

## Alcance

Fase 1C incorpora RBAC multirol, normaliza secciones y matrículas, agrega
histórico de estados y garantías ACID, y protege Supabase mediante RLS y RPC.
La contracción final elimina `usuarios.rol`; la autoridad queda en
`usuarios_roles -> roles -> roles_permisos -> permisos`.

## Rutas de instalación

- Instalación nueva: ejecutar solamente
  `database/baseline/001_schoolmanager_fase1a.sql`. El nombre se conserva por
  compatibilidad, pero su contenido es el baseline consolidado Fase 1C.
- Instalación existente compatible: aplicar 007, 008, 009 y 010, en ese orden.
  Las migraciones legacy 001–006 no se vuelven a ejecutar.

## Prechecks

1. Crear backup verificable y registrar el commit desplegado.
2. Confirmar que 001–006 ya están aplicadas y que no hay una migración parcial.
3. Ejecutar las consultas de validación previas de cada bloque.
4. Confirmar que todo `usuarios.rol` tiene código conocido.
5. Confirmar que `auth_user_id` es único y que no hay usuarios activos sin la
   asignación que requieren.
6. Verificar `auth.uid()`, `anon`, `authenticated` y `service_role` en Supabase.

## Despliegue expand/contract

1. Backup.
2. Aplicar 007 y ejecutar `validation/007_*`.
3. Aplicar 008 y ejecutar `validation/008_*`.
4. Aplicar 009 y ejecutar `validation/009_*`.
5. Desplegar backend y frontend compatibles con `roles`/`permisos` mientras
   `usuarios.rol` todavía existe.
6. Probar login y `GET /api/auth/me`; confirmar que no aparece `rol` singular.
7. Aplicar 010 y ejecutar `validation/010_*`.
8. Ejecutar smoke tests de RLS, RPC autorizada y matrícula controlada.

010 debe ir después del backend compatible. Aplicarla antes rompería el
`UsuarioActualService` anterior y produciría downtime evitable.

## Rollback

- 009 restaura funciones previas y deja los grants cliente en modo seguro; no
  puede reconstruir grants externos desconocidos.
- 010 reconstruye `usuarios.rol` solo si cada usuario tiene exactamente un rol
  global activo compatible. Multirol, rol institucional, cero roles o roles no
  legacy hacen abortar el rollback.
- No retroceder 008 si existen datos académicos cuyo significado se perdería.

Si 010 falla, la transacción no elimina la columna. Se corrigen las asignaciones
RBAC y se reintenta; nunca se fuerza un rol arbitrario.

## Validación posterior

- Compilar backend y frontend y ejecutar suites API/DB.
- Confirmar 007–010 en `schema_migrations`.
- Confirmar RLS, grants, `search_path` y ausencia de DML directo cliente.
- Confirmar aislamiento institucional, usuarios/roles inactivos y responsables.
- Probar RPC de matrícula y su histórico en la misma transacción.

No se almacenan secretos PostgreSQL ni `service_role` en Angular o Expo.
