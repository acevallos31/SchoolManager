// PERF-07 — Escenarios de stress k6 reproducibles y sin secretos.
// El token se lee de env K6_TOKEN (NUNCA versionado). Escalones 50/100/250/500.
// Nota: el listado de alumnos del frontend va por Supabase; el endpoint
// /api/alumnos es el contrato .NET actual (stub de listado). El objeto de
// carga real y medible es /api/matriculas.

import http from 'k6/http';
import { check, sleep } from 'k6';
import { randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const TOKEN = __ENV.K6_TOKEN || '';
const CTX = __ENV.CTX_ID || ''; // alumnoId opcional para el filtro

// Se requiere token; si falta, los requests irán 401 (medimos la tasa de error).
if (!TOKEN && __ENV.EXPECT_AUTH !== '0') {
  console.warn('K6_TOKEN no definido; los endpoints auth=false y darán 401.');
}

const params = {
  headers: TOKEN ? { Authorization: `Bearer ${TOKEN}` } : {},
};

// Config por defecto: escalones 50 → 100 → 250 (→500 opcional con USERS_500=1).
const maxVus = __ENV.USERS_500 === '1' ? 500 : 250;
export const options = {
  scenarios: {
    rampa: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 50 },
        { duration: '20s', target: 100 },
        { duration: '20s', target: maxVus },
        { duration: '30s', target: maxVus }, // meseta
        { duration: '15s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'],       // <5% errores
    http_req_duration: ['p(95)<1500'],    // p95 < 1.5s
  },
};

export default function () {
  const alumnoId = CTX || `alumno-${randomIntBetween(1, 1000)}`;

  // 1) Listar alumnos paginado (endpoint .NET; si es stub devuelve Ok rápido).
  const rAlumnos = http.get(
    `${BASE_URL}/api/alumnos?page=1&pageSize=20&buscar=${__ENV.TERMINO || 'John'}`,
    params,
  );
  check(rAlumnos, { 'alumnos paginado 200/401': r => r.status === 200 || r.status === 401 });

  // 2) Buscar alumno.
  const rBuscar = http.get(
    `${BASE_URL}/api/alumnos?buscar=${__ENV.TERMINO || 'John'}`,
    params,
  );
  check(rBuscar, { 'buscar alumno 200/401': r => r.status === 200 || r.status === 401 });

  // 3) Listar matrículas paginado + filtro estado.
  const rMat = http.get(
    `${BASE_URL}/api/matriculas?page=1&pageSize=20&estado=pendiente&alumnoId=${alumnoId}`,
    params,
  );
  check(rMat, {
    'matrículas paginado 200': r => r.status === 200,
    'matrículas escribe paginado': r => {
      try {
        const b = r.json();
        return Array.isArray(b.items) && typeof b.totalItems === 'number';
      } catch (_e) {
        return false;
      }
    },
  });

  sleep(randomIntBetween(0.1, 0.5));
}