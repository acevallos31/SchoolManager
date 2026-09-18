# Inventario SECURITY DEFINER — Bloque 049 / deuda #15

Fecha de inventario productivo: 2026-09-18.

## Conclusión arquitectónica

El frontend productivo ya no usa `.rpc()` de Supabase para lógica de negocio.
La frontera vigente es **Angular / móvil → API .NET → PostgreSQL/RPC**; Supabase
directo en clientes queda reservado a Auth.

El Security Advisor reportó **93** funciones `SECURITY DEFINER` ejecutables por
`authenticated`. El inventario de dependencias DB separó:

- **9 helpers `usuario_*` referenciados por RLS**: se conservan.
- **84 RPC `rpc_*` de negocio**: consumidores de aplicación son backend/API;
  046 revoca únicamente su EXECUTE directo a `authenticated`.
- `service_role` se conserva.
- no se modifica cuerpo, `SECURITY DEFINER`, RLS ni autorización interna de ninguna función.

Esto evita una revocación ciega: el criterio es consumidor real + dependencia RLS.

## Helpers preservados para RLS

1. `usuario_actual_id()`
2. `usuario_tiene_permiso_actual(text,uuid)`
3. `usuario_puede_ver_alumno(uuid)`
4. `usuario_puede_ver_institucion(uuid)`
5. `usuario_tiene_permiso_institucional_estricto(text,uuid)`
6. `usuario_puede_ver_ciclo(uuid)`
7. `usuario_puede_ver_matricula(uuid)`
8. `usuario_puede_ver_responsable(uuid)`
9. `usuario_tiene_permiso_en_algun_ambito(text)`

## RPC de negocio endurecidas por 046

