# Bloque 042 — Estrategia de entorno Demo aislado

Estado: decisión de arquitectura documentada; implementación de infraestructura pendiente de un bloque propio.

## Objetivo

Permitir demostrar SchoolManager con datos ficticios y navegación amplia sin exponer, mezclar ni modificar información de instituciones reales.

## Decisión

Se separan dos conceptos:

1. **`demo_viewer`**: plantilla/rol institucional real de solo lectura. Puede utilizarse dentro de una institución real cuando se necesite acceso controlado de demostración o auditoría.
2. **Entorno Demo público/comercial**: instalación aislada de SchoolManager con infraestructura y datos propios. No debe reutilizar la base de datos productiva.

## Topología recomendada

El entorno Demo debe tener, como mínimo:

- frontend desplegado en un dominio o deployment dedicado, por ejemplo `demo.schoolmanager...`;
- API .NET apuntando exclusivamente al proyecto Demo;
- proyecto Supabase independiente del productivo;
- datos completamente ficticios;
- usuario(s) demo preconfigurados;
- configuración OAuth propia del entorno si se habilita Google;
- política de reinicio/reset periódico de datos.

Nunca debe compartir `DATABASE_URL`, service role, proyecto Supabase ni datos con producción.

## Datos semilla

El seed Demo debería representar un ciclo escolar pequeño pero completo:

- institución ficticia;
- ciclos y períodos de matrícula;
- grados, jornadas y secciones;
- alumnos y responsables ficticios;
- matrículas;
- conceptos financieros, cargos y pagos ficticios;
- varios roles institucionales de ejemplo;
- usuario demo de solo lectura y, si se requiere para demostraciones internas, otro usuario demo administrativo controlado.

No usar nombres, correos, documentos, teléfonos ni información real de estudiantes o responsables.

## Acceso del usuario Demo

Un usuario Demo comercial no debe depender de una asignación dentro de la base productiva. Su identidad y roles deben existir únicamente en el Supabase Demo.

Para una demostración pública se recomienda privilegiar lectura y flujos seguros. Las operaciones de escritura que se permitan deben ser descartables porque el entorno se restablecerá desde el seed.

## Reset

El entorno debe poder volver a un estado conocido mediante un proceso automatizable e idempotente:

1. limpiar datos funcionales Demo;
2. reaplicar seed conocido;
3. restaurar usuarios/roles Demo requeridos;
4. ejecutar validaciones básicas;
5. publicar/confirmar disponibilidad.

La frecuencia puede ser programada posteriormente (por ejemplo diaria o después de una sesión comercial), pero no forma parte del PR 042.

## Seguridad

- no copiar backups productivos al entorno Demo;
- no reutilizar secretos productivos;
- no otorgar `platform_admin` público;
- mantener RLS/RPC y autorización .NET activas también en Demo;
- registrar auditoría igual que en producción;
- si el Demo es público, aplicar rate limiting y límites de uso apropiados.

## Fuera de alcance del PR 042

Este PR deja documentada la arquitectura y conserva `demo_viewer` como plantilla institucional. Crear el proyecto Supabase Demo, sus secretos, dominio, seed/reset y pipeline de despliegue debe hacerse en un bloque independiente para no mezclar el cierre del RBAC productivo con nueva infraestructura.
