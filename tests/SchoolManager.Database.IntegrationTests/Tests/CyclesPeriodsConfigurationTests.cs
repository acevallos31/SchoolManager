using Npgsql; using SchoolManager.Database.IntegrationTests.Infrastructure; using Xunit;
namespace SchoolManager.Database.IntegrationTests.Tests;
public sealed class CyclesPeriodsConfigurationTests(PostgreSqlFixture f):IClassFixture<PostgreSqlFixture>{
 [Fact] public async Task Ciclos_y_periodos_respetan_contexto_fechas_permisos_e_historico(){
  var inst=await G("insert into public.instituciones(nombre) values('Centro') returning id");var admin=await User("admin");var user=await User("usuario",inst);
  var ciclo=await Auth<Guid>(admin,"select public.rpc_crear_ciclo_escolar('Año lectivo 2026','2026-01-01','2026-12-31')");
  Assert.Equal(inst,await S<Guid>("select institucion_id from public.ciclos_escolares where id=$1",ciclo));
  await Assert.ThrowsAsync<PostgresException>(()=>Auth<Guid>(user,"select public.rpc_crear_ciclo_escolar('No','2026-01-01','2026-12-31',$1)",inst));
  await Assert.ThrowsAsync<PostgresException>(()=>Auth<Guid>(admin,"select public.rpc_crear_ciclo_escolar('Mal','2026-12-31','2026-01-01')"));
  await Auth<object>(admin,"select public.rpc_actualizar_ciclo_escolar($1,'2026 actualizado','2026-01-01','2026-12-31',true)",ciclo);
  var periodo=await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Ordinaria','regular','2026-01-10','2026-02-10')",ciclo);
  Assert.Equal(ciclo,await S<Guid>("select ciclo_id from public.periodos_matricula where id=$1",periodo));
  var anticipado=await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Anticipada','anticipada','2025-09-01','2025-10-31')",ciclo);Assert.NotEqual(Guid.Empty,anticipado);
  Assert.NotEqual(Guid.Empty,await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Normal','normal','2025-11-01','2025-12-31')",ciclo));
  Assert.NotEqual(Guid.Empty,await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Extraordinaria','extraordinaria','2026-01-15','2026-02-15')",ciclo));
  Assert.NotEqual(Guid.Empty,await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Tardia','tardía','2026-02-01','2026-03-01')",ciclo));
  Assert.NotEqual(Guid.Empty,await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Reingreso','reingreso','2025-12-15','2026-01-20')",ciclo));
  Assert.NotEqual(Guid.Empty,await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Traslado','traslado','2026-01-10','2026-01-25')",ciclo));
  await Assert.ThrowsAsync<PostgresException>(()=>Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Rango invalido',null,'2026-02-10','2026-02-01')",ciclo));
  await Auth<object>(admin,"select public.rpc_actualizar_periodo_matricula($1,'Anticipada ajustada','personalizado','2025-08-01','2025-10-15',true)",anticipado);
  await Auth<object>(admin,"select public.rpc_actualizar_ciclo_escolar($1,'2026 ajustado','2026-02-01','2026-11-30',true)",ciclo);
  await Assert.ThrowsAsync<PostgresException>(()=>Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Ordinaria',null,'2026-03-01','2026-03-10')",ciclo));
  var ciclo2=await Auth<Guid>(admin,"select public.rpc_crear_ciclo_escolar('2027','2027-01-01','2027-12-31')");
  Assert.NotEqual(Guid.Empty,await Auth<Guid>(admin,"select public.rpc_crear_periodo_matricula($1,'Ordinaria',null,'2027-01-01','2027-02-01')",ciclo2));
  await Auth<object>(admin,"select public.rpc_desactivar_periodo_matricula($1)",periodo);Assert.False(await S<bool>("select activo from public.periodos_matricula where id=$1",periodo));
  await Auth<object>(admin,"select public.rpc_desactivar_ciclo_escolar($1,'Cierre')",ciclo);Assert.Equal(1L,await S<long>("select count(*) from public.ciclos_escolares where id=$1 and not activo",ciclo));
  await E("update public.configuracion_implementacion set multiples_instituciones=true where id=1");var ex=await Assert.ThrowsAsync<PostgresException>(()=>Auth<long>(admin,"select count(*) from public.rpc_listar_ciclos_escolares()"));Assert.Equal("SM003",ex.SqlState);
  ex=await Assert.ThrowsAsync<PostgresException>(()=>Auth<long>(admin,"select count(*) from public.rpc_listar_periodos_matricula($1)",ciclo));Assert.Equal("SM003",ex.SqlState);
 }
 async Task<Guid> User(string role,Guid? inst=null){var p=await G("insert into personas(nombres,apellidos) values('U','X') returning id");var a=Guid.NewGuid();var u=await S<Guid>("insert into usuarios(persona_id,auth_user_id) values($1,$2) returning id",p,a);var r=await S<Guid>("select id from roles where codigo=$1",role);if(inst.HasValue)await E("insert into usuarios_roles(usuario_id,rol_id,institucion_id) values($1,$2,$3)",u,r,inst.Value);else await E("insert into usuarios_roles(usuario_id,rol_id) values($1,$2)",u,r);return a;}
 Task<Guid> G(string s)=>S<Guid>(s);async Task E(string s,params object[]v){await using var c=f.DataSource.CreateCommand(s);A(c,v);await c.ExecuteNonQueryAsync();}async Task<T>S<T>(string s,params object[]v){await using var c=f.DataSource.CreateCommand(s);A(c,v);return(T)(await c.ExecuteScalarAsync())!;}
 async Task<T>Auth<T>(Guid a,string s,params object[]v){await using var c=await f.DataSource.OpenConnectionAsync();await using var t=await c.BeginTransactionAsync();try{await new NpgsqlCommand("set local role authenticated",c,t).ExecuteNonQueryAsync();var q=new NpgsqlCommand("select set_config('request.jwt.claim.sub',$1,true)",c,t);q.Parameters.AddWithValue(a.ToString());await q.ExecuteNonQueryAsync();await using var x=new NpgsqlCommand(s,c,t);A(x,v);var z=await x.ExecuteScalarAsync();await t.CommitAsync();return z is null or DBNull?default!:(T)z;}catch{await t.RollbackAsync();throw;}}
 static void A(NpgsqlCommand c,IEnumerable<object>v){foreach(var x in v)c.Parameters.AddWithValue(x);}
}
