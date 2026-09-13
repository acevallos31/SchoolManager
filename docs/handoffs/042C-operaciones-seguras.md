# 042C — Operaciones seguras de roles

Este corte implementará creación y clonación de roles institucionales, edición de metadata, reemplazo transaccional de permisos, asignación y desactivación de roles, protección del último administrador institucional y auditoría de cambios sensibles. Ninguna migración de este corte se ejecutará en Supabase durante el desarrollo.

Reglas cerradas: un rol institucional pertenece a una sola institución; no puede recibir permisos de plataforma ni permisos no delegables; el actor solo puede delegar permisos que posee efectivamente en ese mismo ámbito; una plantilla nunca se asigna directamente; el último administrador institucional y el último Superadministrador quedan protegidos contra retiro o desactivación.