1. `public.rpc_actualizar_ciclo_escolar(uuid,text,date,date,boolean,text)`
2. `public.rpc_actualizar_concepto_financiero(uuid,text,numeric,text,uuid)`
3. `public.rpc_actualizar_grado(uuid,text,integer,uuid)`
4. `public.rpc_actualizar_institucion(uuid,text,text,text,text,text,text,boolean,boolean,boolean,text[])`
5. `public.rpc_actualizar_jornada(uuid,text,uuid)`
6. `public.rpc_actualizar_multiples_instituciones(boolean)`
7. `public.rpc_actualizar_periodo_matricula(uuid,text,text,date,date,boolean)`
8. `public.rpc_actualizar_plan_pago(uuid,text,text,jsonb,uuid)`
9. `public.rpc_actualizar_seccion(uuid,uuid,uuid,uuid,text,integer,uuid)`
10. `public.rpc_anular_cargo(uuid,text,uuid)`
11. `public.rpc_anular_pago(uuid,text,uuid)`
12. `public.rpc_asignar_plan_pago_matricula(uuid,uuid,uuid)`
13. `public.rpc_asignar_rol_institucional(uuid,uuid)`
14. `public.rpc_asignar_rol_usuario(uuid,text,uuid)`
15. `public.rpc_cambiar_estado_grado(uuid,boolean,uuid)`
16. `public.rpc_cambiar_estado_jornada(uuid,boolean,uuid)`
17. `public.rpc_cambiar_estado_matricula(uuid,text,text)`
18. `public.rpc_cargos_responsable(uuid,uuid)`
19. `public.rpc_clonar_plantilla_rol(uuid,text,text,text,text)`
20. `public.rpc_crear_alumno_nueva_persona(uuid,text,text,date,text,text)`
21. `public.rpc_crear_alumno_nueva_persona_con_documento(uuid,text,text,text,text,date,text,text)`
22. `public.rpc_crear_alumno_para_persona(uuid,uuid,date,text,text)`
23. `public.rpc_crear_ciclo_escolar(text,date,date,uuid)`
24. `public.rpc_crear_concepto_financiero(text,numeric,text,uuid)`
25. `public.rpc_crear_grado(text,integer,uuid)`
26. `public.rpc_crear_institucion(text,text,text,text,text,text,boolean,boolean,boolean,text[])`
27. `public.rpc_crear_jornada(text,uuid)`
28. `public.rpc_crear_periodo_matricula(uuid,text,text,date,date)`
29. `public.rpc_crear_plan_pago(text,text,jsonb,uuid)`
30. `public.rpc_crear_responsable_con_documento(uuid,text,text,text,text,text,text)`
31. `public.rpc_crear_responsable_para_persona(uuid,uuid)`
32. `public.rpc_crear_rol_institucional(uuid,text,text,text)`
33. `public.rpc_crear_seccion(uuid,uuid,uuid,uuid,text,integer)`
34. `public.rpc_desactivar_alumno(uuid,text)`
35. `public.rpc_desactivar_ciclo_escolar(uuid,text)`
36. `public.rpc_desactivar_concepto_financiero(uuid,text,uuid)`
37. `public.rpc_desactivar_grado(uuid,uuid)`
38. `public.rpc_desactivar_jornada(uuid,uuid)`
39. `public.rpc_desactivar_periodo_matricula(uuid)`
40. `public.rpc_desactivar_plan_pago(uuid,text,uuid)`
41. `public.rpc_desactivar_rol_institucional(uuid,text)`
42. `public.rpc_desactivar_rol_usuario(uuid,text)`
43. `public.rpc_desactivar_seccion(uuid,text,uuid)`
44. `public.rpc_desactivar_vinculo_responsable(uuid,text)`
45. `public.rpc_editar_responsable(uuid,text,text,text,text)`
46. `public.rpc_editar_rol_institucional(uuid,text,text)`
47. `public.rpc_editar_vinculo_responsable(uuid,text,boolean,boolean)`
48. `public.rpc_generar_cargos_matricula(uuid,uuid)`
49. `public.rpc_inactivar_responsable(uuid,text)`
50. `public.rpc_listar_cargos_alumno(uuid,uuid)`
51. `public.rpc_listar_cargos_matricula(uuid,uuid)`
52. `public.rpc_listar_ciclos_escolares(uuid)`
53. `public.rpc_listar_conceptos_financieros(uuid,boolean)`
54. `public.rpc_listar_grados(uuid)`
55. `public.rpc_listar_jornadas(uuid)`
56. `public.rpc_listar_pagos_alumno(uuid,uuid)`
57. `public.rpc_listar_periodos_matricula(uuid)`
58. `public.rpc_listar_planes_pago(uuid,boolean)`
59. `public.rpc_listar_secciones(uuid,uuid)`
60. `public.rpc_matricular_alumno(uuid,uuid,uuid)`
61. `public.rpc_mis_alumnos_responsable()`
62. `public.rpc_obtener_aplicaciones_pago(uuid,uuid)`
63. `public.rpc_obtener_configuracion_institucion(uuid)`
64. `public.rpc_obtener_contexto_implementacion()`
65. `public.rpc_obtener_pago(uuid,uuid)`
66. `public.rpc_obtener_plan_pago(uuid,uuid)`
67. `public.rpc_obtener_seguridad_acceso(uuid)`
68. `public.rpc_pago_aplicaciones_responsable(uuid,uuid)`
69. `public.rpc_pagos_responsable(uuid,uuid)`
70. `public.rpc_reactivar_alumno(uuid)`
71. `public.rpc_reactivar_ciclo_escolar(uuid)`
72. `public.rpc_reactivar_concepto_financiero(uuid,uuid)`
73. `public.rpc_reactivar_grado(uuid,uuid)`
74. `public.rpc_reactivar_jornada(uuid,uuid)`
75. `public.rpc_reactivar_periodo_matricula(uuid)`
76. `public.rpc_reactivar_plan_pago(uuid,uuid)`
77. `public.rpc_reactivar_responsable(uuid)`
78. `public.rpc_reactivar_seccion(uuid,uuid)`
79. `public.rpc_reactivar_vinculo_responsable(uuid)`
80. `public.rpc_reemplazar_permisos_rol_institucional(uuid,text[])`
81. `public.rpc_registrar_pago(uuid,jsonb,numeric,uuid,uuid,text,text,timestamp with time zone)`
82. `public.rpc_resumen_financiero_alumno(uuid,uuid)`
83. `public.rpc_resumen_financiero_responsable(uuid,uuid)`
84. `public.rpc_vincular_alumno_responsable(uuid,uuid,text,boolean,boolean)`

## Validación de cierre

La validación 046 exige simultáneamente:

- cero `public.rpc_*` SECURITY DEFINER ejecutables por `authenticated`;
- los 9 helpers RLS continúan ejecutables;
- todas las RPC conservan ejecución para `service_role`;
- CI DB reconstruye desde cero y aplica 046.

Después de aplicar 046 en producción se debe volver a ejecutar Security Advisor;
el warning `authenticated_security_definer_function_executable` debe reducirse
de 93 a las 9 excepciones justificadas de RLS.
