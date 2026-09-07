# Configuración de SONAR_TOKEN para SonarCloud (Bloque 029 / 029B)

> **Estado: RESUELTO (2026-09-07).** `SONAR_TOKEN` está configurado como secret
> del repositorio y el análisis real de SonarCloud pasa (ver sección «Estado
> actual» al final). Este documento conserva el procedimiento por si hay que
> regenerar/rotar el token, y documenta la regla de directorios.

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

## Cómo se define el alcance con SonarScanner for .NET (029B)

> **El scanner .NET NO soporta `sonar.sources` ni `sonar.tests`**: los ignora con un
> WARNING explícito (*"are not supported by the Scanner for .NET and are ignored"*,
> ver log del run `34160844359`). Por eso **no se pasan** en el `begin` y no aplica
> ninguna regla de directorios/wildcards sobre ellos. Cómo se determina el alcance:

- **C# y proyectos de tests:** se obtienen automáticamente de los `.csproj` que se
  compilan en el paso *build* del job (backend + proyectos de tests) — MSBuild
  reporta al scanner qué es MAIN y qué es TEST.
- **Frontend TS/Angular standalone:** se incorpora con `sonar.scanner.scanAll=true`
  (activo por defecto en el scanner .NET v8+), que además del MSBuild indexa el
  resto de archivos del repo (TypeScript/TSX).
- **El alcance se afina SOLO con** `sonar.exclusions` / `sonar.inclusions` /
  `sonar.test.exclusions` / `sonar.test.inclusions` (sí soportadas y con wildcards)
  y con las rutas de reportes de cobertura.

En el `begin` de `deploy.yml` (todo vía `/d:`, porque el scanner .NET no lee
`sonar-project.properties` — que fue eliminado — y da error si existe):

- `sonar.exclusions` excluye artefactos/build/coverage, `e2e/**`, `.spec.ts` y
  `.test.ts` colocalizados, `lcov.info`, `*.cobertura.xml`, etc. Por eso el paso fija
  `set -f`: los patrones `**` deben llegar literales al scanner (sin expansión del
  shell).
- Los `.spec.ts` colocalizados en `frontend/src` no pueden clasificarse como tests
  por patrón (sin `sonar.tests` para recogerlos), así que se **excluyen** vía
  `sonar.exclusions`. La cobertura sigue siendo correcta porque el reporte LCOV de
  vitest solo mide código productivo.

> Contexto histórico: la regla previa de "solo directorios sin wildcards en
> `sonar.sources`/`sonar.tests`" aplicaba al **scanner genérico** (action
> `sonarqube-scan-action`, corregido en `e76cedc` del 029). Con el SonarScanner for
> .NET del 029B esa restricción quedó obsoleta porque esas propiedades se ignoran.

## Estado actual (2026-09-07)

- `SONAR_TOKEN` **configurado** como secret del repositorio y **válido**
  (validado contra `/api/authentication/validate` → `valid=true`); GitHub lo
  inyecta correctamente (`SONAR_TOKEN: ***` en los logs).
- El job `sonarcloud` usa **SonarScanner for .NET** (`dotnet-sonarscanner`,
  tool v11.3.0) con flujo `begin → dotnet build (tests) → end`, análisis
  estático **C# + frontend TS** (scanAll) + coberturas (Bloque 029B). El QG se
  verifica con `SonarSource/sonarqube-quality-gate-action` pineado por SHA.
- El análisis **real** pasa: backend C# procesado (sin warning
  `cannot be analyzed`), cobertura backend (Cobertura) y frontend (LCOV)
  importadas, `ANALYSIS SUCCESSFUL`, Quality Gate del PR **en verde**.
  Run validado 029B: **34160443496** (HEAD `6e3299a`).
- El guard anti falso-verde permanece activo: si `SONAR_TOKEN` faltara, el job
  fallaría en rojo explícito en vez de saltarse.

## Referencias en el repositorio

- `.github/workflows/deploy.yml` — job `sonarcloud` (SonarScanner for .NET +
  paso de Quality Gate + guard anti falso-verde).
- La configuración del análisis (sources/tests solo directorios, rutas de
  cobertura backend/frontend, exclusiones) se pasa como `/d:` en el `begin`
  del job; **ya no existe `sonar-project.properties`** (el scanner .NET no lo
  lee).
- `.gitignore` — ignora `.env`, `.env.local`, `**/environment.secret.ts`,
  `*.env` (los secretos del frontend no se commitean).
- `docs/handoffs/029B-sonarcloud-csharp.md` — handoff del bloque que cerró el
  residual C#; `docs/handoffs/029-calidad-sonar-e2e.md` — handoff del 029.
