# Prompt operativo — Bloque 028: cierre UX final

## Objetivo
Cerrar formalmente el rediseño UI/UX de SchoolManager después de los Bloques 024–027 mediante una auditoría transversal del frontend, correcciones UX pequeñas y seguras, revisión responsive/accesibilidad básica, limpieza de residuos visuales y documentación final.

Este es un bloque de **cierre y estabilización**, no un bloque funcional nuevo.

## Lectura obligatoria antes de tocar código
Leer completos, en este orden:
1. `AGENTS.md`
2. `docs/AI_CONTEXT.md`
3. `docs/engineering-principles.md`
4. `docs/ui/design-system.md`
5. `docs/handoffs/024-ui-ux-foundation.md`
6. `docs/handoffs/025-ui-ux-matriculas-responsables-configuracion.md`
7. `docs/handoffs/026-ui-ux-finanzas.md`
8. `docs/handoffs/027-ui-ux-portal-responsable.md`
9. este prompt.

El repositorio es la fuente de verdad. No reconstruir estado desde conversaciones anteriores.

## Estado base esperado
- PR #50 / Bloque 027 ya mergeado a `main`.
- Merge commit de 027: `ba3c78fa19389d2d27ce31948bdb2a14bf6dcfe8`.
- Rama de trabajo ya creada: `feature/ui-ux-cierre-028`.
- `docs/AI_CONTEXT.md` ya actualizado para marcar 028 como activo.

Antes de editar:
- `git fetch`;
- confirmar rama `feature/ui-ux-cierre-028`;
- confirmar working tree limpio o identificar únicamente cambios ya pertenecientes a 028;
- revisar `git status` y últimos commits;
- verificar que el commit `6397d1f30538ece4f0ef1ec46dcc422788d76bd6` o su contenido equivalente de contexto esté presente;
- no resetear ni descartar trabajo ajeno.

## Alcance permitido
Auditar y, cuando exista evidencia, corregir únicamente presentación/UX frontend en:
- AppShell;
- Dashboard;
- Alumnos;
- Matrículas;
- Responsables;
- Configuración raíz;
- Ciclos;
- Estructura académica;
- Conceptos financieros;
- Planes de pago;
- Cargos;
- Pagos;
- Portal Responsable `/portal-padre`;
- Login solo si aparece un defecto visual/transversal claro dentro del mismo design system;
- `src/styles.css` / foundation compartida cuando una corrección sea genuinamente reusable;
- tests frontend relacionados;
- documentación de cierre.

## Fuera de alcance — NO tocar
- backend;
- base de datos;
- migraciones;
- RPC;
- RLS/RBAC;
- permisos;
- contratos API;
- lógica financiera;
- reglas académicas;
- autenticación funcional;
- deuda técnica #10 de acceso directo a Supabase;
- infraestructura Render/Vercel/Supabase salvo validación read-only normal;
- dependencias nuevas salvo bloqueo real e imposible de resolver con lo existente;
- rediseños grandes o nuevas funcionalidades.

Si un defecto UX solo puede corregirse cambiando comportamiento de negocio, contrato o backend, documentarlo y dejarlo fuera de 028.

## Principio de trabajo
No “rediseñar otra vez”. La foundation `sm-*` ya es la identidad final.

La tarea consiste en detectar inconsistencias reales y resolverlas con el cambio mínimo:
- tokens `--sm-*`;
- primitivas `.sm-*` existentes;
- HTML semántico;
- CSS local solo para layout específico;
- Angular actual y tipado estricto.

No crear primitivas nuevas por gusto. Extraer una primitive global solo si el patrón existe realmente en dos o más lugares o resuelve una inconsistencia transversal inmediata.

