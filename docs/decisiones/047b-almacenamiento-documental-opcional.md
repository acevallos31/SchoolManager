# 047B — Almacenamiento documental opcional y desacoplado

## Estado

**Diseño aprobado para implementación posterior.**

Este documento deja sentadas las bases arquitectónicas para el almacenamiento persistente de expedientes y documentos de alumnos sin ampliar el alcance funcional de 047A. El bloque 047A continúa enfocado en documentos generados bajo demanda (recibo, impresión y PDF). La persistencia documental se implementará en un bloque posterior.

## Contexto

SchoolManager tendrá dos categorías distintas de documentos:

1. **Documentos generados bajo demanda**: recibos, reportes, constancias y otros documentos que pueden regenerarse desde datos autoritativos. No requieren almacenamiento permanente por defecto.
2. **Documentos de expediente**: partidas de nacimiento, identificaciones, fichas firmadas, documentos escaneados, anexos y otros archivos que forman parte del expediente histórico del alumno. Estos sí deben conservarse de manera persistente, versionada y auditable.

El almacenamiento documental debe ser **opcional por institución**. SchoolManager debe continuar funcionando aunque ninguna integración de almacenamiento externo esté configurada.

## Decisión arquitectónica

La lógica académica y documental no dependerá directamente de Google Drive, Supabase Storage ni otro proveedor concreto.

Se implementará un puerto de almacenamiento, por ejemplo:

```csharp
public interface IExpedienteStorage
{
    Task<ArchivoAlmacenado> GuardarAsync(DocumentoEntrada documento, CancellationToken ct);
    Task<Stream> AbrirLecturaAsync(string objectId, CancellationToken ct);
    Task EliminarAsync(string objectId, CancellationToken ct);
}
```

Los proveedores serán adaptadores de infraestructura:

- `GoogleSharedDriveStorage` — recomendado para Google Workspace institucional.
- `GooglePersonalDriveStorage` — para instituciones pequeñas que utilicen una cuenta Google individual mediante OAuth.
- `SupabaseStorage` — alternativa futura/opcional.
- Otros proveedores S3-compatible o equivalentes podrán añadirse sin modificar la lógica de negocio.

Esto aplica DIP/SOLID: los servicios de dominio/aplicación dependen del contrato `IExpedienteStorage`, no del SDK de Google.

## Activación y configuración por institución

La integración se administrará desde **Configuración > Almacenamiento documental** y deberá poder:

- activarse o desactivarse por institución;
- seleccionar proveedor;
- seleccionar modo de conexión;
- probar la conexión;
- inicializar automáticamente la estructura remota;
- mostrar estado de la integración sin revelar secretos;
- cambiar de proveedor mediante un proceso controlado de migración futura.

Desactivar la integración **no debe borrar archivos existentes**. Debe impedir nuevas escrituras y dejar la recuperación/migración bajo una operación administrativa explícita.

### Parámetros previstos

La UI y backend deberán contemplar parámetros según proveedor/modo, sin almacenar secretos en texto plano:

#### Google Workspace / Shared Drive

- integración habilitada;
- proveedor = `google_drive`;
- modo = `shared_drive`;
- dominio de la organización;
- identificador de Shared Drive cuando ya exista, o creación/selección administrada por SchoolManager cuando la API y permisos lo permitan;
- cuenta técnica/delegada utilizada por el backend;
- Client ID / credencial de aplicación cuando aplique;
- configuración de OAuth/API y scopes requeridos;
- estado de validación de permisos;
- identificador interno de la carpeta raíz administrada por SchoolManager.

#### Google Drive personal

- integración habilitada;
- proveedor = `google_drive`;
- modo = `personal_oauth`;
- cuenta Google conectada;
- Client ID de la aplicación;
- OAuth administrado desde SchoolManager;
- refresh token/credenciales almacenados cifrados o en secret storage, nunca visibles en frontend;
- identificador interno de la carpeta raíz administrada por SchoolManager.

La UI podrá mostrar el correo/dominio conectado como información administrativa, pero nunca devolver tokens, secretos o credenciales completas.

## Aprovisionamiento automático

**Un administrador no debe crear manualmente carpetas en Google Drive como requisito previo.**

Al activar/configurar el proveedor, el backend ejecutará un proceso idempotente de aprovisionamiento:

1. validar credenciales y permisos;
2. localizar o crear la raíz administrada por SchoolManager;
3. registrar los identificadores externos necesarios en configuración interna;
4. crear/verificar subestructuras cuando sean necesarias;
5. reusar estructuras existentes en ejecuciones posteriores;
6. nunca duplicar carpetas por reintentos.

La estructura física podrá ser similar a:

```text
SchoolManager - Expedientes/
  institucion-{uuid}/
    alumnos/
      alumno-{uuid}/
        documentos/
```

Los nombres visibles son auxiliares. **La relación autoritativa será por IDs almacenados en PostgreSQL**, no por nombres o rutas de carpetas.

## Modelo de metadata

PostgreSQL/Supabase seguirá siendo la fuente de verdad para el catálogo de documentos. El almacenamiento externo contendrá los bytes.

Modelo conceptual futuro:

```text
documentos_alumnos
- id (UUID SchoolManager)
- institucion_id
- alumno_id
- tipo_documento
- nombre_original
- mime_type
- tamanio_bytes
- sha256
- version
- storage_provider
- storage_object_id
- estado
- creado_por
- created_at
- updated_at
```

`storage_object_id` es información interna. No debe exponerse a clientes.

No se guardarán URLs públicas como autoridad del documento.

## Acceso y proxy del backend

