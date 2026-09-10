# ADR-002: Usar Supabase Auth con JWT y autorización por permisos en la API

## Contexto

SchoolManager necesita identificar a cada usuario y limitar lo que puede hacer
según su responsabilidad dentro del sistema. Un administrador, un operador y un
padre o responsable no deben tener el mismo acceso. Además, la autenticación no
debe depender de contraseñas almacenadas o validadas directamente por la API de
SchoolManager.

También se requiere que la autorización pueda crecer con nuevos módulos sin
llenar el código de condicionales como `if rol == admin`.

## Decisión

Usar **Supabase Auth** para el inicio de sesión y la emisión de tokens JWT. La
SPA Angular realiza el login con Supabase Auth y envía el `access_token` a la
API ASP.NET Core como `Authorization: Bearer <JWT>`.

La API valida el JWT y aplica autorización mediante políticas basadas en
permisos. Los permisos se almacenan en el modelo RBAC de SchoolManager y usan
códigos por recurso y acción, por ejemplo `academico.matriculas.crear`.

La lógica de negocio permanece detrás de la API. El frontend puede ocultar o
mostrar opciones según permisos, pero la decisión de seguridad final siempre se
verifica en el backend y en las reglas de datos correspondientes.

## Consecuencias

### Positivas

- SchoolManager no implementa ni almacena por su cuenta el mecanismo de login.
- Los tokens JWT permiten autenticar las peticiones a la API sin mantener una
  sesión de servidor tradicional.
- Los permisos permiten un control más fino que depender únicamente del nombre
  de un rol.
- Es posible agregar nuevos módulos y permisos sin rediseñar el mecanismo de
  autenticación.
- La API mantiene la responsabilidad final de autorizar las operaciones de
  negocio.

### Negativas

- El inicio de sesión depende de la disponibilidad de Supabase Auth.
- La configuración del issuer, audience y validación JWT debe mantenerse
  correctamente en cada ambiente.
- El sistema debe mantener sincronizado su modelo interno de usuarios, roles y
  permisos con la identidad emitida por Supabase Auth.
- La autorización requiere más configuración inicial que una comprobación de
  roles simple.

## Estado

Aceptada.
