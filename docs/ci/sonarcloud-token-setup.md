# Configuración de SONAR_TOKEN para SonarCloud (Bloque 029)

> **Acción humana requerida.** Este documento detalla, paso a paso, cómo
> activar el análisis real de SonarCloud. Mientras `SONAR_TOKEN` no esté
> configurado, el job `sonarcloud` del CI quedará **en rojo a propósito**
> (guard anti falso-verde) para que un job interno nunca aparezca verde sin
> haber analizado.

## ¿Por qué está rojo el job `sonarcloud`?

El análisis de SonarCloud corre dentro de `.github/workflows/deploy.yml`
(job `sonarcloud`). Para subir el análisis hace falta un token de análisis.
El workflow lee el secret de GitHub Actions `SONAR_TOKEN`:

```yaml
env:
  SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
```

Si el secret **no existe**, `SONAR_TOKEN` es `''`, el paso `Analizar con
sonar-scanner` se salta y, desde el Bloque 029, el paso guard que le sigue
**falla el job con un error explícito**. Antes del Bloque 029 ese job quedaba
verde silenciosamente (falso verde): parecía que SonarCloud analizaba, pero no
se subía nada.

Verificación actual (2026-09-07): `gh secret list` no incluye `SONAR_TOKEN`
en el repositorio ni en la organización `acevallos31`.

## Qué necesitas configurar

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
organización `acevallos31` para no repetirlo por repositorio:

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

### 5. Confirmar el análisis real

Tras configurar el secret, reabre o re-dispara el CI del PR. El job
`sonarcloud` debe pasar ejecutando `sonar-scanner` (ya no saltarse) y el
análisis aparece en https://sonarcloud.io/dashboard?id=SchoolManager con el
quality gate correspondiente.

## Referencias en el repositorio

- `.github/workflows/deploy.yml` — job `sonarcloud` + guard anti falso-verde.
- `sonar-project.properties` — configuración del análisis (sources, tests,
  rutas de cobertura backend/frontend).
- `.gitignore` — ignora `.env`, `.env.local`, `**/environment.secret.ts`,
  `*.env` (los secretos del frontend no se commitean).
- `docs/handoffs/020.5A-hardening-ci.md` — contexto previo de hardening del CI.