Los clientes Angular/PWA/móvil no se conectarán directamente a Google Drive para documentos de expediente.

Flujo:

```text
Cliente -> API SchoolManager -> autorización/RBAC -> metadata DB -> IExpedienteStorage -> proveedor
```

La API pública utilizará IDs propios de SchoolManager:

```http
GET /api/documentos/{documentoId}/contenido
GET /api/documentos/{documentoId}/descargar
```

El backend validará al menos:

- autenticación;
- permiso requerido;
- institución activa/contexto;
- pertenencia del documento a la institución;
- relación con alumno/expediente;
- reglas especiales de acceso para responsables/padres cuando apliquen.

Nunca se expondrán al navegador o app móvil:

- Google file ID;
- Shared Drive ID;
- URLs directas de Drive;
- refresh tokens;
- service-account credentials;
- secretos OAuth.

## Seguridad y auditoría

- Archivos privados por defecto.
- Sin enlaces `anyone with the link`.
- Credenciales únicamente en backend/secret storage.
- Hash SHA-256 registrado para integridad.
- Tamaño y MIME type registrados y validados server-side.
- Límites de tamaño/cantidad configurables.
- Auditoría de alta, consulta sensible, sustitución/versionado, baja lógica y migración.
- Evitar IDOR: el cliente opera con `documentoId` interno y el backend resuelve el objeto externo.
- Eliminación física solo mediante flujo administrativo explícito y con trazabilidad.

## Versionado e inmutabilidad

Los documentos históricos importantes no deben sobrescribirse silenciosamente.

Una sustitución debe producir una nueva versión o dejar auditoría suficiente para conocer qué archivo existía anteriormente, quién lo reemplazó y cuándo.

Para documentos firmados, certificados o requeridos legalmente, se podrá exigir conservación inmutable de versiones anteriores.

## Captura móvil y conversión a PDF

Para fotografías tomadas desde SchoolManager Móvil:

1. el dispositivo conserva únicamente rutas temporales/estado de captura;
2. puede aplicar compresión ligera antes de enviar;
3. envía imágenes por multipart al backend;
4. el backend valida formato, tamaño, orientación y cantidad;
5. el backend normaliza y genera el PDF definitivo;
6. calcula hash;
7. persiste mediante `IExpedienteStorage`;
8. registra metadata en PostgreSQL dentro del flujo transaccional correspondiente.

Redux/store móvil no debe conservar imágenes Base64 completas ni secretos del proveedor.

## Consistencia y ACID

El archivo externo y la metadata viven en sistemas diferentes, por lo que no existe una transacción distribuida ACID real entre PostgreSQL y Google Drive.

La implementación deberá usar un flujo compensable e idempotente:

1. preparar operación y validar permisos;
2. subir archivo;
3. registrar metadata en DB;
4. si falla DB después de la subida, ejecutar limpieza compensatoria o registrar objeto huérfano para reconciliación;
5. utilizar claves/idempotency keys para evitar duplicados por reintentos;
6. disponer de reconciliación periódica de objetos huérfanos/inconsistentes.

Las reglas de negocio permanecen en backend; el proveedor solo implementa persistencia de bytes.

## Resiliencia

Los adaptadores externos deberán contemplar:

- timeout;
- retry con backoff exponencial para errores transitorios;
- cancelación;
- subidas resumibles para archivos grandes cuando el proveedor lo soporte;
- logging estructurado sin secretos;
- health/status de integración;
- circuit breaker si resulta necesario;
- idempotencia.

## Multiinstitución

La configuración será por institución. Dos instituciones pueden usar proveedores/modos distintos simultáneamente.

Ejemplo:

```text
Institución A -> Google Workspace / Shared Drive
Institución B -> Google Drive personal / OAuth
Institución C -> integración deshabilitada
Institución D -> Supabase Storage
```

El proveedor se resolverá server-side a partir del contexto institucional. El frontend nunca elige arbitrariamente una configuración perteneciente a otra institución.

## Bases que 047A debe preservar

El bloque 047A debe quedar compatible con esta evolución:

- DTOs de documentos separados del motor de PDF;
- servicios de aplicación backend como autoridad;
- controllers delgados;
- frontend limitado a presentación/impresión/descarga;
- adaptadores externos detrás de interfaces;
- ninguna dependencia de Google dentro de la lógica de pagos;
- los recibos regenerables no requieren persistencia externa.

No es requisito de 047A crear las tablas, credenciales, OAuth de Drive ni los adaptadores anteriores.

## Fuera de alcance actual

Para evitar inflar 047A, quedan para 047B o posterior:

- migración de metadata documental;
- `IExpedienteStorage` productivo y sus adaptadores;
- OAuth de Google Drive personal;
- integración Workspace/Shared Drive;
- aprovisionamiento automático remoto;
- captura móvil de expediente;
- visor de expediente;
- migración entre proveedores;
- reconciliación de objetos huérfanos;
- políticas de retención institucional configurables.

## Criterios de implementación futura

La integración solo se considerará lista cuando:

1. pueda activarse/desactivarse por institución;
2. todos los parámetros requeridos se configuren desde SchoolManager;
3. no requiera creación manual de carpetas;
4. no exponga rutas/IDs/tokens del proveedor al cliente;
5. permita Workspace Shared Drive y Google Drive personal como modos distintos;
6. conserve RBAC y aislamiento multiinstitución en backend;
7. tenga pruebas positivas/negativas de autorización;
8. tenga pruebas de idempotencia y compensación ante fallos;
9. documente recuperación, rotación de credenciales y desconexión;
10. no convierta Google Drive en una dependencia obligatoria del funcionamiento general de SchoolManager.
