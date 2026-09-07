# Configuración de SONAR_TOKEN para SonarCloud (Bloque 029)

> **Estado: RESUELTO (2026-09-07).** `SONAR_TOKEN` está configurado como secret
> del repositorio y el análisis real de SonarCloud pasa (ver sección «Estado
> actual» al final). Este documento conserva el procedimiento por si hay que
> regenerar/rotar el token, y documenta la regla de directorios de
> `sonar-project.properties`.

## Por qué existe este documento

El análisis de SonarCloud corre dentro de `.github/workflows/deploy.yml`
(job `sonarcloud`). Para subir el análisis hace falta un token de análisis.
El workflow lee el secret de GitHub Actions `SONAR_TOKEN`:

```yaml
env:
  SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
```

Si el secret **no existe**, `SONAR_TOKEN` es `''` y el job **falla en rojo a
propósito** (guard anti falso-verde): un job interno nunca debe quedar verde
sin haber subido análisis real. Antes del Bloque 029, el job podía quedar verde
silenciosamente con `sonar-scanner` skipped (falso verde).

## Procedimiento (si se regenera/rota el token)

### 1. Generar un token de análisis en SonarCloud

1. Ve a https://sonarcloud.io y entra con la cuenta propietaria del proyecto
   (organización `acevallos31`).
2. `My Account` → `Security`.
3. Genera un **Analysis Token** (p. ej. `sm_sonar_token`).
   - Usa *Analysis Token* (para subir análisis desde CI), no un token global
     de cuenta.
4. Copia el valor (se muestra una sola vez).

> ⚠️ El token es un secreto. **No se commitea, no se imprime, no se pega en
> chat, logs ni issues.** Se guarda únicamente como secret de GitHub Actions.

### 2. Registrar el secret en GitHub

Desde la raíz del repositorio:

```bash
gh secret set SONAR_TOKEN
# te pedirá pegar el valor; elige visibilidad del repositorio
```

O en la web: **Settings → Secrets and variables → Actions → New repository
secret**, nombre `SONAR_TOKEN`, valor el token.

Opcional (recomendado si hay varios repos): crear el secret a nivel de
organización `acevallos31`:

```bash
gh secret set SONAR_TOKEN --org acevallos31
```

### 3. Verificar que se ve el nombre (nunca el valor)

```bash
gh secret list
# debe aparecer SONAR_TOKEN  (solo el nombre)
```

### 4. Desactivar Automatic Analysis en SonarCloud (manual, una sola vez)

El CI usa **CI Analysis**; conviene desactivar la importación automática para
evitar análisis duplicados. Se hace en el panel de SonarCloud (proyecto →
Administration → Analysis Method → desactivar *Automatic Analysis*). No se
puede automatizar desde el repo.

## Regla importante: `sonar.sources` y `sonar.tests` aceptan DIRECTORIOS, no wildcards

SonarScanner (desde 8.x) **rechaza comodines** (`**`, `*`) en las propiedades
`sonar.sources` y `sonar.tests`. Si se usan, el análisis falla en la fase de
configuración con (exit code 3):

```
ERROR Invalid value of sonar.tests for SchoolManager
ERROR Wildcards ** and * are not supported in "sonar.sources" and "sonar.tests".
```

Reglas de `sonar-project.properties`:

- `sonar.sources` y `sonar.tests` aceptan **solo listas de directorios**
  separadas por coma (sin `**` ni `*`).
  - `sonar.sources=backend,frontend/schoolmanager-frontend/src`
  - `sonar.tests=tests`
- Los archivos que deban **excluirse** (artefactos, build, coverage) y los
  specs colocalizados se gestionan con `sonar.exclusions`, que **sí** admite
  wildcards.
- Los `.spec.ts` colocalizados bajo un directorio declarado en
  `sonar.sources` **no se pueden clasificar como tests** por patrón (un dir
  de `sonar.tests` no admite wildcards para recogerlos), así que se **excluyen**
  del análisis vía `sonar.exclusions`. La cobertura sigue siendo correcta
  porque el reporte LCOV de vitest solo mide código productivo.

Esto se corrigió en el commit `e76cedc` (Bloque 029). Ver
`sonar-project.properties` para la config completa.

## Estado actual (2026-09-07)

- `SONAR_TOKEN` **configurado** como secret del repositorio y **válido**
  (validado contra `/api/authentication/validate` → `valid=true`); GitHub lo
  inyecta correctamente (`SONAR_TOKEN: ***` en los logs).
- El job `sonarcloud` usa la **action oficial**
  `SonarSource/sonarqube-scan-action@v8.2.1` (auto-aprovisiona su JRE y su
  sonar-scanner), en lugar del CLI descargado manualmente (que moría con
  `exit 8` sin salida útil).
- El análisis **real** pasa: cobertura backend (Cobertura) y frontend (LCOV)
  importadas, `ANALYSIS SUCCESSFUL`, Quality Gate del PR **en verde**.
  Run validado: **34154637093** (HEAD `e76cedc`).
- El guard anti falso-verde permanece activo: si `SONAR_TOKEN` faltara, el job
  fallaría en rojo explícito en vez de saltarse.

## Referencias en el repositorio

- `.github/workflows/deploy.yml` — job `sonarcloud` (action oficial v8.2.1) +
  guard anti falso-verde.
- `sonar-project.properties` — configuración del análisis (sources/tests solo
  directorios, rutas de cobertura backend/frontend, exclusiones).
- `.gitignore` — ignora `.env`, `.env.local`, `**/environment.secret.ts`,
  `*.env` (los secretos del frontend no se commitean).
- `docs/handoffs/029-calidad-sonar-e2e.md` — handoff del bloque con el cierre.
