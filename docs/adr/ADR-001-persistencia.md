# ADR-001: Usar PostgreSQL gestionado en Supabase como persistencia principal

## Contexto

SchoolManager administra información académica y financiera que debe mantenerse
consistente cuando varios usuarios trabajan al mismo tiempo. El sistema maneja
alumnos, matrículas, ciclos, responsables, cargos y pagos, por lo que necesita
relaciones, restricciones de integridad, transacciones y control de acceso a los
datos.

Una base de datos local o embebida simplificaría el desarrollo inicial, pero no
representaría bien el escenario real de una aplicación web desplegada y usada
por diferentes personas desde distintos equipos.

## Decisión

Usar **PostgreSQL 16 gestionado en Supabase** como base de datos principal de
SchoolManager.

La API ASP.NET Core accede a PostgreSQL mediante Npgsql. Las reglas que protegen
la consistencia transaccional se apoyan en restricciones SQL, RLS y funciones
RPC cuando corresponde. Las claves primarias y foráneas internas utilizan UUID.

El frontend Angular no accede directamente a las tablas de negocio; las
operaciones de negocio pasan por la API .NET.

## Consecuencias

### Positivas

- Permite relaciones y restricciones fuertes para proteger la integridad de los datos.
- Soporta transacciones para procesos como matrículas, cargos y pagos.
- Permite varios usuarios concurrentes sin depender de un archivo local.
- Supabase simplifica el alojamiento, respaldo y disponibilidad de PostgreSQL.
- Mantiene una única fuente de verdad para los datos académicos y financieros.

### Negativas

- El sistema depende de conectividad con el servicio de base de datos.
- La configuración y el despliegue son más complejos que usar una base embebida.
- Es necesario administrar migraciones, credenciales y políticas de seguridad.
- Parte de la lógica de integridad queda distribuida entre la API y PostgreSQL,
  por lo que debe mantenerse documentada y probada.

## Estado

Aceptada.
