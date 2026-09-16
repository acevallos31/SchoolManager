# Handoff 043B — GitHub Actions sobre runtime Node 24

## Objetivo

Eliminar la dependencia temporal del modo de compatibilidad de GitHub que estaba
forzando actions basadas en Node.js 20 a ejecutarse con Node.js 24.

## Evidencia de la deuda

Los runs posteriores al Bloque 042 mostraban repetidamente:

`Node.js 20 is deprecated ... actions/checkout@v4, actions/setup-dotnet@v4, actions/setup-node@v4, actions/upload-artifact@v4 ...`

El job Sonar incluía además `actions/download-artifact@v4`.

## Cambio

Se actualizan únicamente actions oficiales de GitHub y se fijan por SHA al
commit exacto de su release verificada:

- `actions/checkout` → `3d3c42e5aac5ba805825da76410c181273ba90b1` (`v7.0.1`);
- `actions/setup-dotnet` → `a98b56852c35b8e3190ac28c8c2271da59106c68` (`v6.0.0`);
- `actions/setup-node` → `820762786026740c76f36085b0efc47a31fe5020` (`v7.0.0`);
- `actions/upload-artifact` → `043fb46d1a93c77aae656e7c1c64a875d1fc6a0a` (`v7.0.1`);
- `actions/download-artifact` → `3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c` (`v8.0.1`).

Las cinco releases declaran runtime `node24`. El Quality Gate de Sonar ya estaba
pineado por SHA y no se modifica.

## Alcance deliberadamente pequeño

No se cambian:

- comandos de build/test;
- versiones de .NET, Node o npm usadas por SchoolManager;
- secrets;
- condiciones de despliegue;
- cobertura ni thresholds;
- configuración Sonar;
- lógica de aplicación;
- base de datos o migraciones.

## Criterio de cierre

El PR solo puede considerarse cerrado si:

1. `git diff --check` pasa;
2. backend/API/DB/frontend pasan como antes;
3. artifacts de cobertura siguen subiendo/descargando correctamente;
4. SonarScanner + Quality Gate quedan verdes;
5. los logs ya no contienen `Node.js 20 is deprecated` para estas actions.

No mergear sin autorización explícita.