## Fase 0 — inventario y auditoría
Antes de cambiar archivos, levantar un inventario breve de las pantallas y revisar:
- jerarquía de títulos (`sm-page-title`, `sm-section-title`, `sm-eyebrow`);
- botones y tamaños;
- cards;
- tablas y `sm-table-wrap`;
- badges por estado;
- inputs/selects/textareas;
- modales;
- tabs;
- alertas;
- loading;
- empty state;
- error state;
- alineaciones y spacing;
- responsive;
- foco visible;
- labels/aria;
- CSS duplicado o valores visuales hardcodeados.

Usar búsquedas dirigidas en vez de releer repetidamente archivos grandes.

## Fase 1 — consistencia visual
Corregir únicamente hallazgos concretos como:
- títulos con jerarquía distinta sin justificación;
- botones equivalentes usando estilos distintos;
- tablas sin wrapper responsive;
- estados funcionalmente iguales renderizados de forma diferente;
- badges inconsistentes para el mismo estado;
- inputs que no usan primitives existentes;
- cards con bordes/sombras/spacing locales innecesarios;
- layout que rompe la foundation.

No buscar uniformidad artificial cuando la diferencia tenga razón funcional.

## Fase 2 — responsive final
Verificar al menos tres anchos representativos:
- desktop;
- tablet;
- móvil.

Prioridades:
- AppShell y drawer sin overflow;
- overlays correctos;
- contenido no oculto detrás de topbar/sidebar;
- cards/KPIs que reflow correctamente;
- formularios utilizables;
- botones no cortados;
- tablas con scroll horizontal contenido y sin overflow global;
- modales visibles en viewport pequeño;
- textos/montos/identificadores largos no rompen layout;
- Portal Responsable usable sin AppShell.

Si no hay E2E autenticado disponible, usar revisión estática con templates/CSS reales y datos representativos solo para inspección visual, documentándolo como tal.

## Fase 3 — accesibilidad básica
Sin convertir 028 en una auditoría WCAG completa, revisar y corregir:
- `label` asociado a control mediante `for/id` cuando aplique;
- botones reales para acciones;
- controles solo-icono con nombre accesible;
- `aria-label` únicamente cuando el texto visible no sea suficiente;
- `role="alert"` / `status` donde corresponda;
- foco visible;
- orden razonable de headings;
- modales con semántica adecuada si existen;
- no eliminar atributos accesibles ya existentes;
- evitar dependencia exclusiva de color para comunicar estado cuando sea evidente.

No agregar ARIA redundante o incorrecta.

## Fase 4 — limpieza CSS
Buscar de forma dirigida:
- colores hex/rgb locales fuera de tokens;
- sombras/radius/spacing hardcodeados que deberían venir de `--sm-*`;
- `!important`;
- `style="..."`;
- primitivas duplicadas;
- media queries inconsistentes;
- reglas muertas evidentes ligadas al rediseño anterior.

No hacer limpieza masiva ni reformatear archivos completos sin necesidad.
No borrar CSS si no se puede demostrar que está muerto o reemplazado.

## Tests
Conservar todos los tests existentes.
Agregar o ajustar tests solo cuando una corrección afecte comportamiento observable de UX, por ejemplo:
- drawer abre/cierra;
- render condicional de estados;
- accesibilidad básica que ya se verifica por DOM;
- cambios de bindings necesarios para corregir labels/aria.

No crear tests de valores CSS exactos, colores o píxeles.
No debilitar tests funcionales para facilitar el cambio visual.

## Validación local obligatoria
Desde `frontend/schoolmanager-frontend`:
- ejecutar tests relevantes durante cada checkpoint;
- ejecutar suite FE completa al cierre;
- ejecutar production build;
- ejecutar coverage gate si el repo lo tiene en scripts/CI.

Además:
- `git diff --check`;
- revisar warnings y distinguir nuevos vs preexistentes;
- revisar el diff completo antes de commit final.

No declarar completo con tests/build requeridos fallando.

## Revisión visual
Hacer revisión desktop/tablet/móvil de todas las superficies razonablemente accesibles.

