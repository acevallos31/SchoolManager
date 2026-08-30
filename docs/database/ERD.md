# Diagrama entidad-relación

```mermaid
erDiagram
    INSTITUCIONES ||--o{ ALUMNOS : contiene
    INSTITUCIONES ||--o{ RESPONSABLES : contiene
    INSTITUCIONES ||--o{ CICLOS_ESCOLARES : organiza
    INSTITUCIONES ||--o{ SECCIONES : contextualiza
    INSTITUCIONES o|--o{ USUARIOS_ROLES : limita

    PERSONAS ||--o| USUARIOS : autentica
    PERSONAS ||--o{ ALUMNOS : perfila
    PERSONAS ||--o{ RESPONSABLES : perfila

    USUARIOS ||--o{ USUARIOS_ROLES : recibe
    ROLES ||--o{ USUARIOS_ROLES : asigna
    ROLES ||--o{ ROLES_PERMISOS : contiene
    PERMISOS ||--o{ ROLES_PERMISOS : compone

    ALUMNOS ||--o{ ALUMNO_RESPONSABLE : vincula
    RESPONSABLES ||--o{ ALUMNO_RESPONSABLE : vincula

    CICLOS_ESCOLARES ||--o{ SECCIONES : define
    GRADOS ||--o{ SECCIONES : clasifica
    JORNADAS o|--o{ SECCIONES : organiza
    CICLOS_ESCOLARES ||--o{ PERIODOS_MATRICULA : abre

    ALUMNOS ||--o{ MATRICULAS : cursa
    SECCIONES ||--o{ MATRICULAS : recibe
    PERIODOS_MATRICULA ||--o{ MATRICULAS : registra
    USUARIOS o|--o{ MATRICULAS : crea
    MATRICULAS ||--o{ MATRICULA_ESTADO_HISTORIAL : genera
    USUARIOS o|--o{ MATRICULA_ESTADO_HISTORIAL : cambia

    INSTITUCIONES {
      uuid id PK
      text nombre
      boolean activo
    }
    PERSONAS {
      uuid id PK
      text nombres
      text apellidos
      text estado
    }
    USUARIOS {
      uuid id PK
      uuid persona_id FK
      uuid auth_user_id UK
      boolean activo
    }
    ROLES {
      uuid id PK
      text codigo UK
      boolean activo
    }
    PERMISOS {
      uuid id PK
      text codigo UK
      text modulo
    }
    USUARIOS_ROLES {
      uuid id PK
      uuid usuario_id FK
      uuid rol_id FK
      uuid institucion_id FK
      boolean activo
    }
    ROLES_PERMISOS {
      uuid rol_id PK,FK
      uuid permiso_id PK,FK
    }
    ALUMNOS {
      uuid id PK
      uuid persona_id FK
      uuid institucion_id FK
      text estado
    }
    RESPONSABLES {
      uuid id PK
      uuid persona_id FK
      uuid institucion_id FK
    }
    ALUMNO_RESPONSABLE {
      uuid id PK
      uuid alumno_id FK
      uuid responsable_id FK
    }
    CICLOS_ESCOLARES {
      uuid id PK
      uuid institucion_id FK
      text nombre
      boolean activo
    }
    GRADOS {
      uuid id PK
      text nombre UK
      integer orden
    }
    JORNADAS {
      uuid id PK
      text nombre UK
    }
    SECCIONES {
      uuid id PK
      uuid institucion_id FK
      uuid ciclo_id FK
      uuid grado_id FK
      uuid jornada_id FK
      text nombre
      integer cupo
    }
    PERIODOS_MATRICULA {
      uuid id PK
      uuid ciclo_id FK
      date fecha_inicio
      date fecha_fin
    }
    MATRICULAS {
      uuid id PK
      uuid alumno_id FK
      uuid institucion_id FK
      uuid ciclo_id FK
      uuid seccion_id FK
      uuid periodo_matricula_id FK
      text estado
    }
    MATRICULA_ESTADO_HISTORIAL {
      uuid id PK
      uuid matricula_id FK
      text estado_anterior
      text estado_nuevo
      uuid usuario_id FK
      timestamptz fecha
    }
```

## Frontera de seguridad

El ERD representa relaciones, no policies. Las tablas de identidad, RBAC y
académicas del diagrama tienen RLS habilitada para lecturas de
`authenticated`; el ámbito se resuelve desde `auth.uid()` y
`usuarios_roles.institucion_id`. Las escrituras transaccionales y la gestión de
roles se realizan mediante las funciones `rpc_*`, no con DML directo desde el
cliente. El detalle está en [SUPABASE_SECURITY.md](SUPABASE_SECURITY.md).
