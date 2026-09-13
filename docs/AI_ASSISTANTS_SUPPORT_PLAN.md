# Plan inicial: soporte de errores, tickets y asistentes Hermes

**Estado:** propuesta inicial para revisión  
**Fecha:** 2026-09-13  
**Alcance de este documento:** diseño y decisiones preliminares. No implementa todavía endpoints, tablas, conexión con Hermes, facturación ni cambios en Supabase.

## 1. Objetivo

Explorar una integración opcional entre SchoolManager y Hermes para dos necesidades separadas:

1. **Soporte del producto:** capturar errores de la plataforma, recibir reportes manuales de usuarios y convertirlos en tickets que un agente de desarrollo pueda investigar y proponer corregir.
2. **Asistente institucional:** ofrecer a una institución un asistente opcional dentro de SchoolManager para consultas académicas, matrícula, reportes y orientación operativa.

Las dos funciones deben tener identidades, datos, permisos y costos separados. Una institución no debe recibir acceso al agente interno de desarrollo.

## 2. Dos agentes, no un rol genérico

| Identidad | Usuario | Alcance | Regla principal |
| --- | --- | --- | --- |
| development_agent | Equipo de desarrollo/proveedor | Repositorio, CI, logs sanitizados y tickets técnicos | Puede investigar y abrir PR; una persona aprueba merge y despliegue |
| institution_assistant | Institución contratante | Datos y operaciones permitidas de una sola institución | Usa herramientas del backend; nunca consulta SQL arbitrario ni decide permisos |

Estas identidades son **identidades de máquina**, no roles humanos que se asignan desde la pantalla de usuarios.

La relación con RBAC está definida en [Diseño de roles y permisos dinámicos](ROLES_PERMISOS_DINAMICOS.md).

## 3. Flujo propuesto de errores y tickets

~~~text
Error de UI/API o reporte manual
        ↓
Sanitización, correlación y fingerprint del incidente
        ↓
Ticket con contexto mínimo y sin secretos
        ↓
Agente de desarrollo: clasifica, reproduce, prueba e investiga
        ↓
Rama + cambios + pruebas + Pull Request
        ↓
Revisión humana, CI/Quality Gate, merge y despliegue
~~~

### 3.1 Captura automática

La primera versión debería capturar únicamente:

- entorno y versión/deploy;
- ruta, módulo y operación;
- método HTTP y código de respuesta;
- mensaje técnico sanitizado y fingerprint;
- correlation ID;
- navegador/versión de forma resumida;
- institución afectada solo como referencia interna protegida;
- frecuencia, primera aparición y última aparición.

No debe capturar:

- contraseñas;
- access tokens, refresh tokens, cookies o headers de autorización;
- secretos de Vercel, Render, Supabase, GitHub, Hermes u otros proveedores;
- payloads completos con datos de alumnos;
- información sensible de menores salvo que sea imprescindible y esté minimizada.

Las capturas de pantalla y datos adicionales deben ser voluntarios, con advertencia al usuario y redacción previa cuando sea posible.

### 3.2 Reporte manual

El usuario podrá abrir un ticket desde la interfaz indicando:

- título y descripción;
- módulo y ruta actual;
- tipo: error lógico, visualización, datos o acceso;
- pasos para reproducir;
- severidad percibida;
- adjuntos opcionales.

El backend debe generar el ticket y añadir la información técnica permitida. El navegador nunca enviará el token de sesión al agente.

## 4. Flujo del asistente institucional

~~~text
Usuario
  ↓
Angular
  ↓ HTTPS + JWT
API .NET
  ↓ políticas, institución activa y alcance de datos
Herramientas seguras del dominio
  ↓
Instancia Hermes configurada para la institución
~~~

SchoolManager seguirá siendo responsable de:

- autenticar al usuario;
- resolver la institución activa;
- aplicar RBAC, relaciones de responsable/alumno y RLS;
- ejecutar las consultas y operaciones de negocio;
- confirmar las mutaciones;
- registrar auditoría.

Hermes se utilizará para interpretar la solicitud, elegir una herramienta permitida y explicar el resultado. No será la autoridad de seguridad ni de negocio.

Ejemplos de herramientas futuras:

- consultar estado de una matrícula;
- preparar una matrícula para revisión;
- consultar alumnos permitidos;
- generar un reporte parametrizado;
- explicar requisitos o pasos de un proceso;
- crear un borrador de comunicación.

Las operaciones que escriban datos deberán requerir confirmación explícita y volver a validar permisos en el backend justo antes de ejecutarse.

## 5. Integración técnica prevista

La integración debe ser servidor a servidor:

- Angular llama a SchoolManager, no directamente a Hermes.
- La API .NET mantiene las credenciales de la instancia y aplica límites.
- La API expone una fachada de herramientas con contratos explícitos.
- Cada llamada lleva contexto de institución y usuario, pero no entrega secretos ni tokens de larga duración al modelo.
- Se registran auditoría, latencia, costo estimado, herramienta usada y resultado resumido.
- Los errores del proveedor no deben revelar URLs internas, claves ni trazas completas al navegador.