Si el entorno permite navegador real con sesión/datos seguros, usarlo.
Si no hay credenciales o staging apropiado:
- no inventar credenciales;
- no usar producción para escrituras;
- usar revisión estática de layout con componentes/templates/CSS reales;
- documentar claramente el alcance de la revisión y el riesgo residual.

## Documentación obligatoria
Crear:
- `docs/handoffs/028-ui-ux-cierre-final.md`

Actualizar al final:
- `docs/AI_CONTEXT.md` para marcar 028 completo solo cuando sea real;
- `docs/ui/design-system.md` para reflejar cierre/adopción total y únicamente cambios reales;
- `AGENTS.md` solo si existe una regla operativa nueva verdaderamente necesaria; no modificarlo por rutina.

El handoff debe incluir:
- rama y HEAD;
- objetivo;
- hallazgos;
- archivos cambiados;
- correcciones realizadas;
- validaciones con números/resultados;
- modalidad de revisión visual;
- riesgos residuales;
- qué quedó explícitamente fuera de alcance.

## Commits
Autorizado hacer commits y push en `feature/ui-ux-cierre-028`.
Usar Conventional Commits en español y commits pequeños/coherentes.

Orden sugerido, solo si el trabajo real lo justifica:
1. `fix(ui): corrige inconsistencias finales de UX`
2. `test(ui): cubre ajustes finales de experiencia`
3. `docs: cerrar rediseño UI UX`

No crear commits vacíos solo para seguir este orden.

## Git / PR
Al finalizar correctamente:
- push normal de `feature/ui-ux-cierre-028`;
- abrir PR contra `main`;
- dejarlo Ready for review;
- incluir resumen de hallazgos/correcciones/validaciones/riesgos;
- esperar GitHub Actions, SonarCloud y Vercel;
- si alguno falla por el cambio, investigar y corregir dentro de alcance, volver a push y validar;
- si el fallo es externo/preexistente, documentarlo con evidencia.

**NO mergear el PR.** El merge final queda reservado para revisión humana/ChatGPT con autorización explícita.

## Regla de parada
Detenerse únicamente ante una condición real de `AGENTS.md` sección 5 o si el arreglo exige salir del alcance 028.

No detenerse por:
- warning solucionable dentro de frontend;
- test roto por un cambio propio dentro de alcance;
- necesidad de ajustar CSS/HTML/tests/docs de 028;
- CI/Sonar que señale un defecto corregible del propio PR.

Corregir, revalidar y continuar.

## Protección de contexto
Si el contexto se acerca al límite:
- cerrar el checkpoint actual;
- ejecutar tests relevantes;
- revisar diff;
- commit y push si el estado es coherente;
- actualizar el handoff 028 con lo hecho y lo pendiente;
- detener la sesión limpiamente.

No compactar repetidamente una sesión enorme si el handoff ya permite continuar.

## Criterio de terminado
028 solo está terminado cuando:
- la auditoría transversal fue realizada;
- los hallazgos dentro de alcance fueron resueltos o documentados justificadamente;
- no quedan páginas del rediseño pendientes de adoptar foundation;
- suite FE completa verde;
- build de producción verde;
- coverage gate requerido verde;
- `git diff --check` verde;
- no hay warnings nuevos sin explicar;
- revisión visual desktop/tablet/móvil completada en modalidad documentada;
- documentación final actualizada;
- PR abierto contra `main`;
- checks remotos revisados.

## Reporte final a Telegram/consola
Mantenerlo breve e incluir:
- `Bloque 028 listo` o `Bloque 028 bloqueado`;
- rama;
- PR y HEAD si existe;
- número de tests FE y resultado;
- build;
- CI/Sonar/Vercel;
- cantidad/resumen de hallazgos corregidos;
- modalidad de revisión visual;
- riesgos residuales;
- confirmación explícita: `NO MERGE REALIZADO`.
