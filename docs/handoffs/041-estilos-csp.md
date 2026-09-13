# 041 — Recuperar el diseño bajo CSP

- Agente: Codex.
- Fecha: 2026-09-13.
- Rama: `fix/estilos-csp`.
- Base: `main`, `8bd97dfec06342609460b67e91137f15b1d38b0b` (PR #94).

## Síntoma y evidencia

Después de confirmar el login con Google, el usuario mostró `/alumnos` con
botones e inputs nativos, tabla sin formato y barra superior ausente.

El build de la base genera, tanto en `index.csr.html` como en
`login/index.html`, el siguiente enlace:

```html
<link rel="stylesheet" href="styles-YWYFLI6L.css" media="print" onload="this.media='all'">
```

El mismo enlace se observó en el DOM desplegado de
`https://schoolmanager.nocpbx.com/login`. La CSP de `vercel.json` contiene
`script-src 'self'`, que bloquea ese handler inline; las capturas del usuario
mostraban precisamente ese bloqueo. El CSS global queda condicionado a impresión.
Los estilos de componentes pueden seguir presentes, lo que explica el aspecto
parcial del diseño. Los templates conservan sus clases `sm-*` y el CSS global
conserva los estilos de botones, inputs y tablas.

La atribución anterior a un despliegue antiguo no estaba verificada y queda
sustituida por este diagnóstico del artefacto generado.

## Corrección

- Desactivar solamente `optimization.styles.inlineCritical` en producción.
  Angular conserva minificación y hashing, y emite un enlace CSS normal que no
  necesita ejecutar JavaScript inline. La CSP se conserva.
- Completar el sustituto de `AuthService` usado exclusivamente durante SSG con
  `consumirMensajeSesionInvalida: () => null`, tipado mediante `Pick`.
  El objeto vacío anterior hacía fallar `Login.ngOnInit` y dejaba ambos botones
  sin texto en el HTML prerenderizado, aunque el comando de build terminaba con 0.
- Añadir pruebas del HTML generado después del build en el CI, además de los
  tests unitarios existentes.

Referencia de Angular para `inlineCritical`:
https://angular.dev/reference/configs/workspace-config#styles-optimization-options

## Validaciones locales

- Base sin corrección: fallan las dos pruebas de CSS por `media=print`.
- Con CSS corregido y antes de completar el sustituto SSG: falla la prueba de
  texto del botón de login (texto vacío).
- Corrección completa: `npm run build` finaliza sin errores Angular ni avisos de
  stylesheet ausente. npm muestra un aviso del entorno sobre `http-proxy`.
- `node --test scripts/build-styles.test.ts`: 3/3.
- `npm test -- --watch=false`: 323/323; el cambio posterior al run es únicamente
  el sustituto SSG, verificado mediante el build y la prueba de HTML.
- `git diff --check`: correcto.

## Límites y siguiente comprobación

El navegador remoto bloqueó la apertura del servidor local con
`ERR_BLOCKED_BY_CLIENT`. No se realizó una comprobación visual autenticada
del arreglo ni se repitió OAuth con una cuenta real en esta sesión.

En el preview del PR, comprobar `/login`, entrar con Google y visitar
`/alumnos`: verificar barra superior, botones, buscador y tabla. Recargar la
ruta y comprobar también el ancho móvil. El enlace `styles-*.css` debe carecer
de `media="print"` y de `onload`. Los avisos CSP del toolbar de Vercel, si aparecen,
son distintos del handler de carga de estilos corregido aquí.

Coste del cambio: el CSS global pasa a bloquear el primer pintado (9,56 kB sin
comprimir, estimación del build 2,25 kB transferidos); se prioriza que el diseño
completo se aplique bajo la política de seguridad existente.