Hermes ofrece un servidor API compatible con el formato OpenAI y un mecanismo MCP para conectar herramientas externas. Estos mecanismos son opciones de integración, no una decisión de implementación cerrada para este bloque:

- API Server: https://hermes-agent.nousresearch.com/docs/api-server
- MCP: https://hermes-agent.nousresearch.com/docs/mcp
- Seguridad: https://hermes-agent.nousresearch.com/docs/security
- Perfiles aislados: https://hermes-agent.nousresearch.com/docs/profiles

## 6. Modalidades de despliegue

| Modalidad | Responsable de infraestructura | Uso previsto |
| --- | --- | --- |
| Proveedor administrado | SchoolManager/proveedor de IA | Activación rápida con configuración central |
| Instancia de la institución | Escuela en VPS, servidor local o red privada | Mayor control y posible requisito de residencia de datos |
| Hermes Cloud | Institución o proveedor según contrato | Instancia persistente separada y facturable |

La configuración debe permitir activar o desactivar el asistente por institución, elegir el endpoint y definir límites. Una instancia no debe compartir memoria, sesiones, herramientas ni credenciales con otra institución.

La facturación futura debe medir al menos:

- instancia o infraestructura;
- uso de modelo;
- uso de herramientas;
- almacenamiento;
- límites y sobreconsumo.

No se fija precio en esta propuesta; debe validarse con el proveedor elegido y con el modelo comercial de SchoolManager.

## 7. Estrategia de costo reducido

Para evitar que cada pregunta produzca un costo alto:

- generar reportes y agregaciones con código .NET y SQL parametrizado;
- usar el modelo para interpretar, resumir y explicar, no para recalcular totales;
- clasificar errores simples con un modelo económico;
- reservar modelos más capaces para diagnósticos complejos;
- procesar errores repetidos de forma agrupada;
- usar colas/asíncrono para análisis no urgente;
- aplicar cuotas por institución, usuario y período;
- cachear respuestas seguras y datos de referencia;
- permitir proveedores compatibles sin acoplar la lógica de negocio a uno solo.

La política debe ser “modelo como orquestador controlado”, no “modelo con acceso libre a la base”.

## 8. Seguridad y privacidad

- No usar la clave service_role de Supabase en Angular ni entregarla a Hermes.
- No permitir SQL libre generado por el modelo.
- No permitir que el agente cambie roles, permisos, RLS, secretos o configuración de producción.
- Separar tickets técnicos internos de tickets visibles para una institución.
- Aplicar redacción y retención limitada a logs y conversaciones.
- Registrar quién pidió una acción, qué herramienta se ejecutó y qué confirmación se recibió.
- Rechazar o poner en revisión solicitudes que intenten extraer datos de otra institución.
- Tratar los datos académicos de menores como información sensible y minimizar su exposición.
- Mantener aprobación humana para cambios de código, despliegues y operaciones irreversibles.

## 9. Roadmap sugerido

### Fase 0 — Contrato de integración

- definir eventos y formato de ticket;
- definir datos permitidos y política de retención;
- definir identidad de máquina y secret management;
- definir herramientas y permisos por institución;
- decidir si el primer proveedor será una instalación propia, VPS o Hermes Cloud.

### Fase 1 — Tickets y observabilidad

- endpoint backend para reportes manuales;
- captura de errores con redacción;
- fingerprint y deduplicación;
- bandeja de tickets y estados;
- auditoría sin agente autónomo.

### Fase 2 — Agente de desarrollo

- conexión servidor a servidor;
- lectura de tickets y logs permitidos;
- ejecución de pruebas en entorno aislado;
- creación de ramas y PR;
- aprobación humana obligatoria.

### Fase 3 — Asistente institucional opcional

- configuración por institución;
- herramientas de consulta read-only;
- reportes parametrizados;
- confirmación para operaciones de escritura;
- límites y auditoría por institución.

### Fase 4 — Comercialización

- elección de modalidad de despliegue;
- medición de consumo;
- cuotas y facturación;
- soporte de múltiples proveedores;
- controles de apagado y exportación.

## 10. Criterios de aceptación del futuro módulo

El módulo no se considerará listo hasta que:

- sea opcional por institución;
- ninguna llamada del navegador contenga una clave de Hermes;
- un usuario no pueda consultar datos fuera de su alcance;
- el agente no pueda asignarse permisos de SchoolManager por sí mismo;
- un ticket no incluya tokens, contraseñas ni payloads sensibles;
- las acciones de escritura tengan confirmación y auditoría;
- el agente de desarrollo solo pueda abrir cambios revisables;
- las pruebas, CI y Quality Gate sigan siendo obligatorias;
- se pueda desactivar la integración sin afectar el login ni los módulos académicos.

**Decisión para el siguiente bloque:** continuar con el RBAC dinámico por institución y usar sus permisos como frontera de las futuras herramientas de Hermes.
