# Cierre smart de Sonar — PR #31

## Objetivo
Cerrar exclusivamente el estado de integración/Sonar del PR #31 sin tocar la lógica funcional del Bloque 018, sin mergear el PR y sin iniciar 019.

## Estado de partida conocido
- Repo: `/opt/projects/SchoolManager`
- PR: #31
- Rama: `feature/responsables-fase-018`
- Se agregó `.sonarcloud.properties` al default branch `main` con `sonar.cpd.exclusions=database/baseline/` para que el análisis automático de SonarQube Cloud ignore el baseline consolidado solo en CPD.
- El último análisis visible anterior seguía mostrando duplicación stale de ~34.12% porque correspondía a una corrida previa a la exclusión.
- Se creó un commit vacío para relanzar análisis: `a7da06ce5890d645bc52adc75a839aee0f0c55b3`.
- Después de avanzar `main` con la configuración de Sonar, GitHub llegó a reportar el PR como no mergeable. La causa probable es que la rama del PR no estaba sincronizada con el nuevo `main` y ambos lados tocaron/agregaron `.sonarcloud.properties`.

## Instrucciones
Trabaja en modo smart y de forma autónoma dentro de este alcance.

1. Entra a `/opt/projects/SchoolManager`.
2. Verifica que no haya otro escritor activo sobre el repo y respeta el mecanismo single-writer de `AGENTS.md`.
3. Lee `AGENTS.md`, `docs/AI_CONTEXT.md` y `docs/handoffs/018-cierre-pr31.md` solo en lo necesario para este cierre.
4. Ejecuta `git status`, confirma la rama `feature/responsables-fase-018`, haz `git fetch origin` y compara `origin/main` con `HEAD`.
5. Sin rebase destructivo y sin force push, integra `origin/main` en `feature/responsables-fase-018` mediante merge normal si la rama está detrás/divergente.
6. Si existe conflicto add/add en `.sonarcloud.properties`, resuélvelo conservando exactamente la configuración correcta y mínima:
   `sonar.cpd.exclusions=database/baseline/`
   No uses NOSONAR, no bajes el Quality Gate y no excluyas código productivo.
7. No modifiques lógica backend/frontend/DB ni tests funcionales, salvo que la integración de main lo exija por un conflicto real; si aparece un conflicto de lógica inesperado, detente y documenta.
8. Después del merge, ejecuta `git diff --check` y una verificación ligera del árbol. No repitas las suites completas porque la lógica funcional no cambia.
9. Haz commit del merge/resolución si corresponde y push normal a `feature/responsables-fase-018`. Nunca force push.
10. Verifica que el PR #31 vuelva a ser mergeable o al menos que GitHub ya no reporte conflicto de integración.
11. Revisa los checks del nuevo HEAD: GitHub Actions, Vercel y SonarQube Cloud.
12. Para Sonar:
    - confirma que el análisis corresponde al NUEVO HEAD y no a una medida stale;
    - confirma si `.sonarcloud.properties` fue aplicada;
    - si el Quality Gate queda verde, reporta el porcentaje final de duplicación nueva y detente;
    - si queda rojo todavía por duplicación, identifica qué archivos aparecen como duplicados y corrige únicamente configuración de análisis si la causa sigue siendo el baseline consolidado;
    - no hagas cambios cosméticos para engañar CPD;
    - si el análisis queda queued/pending por infraestructura, consulta solo unas pocas veces con espera razonable y luego documenta y detente. No hagas loops largos.
13. Actualiza `docs/handoffs/018-cierre-pr31.md` solo si hay un cambio de estado verificable importante (por ejemplo, Sonar verde, conflicto resuelto, porcentaje final, nuevo SHA).
14. No hagas merge del PR #31 a `main`.
15. No inicies Bloque 019.

## Criterio de salida
Termina cuando ocurra uno de estos casos:

### LISTO
- rama sincronizada con main;
- PR sin conflicto de merge;
- CI/Vercel correctos;
- Sonar completado para el nuevo HEAD;
- Quality Gate verde;
- handoff actualizado si corresponde.

### LISTO CON PENDIENTE EXTERNO
- rama sincronizada y PR sin conflicto;
- Sonar sigue queued/pending por infraestructura después de una espera razonable;
- no hay más trabajo local útil que hacer.

### BLOQUEADO
- aparece conflicto funcional/arquitectónico inesperado;
- Sonar requiere una configuración administrativa no disponible desde repo;
- hace falta escribir en producción/Supabase o usar secretos no autorizados.

## Reporte final por Telegram
Responde en español con:

ESTADO: LISTO / LISTO CON PENDIENTE EXTERNO / BLOQUEADO
PR: #31
HEAD: <sha>
MERGEABLE: sí/no
SONAR: <verde/rojo/pending + porcentaje de duplicación nueva si está disponible>
CI: <estado>
VERCEL: <estado>
CAMBIOS HECHOS: <resumen corto>
PENDIENTES: <solo los reales>
SIGUIENTE PASO: <qué debe revisar Axeell>

No pidas confirmaciones rutinarias. No mergees. No inicies 019.