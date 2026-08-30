# Configuración de implementación

SchoolManager se distribuye por implementación. La configuración global describe cómo opera esa instalación; una institución representa cada centro educativo administrado dentro de ella.

## Modo institucional

`configuracion_implementacion` es una tabla singleton, con columnas explícitas y tipadas. `multiples_instituciones` es `false` por defecto.

- En modo single debe existir exactamente una institución activa. El contexto la resuelve automáticamente.
- En modo multi no se elige una institución implícita. Cada operación que la necesite debe recibir un contexto seleccionado; la UX de selección se implementará posteriormente.
- `institucion_id` se conserva en las entidades porque define aislamiento, permisos y la capacidad multi-institución futura.

Los módulos no deben inferir la institución desde ciclos, matrículas u otros datos académicos. Deben consumir `rpc_obtener_contexto_implementacion`; en Angular, esa regla está centralizada en `ConfiguracionService`.

## Seguridad

La configuración tiene RLS habilitada y `authenticated` no posee acceso directo a la tabla. La lectura se realiza mediante una RPC que exige usuario activo. La escritura se realiza mediante `rpc_actualizar_multiples_instituciones` y requiere `configuracion.sistema.editar`; el rol `admin` recibe los permisos de configuración.

Las funciones `SECURITY DEFINER` fijan su `search_path`. El helper interno no es ejecutable por `authenticated`.

## Errores de configuración

Las RPC devuelven SQLSTATE estables para que los clientes no dependan del texto:

- `SM001`: falta configuración o no existe institución activa (`CONFIGURATION_REQUIRED`).
- `SM002`: existen varias instituciones activas en modo single.
- `SM003`: reservado para operaciones que requieran una selección explícita en modo multi.

El cambio de multi a single se rechaza si existen varias instituciones activas. Un modo single sin institución puede configurarse, pero las operaciones que requieren contexto responderán `SM001` hasta completar la configuración.

## Alcance pendiente

Este bloque no incluye CRUD de instituciones, selector global, cambio de institución durante una sesión ni configuración académica o financiera.

## Configuración del centro educativo

La migración 013 reutiliza `instituciones`; no crea otra entidad ni agrega campos fiscales sin un consumidor definido. La pantalla administra `nombre`, `nombre_corto`, `direccion`, `telefono`, `correo` y `logo_url` mediante RPC tipadas. No existe eliminación física.

`rpc_obtener_configuracion_institucion(uuid)` devuelve en una operación los datos institucionales y `configuracion_identificadores`. En modo single resuelve la institución activa o devuelve un estado sin institución para permitir configurar la primera. En modo multi exige el UUID explícito y responde `SM003` si falta.

Las escrituras se realizan exclusivamente mediante `rpc_crear_institucion` y `rpc_actualizar_institucion`. Requieren `configuracion.instituciones.editar`, validan el nombre y correo, normalizan textos y mantienen una sola configuración de identificadores por institución. La creación de una segunda institución activa en modo single responde `SM004`.

## Ciclos y períodos de matrícula

La migración 014 reutiliza `ciclos_escolares` y `periodos_matricula`. Se permiten varios ciclos activos para preparar el siguiente año sin cerrar el actual; la vigencia se expresa mediante `activo` y el rango de fechas. Todo período debe quedar dentro de las fechas de su ciclo.

Las lecturas y escrituras usan RPC separadas y permisos `configuracion.ciclos.*` y `configuracion.periodos_matricula.*`. En single la institución se resuelve automáticamente; en multi es obligatoria de forma explícita. No existe eliminación física y la configuración está disponible en `/configuracion/ciclos`.
