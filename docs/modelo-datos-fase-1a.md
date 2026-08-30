# Modelo de Datos Fase 1A

## Entidades nuevas

| Tabla | Proposito |
| --- | --- |
| `instituciones` | Configuracion organizacional. |
| `configuracion_identificadores` | Politica institucional de identificadores. |
| `personas` | Identidad y contacto globales. |
| `responsables` | Rol de responsable por institucion. |
| `alumno_responsable` | Relacion familiar N:M y permisos. |
| `periodos_matricula` | Ventanas de matricula por ciclo. |

## Relaciones

```mermaid
erDiagram
    INSTITUCIONES ||--|| CONFIGURACION_IDENTIFICADORES : configura
    PERSONAS ||--o{ ALUMNOS : identifica
    PERSONAS ||--o{ RESPONSABLES : representa
    PERSONAS ||--o{ USUARIOS : autentica
    ALUMNOS ||--o{ ALUMNO_RESPONSABLE : tiene
    RESPONSABLES ||--o{ ALUMNO_RESPONSABLE : participa
    INSTITUCIONES ||--o{ CICLOS_ESCOLARES : opera
    CICLOS_ESCOLARES ||--o{ PERIODOS_MATRICULA : contiene
    ALUMNOS ||--o{ MATRICULAS : cursa
    CICLOS_ESCOLARES ||--o{ MATRICULAS : corresponde
    GRADOS ||--o{ MATRICULAS : asigna
    SECCIONES ||--o{ MATRICULAS : asigna
    PERIODOS_MATRICULA ||--o{ MATRICULAS : aplica
```

La institucion de un periodo se deriva de su ciclo. La matricula mantiene
directamente alumno, ciclo, grado y seccion; la oferta anual queda pospuesta.

## Identificadores

`personas.numero_identificacion` conserva el valor presentado. Su campo
`numero_identificacion_normalizado` sirve para comparar y aplicar unicidad.
La normalizacion es generica y no impone formatos nacionales.

RNE es nullable en `alumnos` y unico global cuando existe. Su comparacion es
exacta: no se aplica `lower`, trim, regex ni normalizacion hasta disponer de una
especificacion formal. `codigo_interno` es nullable y unico por institucion.

## Restricciones relevantes

- Las PK y FK usan UUID.
- `UNIQUE(alumno_id, responsable_id)` evita relaciones duplicadas.
- Un indice unico parcial permite como maximo un responsable principal activo.
- El indice unico parcial de codigo interno protege su contexto institucional.
- El indice unico parcial global de RNE protege los valores no nulos con
    comparacion exacta.
- Checks validan estados, fechas ordenadas y texto no vacio.

## Datos legacy

No se eliminan ni se vuelven fuente secundaria en Fase 1A: los campos actuales
de alumnos, usuarios, ciclos y matriculas siguen disponibles para consumidores
existentes. `padres_encargados` es texto no estructurado y nunca se transforma
automaticamente en personas o responsables.

## Diagrama ER final

```mermaid
erDiagram
    INSTITUCIONES {
        uuid id PK
        text nombre
    }
    CONFIGURACION_IDENTIFICADORES {
        uuid id PK
        uuid institucion_id FK
    }
    PERSONAS {
        uuid id PK
        text numero_identificacion
        text numero_identificacion_normalizado
    }
    ALUMNOS {
        uuid id PK
        uuid persona_id FK
        uuid institucion_id FK
        text rne
        text codigo_interno
    }
    USUARIOS {
        uuid id PK
        uuid persona_id FK
        uuid auth_user_id
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
        boolean es_principal
        boolean acceso_financiero
    }
    CICLOS_ESCOLARES {
        uuid id PK
        uuid institucion_id FK
    }
    PERIODOS_MATRICULA {
        uuid id PK
        uuid ciclo_id FK
    }
    GRADOS {
        uuid id PK
    }
    SECCIONES {
        uuid id PK
        uuid grado_id FK
    }
    MATRICULAS {
        uuid id PK
        uuid alumno_id FK
        uuid ciclo_id FK
        uuid grado_id FK
        uuid seccion_id FK
        uuid periodo_matricula_id FK
        uuid registrado_por FK
    }

    INSTITUCIONES ||--|| CONFIGURACION_IDENTIFICADORES : configura
    INSTITUCIONES ||--o{ ALUMNOS : registra
    INSTITUCIONES ||--o{ RESPONSABLES : opera
    INSTITUCIONES ||--o{ CICLOS_ESCOLARES : administra
    PERSONAS ||--o{ ALUMNOS : identifica
    PERSONAS ||--o| USUARIOS : autentica
    PERSONAS ||--o{ RESPONSABLES : representa
    ALUMNOS ||--o{ ALUMNO_RESPONSABLE : tiene
    RESPONSABLES ||--o{ ALUMNO_RESPONSABLE : participa
    CICLOS_ESCOLARES ||--o{ PERIODOS_MATRICULA : contiene
    ALUMNOS ||--o{ MATRICULAS : cursa
    CICLOS_ESCOLARES ||--o{ MATRICULAS : corresponde
    GRADOS ||--o{ MATRICULAS : asigna
    SECCIONES ||--o{ MATRICULAS : asigna
    PERIODOS_MATRICULA ||--o{ MATRICULAS : aplica
    USUARIOS ||--o{ MATRICULAS : registra
```
