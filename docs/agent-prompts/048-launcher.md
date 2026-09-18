# Hermes launcher — 048 overnight

Use this file as the **execution prompt**. The longer document
`docs/agent-prompts/048-smart-night-auth-debt-close.md` is reference material only.

## Rules

- Branch: `chore/048-night-auth-debt-close`
- Work continuously for the rest of the night in Smart mode.
- Use **low reasoning**. Prefer execution over analysis.
- Do not spend more than 10 minutes on reconnaissance.
- Do not merge.
- Do not change production DB/data.
- Do not run load tests against production.
- Do not expose secrets.
- Do not auto-link identities by email.
- Keep Google/Microsoft OAuth working.
- Backend owns business logic; preserve SOLID/ACID/RBAC/RLS.

## Phase 1 — Fix Demo/auth first

Known production evidence:

- `demo@schoolmanager.com` exists in `auth.users`.
- auth id: `a6a3d715-2418-4fb5-b95c-072e0ce216cf`.
- matching `public.usuarios` is active.
- `public.usuarios.auth_user_id` already equals that auth id.
- an active institutional role assignment exists.
- UI still reports `IDENTIDAD_NO_VINCULADA`.

Do NOT rewrite that linkage.

Trace the real path:
`Supabase Auth -> Vercel session sync -> JWT -> /api/auth/me -> UsuarioActualService -> frontend error mapping`.

Check especially:
- stale/persisted invalid-session message;
- wrong JWT/sub reaching backend;
- frontend restoring an old session;
- production API using different DB/project configuration;
- semantic confusion between no identity and no applicable role.

Create regression tests, implement the smallest verified fix, run relevant tests, commit and push.

## Phase 2 — Pending identity approval

Implement the missing administrative flow in Configuración > Seguridad y acceso:

- distinguish Account Active/Inactive from Identity Linked/Pending;
- authorized operator can review a pending external identity;
- explicit approve/reject;
- approval reuses `public.vincular_identidad_usuario(usuario_id, auth_user_id)`;
- never link automatically by email;
- audit the action;
- backend authorization;
- Angular only presents/orchestrates.

If DB changes are needed, create versioned migration + validation + rollback + tests, but do NOT apply to production.

Commit and push.

## Phase 3 — Technical debt #15 / issue #109

Audit SECURITY DEFINER RPCs exposed to `authenticated` in small batches.

Create/update a versioned inventory with:
- function signature;
- real consumer;
- current grant;
- classification;
- risk;
- action.

Classifications:
- KEEP_AUTHENTICATED
- API_ONLY_REVOKE_AUTHENTICATED
- CONVERT_SECURITY_INVOKER
- JUSTIFIED_EXCEPTION

For API-only RPCs, prepare safe incremental hardening with tests. Never mass revoke. Do not apply migrations to production.

Commit and push after coherent batches.

## Phase 4 — Technical debt #14 / issue #85

Do NOT load test production.

Use local/staging safe infrastructure only. Prepare or execute safe baseline for:
- /health
- /health/ready
- one authenticated read-only API route

Record p50/p95/p99, throughput and errors when possible.

If production measurement is still required for canonical closure, leave the runbook ready and mark that specific part blocked by authorization. Do not fake closure.

## Quality

Before final handoff run:

- `git diff --check`
- backend Release build
- API integration tests
- DB integration tests
- frontend tests
- frontend build
- Vercel tests
- relevant E2E if safe
- Sonar/CI if PR is opened

Do not lower thresholds or add exclusions.

## Checkpoint discipline

Every 60–90 minutes, or before tool/token limits:

1. update `docs/agent-runs/048-night-checkpoint.md`;
2. include current phase, SHA, tests, blocker and next exact action;
3. commit + push;
4. continue.

If the agent/runtime cannot continue, the final response must contain one exact resume command/message and must NOT claim completion.

## Final deliverable

Update:
- `docs/technical-debt.md`
- `docs/HANDOFF.md`
- `docs/AI_CONTEXT.md`
- `docs/handoffs/048-auth-debt-night-close.md`

Report:
- root cause Demo/auth;
- fix + tests + SHA;
- pending identity approval status;
- #15 audit/hardening status;
- #14 safe load-test status;
- all test counts;
- final SHA and PR.

End with exactly:

```
MERGE: NO
PRODUCTION DB CHANGES: NO
PRODUCTION DATA CHANGES: NO
PRODUCTION LOAD TEST: NO
```
