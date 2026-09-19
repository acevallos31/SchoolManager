import { DocumentoImprimible } from './documento-imprimible';
import type { LienzoPdf } from './lienzo-pdf';
import type { EstadoCuenta } from '../services/estado-cuenta.service';

/**
 * Documento concreto del estado de cuenta del alumno (047B).
 *
 * SOLO presentación: toma el DTO autoritativo que compone el backend
 * (EstadoCuenta) y lo dibuja en PDF (a través del puerto LienzoPdf) o lo
 * expone como HTML autocontenido. No recalcula saldos ni decide estados.
 */
export class EstadoCuentaDocumento extends DocumentoImprimible {
  private readonly estado: EstadoCuenta;

  constructor(estado: EstadoCuenta) {
    super(
      `estado-cuenta-${estado.alumno.codigoInterno ?? estado.alumno.id}.pdf`,
      'Estado de cuenta',
      {
        nombre: estado.institucion?.nombre ?? '',
        nombreCorto: estado.institucion?.nombreCorto ?? null,
        direccion: estado.institucion?.direccion ?? null,
        telefono: estado.institucion?.telefono ?? null,
        correo: estado.institucion?.correo ?? null,
        logoUrl: estado.institucion?.logoUrl ?? null,
      },
    );
    this.estado = estado;
  }

  renderizarPdf(lienzo: LienzoPdf): void {
    const margen = 15;
    const ancho = lienzo.getWidth();
    const anchoUtil = ancho - margen * 2;
    const xDerecha = ancho - margen;
    let y = this.crearEncabezadoPdf(lienzo, margen);

    // Título y alumno.
    lienzo.setFont('bold');
    lienzo.setFontSize(13);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text('ESTADO DE CUENTA', margen, y);
    y += 7;

    lienzo.setFont('normal');
    lienzo.setFontSize(9);
    lienzo.setTextColor(51, 65, 85);
    lienzo.text(`Alumno: ${this.estado.alumno.nombreCompleto}`, margen, y);
    y += 5;

    const identidad = [
      this.estado.alumno.rne ? `RNE: ${this.estado.alumno.rne}` : null,
      this.estado.alumno.codigoInterno ? `Código: ${this.estado.alumno.codigoInterno}` : null,
    ].filter(Boolean).join(' · ');
    if (identidad) {
      lienzo.text(identidad, margen, y);
      y += 5;
    }

    // Resumen financiero (totales autoritativos del backend).
    y += 3;
    lienzo.setFont('bold');
    lienzo.setFontSize(9);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text('Resumen', margen, y);
    y += 5;
    lienzo.setFont('normal');
    lienzo.setTextColor(51, 65, 85);
    const resumen = this.estado.resumen;
    const lineasResumen = [
      `Obligaciones: ${resumen.totalObligaciones}`,
      `Monto original: ${this.formatoMoneda(resumen.totalMontoOriginal)}`,
      `Pendiente: ${this.formatoMoneda(resumen.totalPendiente)}`,
      `Vencido: ${this.formatoMoneda(resumen.totalVencido)}`,
      `Aplicado: ${this.formatoMoneda(resumen.totalAplicado)}`,
      `Anulado: ${this.formatoMoneda(resumen.totalAnulado)}`,
    ];
    for (const linea of lineasResumen) {
      lienzo.text(linea, margen, y);
      y += 4.5;
    }

    // Detalle de cargos.
    y += 3;
    lienzo.setFont('bold');
    lienzo.setFontSize(9);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text('Cargos', margen, y);
    y += 2;
    lienzo.setDrawColor(203, 213, 225);
    lienzo.line(margen, y, xDerecha, y);
    y += 5;

    lienzo.setFont('normal');
    lienzo.setTextColor(51, 65, 85);
    const anchoConcepto = Math.max(40, anchoUtil - 60);
    for (const cargo of this.estado.cargos) {
      const lineas = lienzo.splitTextToSize(cargo.conceptoNombre || 'Cargo', anchoConcepto);
      lienzo.text(lineas, margen, y);
      const monto = this.formatoMoneda(cargo.montoOriginal);
      lienzo.text(monto, xDerecha - lienzo.getTextWidth(monto), y);
      y += lineas.length * 4.5 + 1.5;
    }
    if (this.estado.cargos.length === 0) {
      lienzo.text('Sin cargos registrados.', margen, y);
      y += 5;
    }

    // Histórico de pagos.
    y += 3;
    lienzo.setFont('bold');
    lienzo.setFontSize(9);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text('Pagos', margen, y);
    y += 2;
    lienzo.setDrawColor(203, 213, 225);
    lienzo.line(margen, y, xDerecha, y);
    y += 5;

    lienzo.setFont('normal');
    lienzo.setTextColor(51, 65, 85);
    for (const pago of this.estado.pagos) {
      const linea = `N° ${pago.numeroRecibo} · ${this.formatoFecha(pago.fechaPago)} · ${pago.metodoPago ?? '—'}`;
      lienzo.text(linea, margen, y);
      const monto = this.formatoMoneda(pago.montoTotal);
      lienzo.text(monto, xDerecha - lienzo.getTextWidth(monto), y);
      y += 5;
    }
    if (this.estado.pagos.length === 0) {
      lienzo.text('Sin pagos registrados.', margen, y);
      y += 5;
    }
  }

  renderizarHtml(): string {
    const e = (v: unknown) => this.escaparHtml(v);
    const contacto = [
      this.institucion.direccion,
      this.institucion.telefono,
      this.institucion.correo,
    ].filter((v): v is string => !!v).map(e).join(' · ');

    const resumen = this.estado.resumen;
    const filasCargos = this.estado.cargos.length
      ? this.estado.cargos.map((c) => `
          <tr>
            <td>${e(c.conceptoNombre || 'Cargo')}</td>
            <td class="num">${e(this.formatoMoneda(c.montoOriginal))}</td>
            <td class="num">${e(this.formatoMoneda(c.saldo))}</td>
            <td>${e(c.fechaVencimiento)}</td>
            <td>${e(c.estado)}</td>
          </tr>`).join('')
      : '<tr><td colspan="5" class="vacio">Sin cargos registrados.</td></tr>';

    const filasPagos = this.estado.pagos.length
      ? this.estado.pagos.map((p) => `
          <tr>
            <td>N° ${e(p.numeroRecibo)}</td>
            <td>${e(this.formatoFecha(p.fechaPago))}</td>
            <td>${e(p.metodoPago ?? '—')}</td>
            <td class="num">${e(this.formatoMoneda(p.montoTotal))}</td>
            <td>${e(p.estado)}</td>
          </tr>`).join('')
      : '<tr><td colspan="5" class="vacio">Sin pagos registrados.</td></tr>';

    const identidad = [
      this.estado.alumno.rne ? `RNE: ${e(this.estado.alumno.rne)}` : null,
      this.estado.alumno.codigoInterno ? `Código: ${e(this.estado.alumno.codigoInterno)}` : null,
    ].filter((v): v is string => !!v).join(' · ');

    return `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8" />
<title>${e(this.titulo)}</title>
<style>
${this.estilosHtml(`
  .resumen { display: flex; flex-wrap: wrap; gap: 8px 24px; margin: 0 0 16px; font-size: 13px; }
  .resumen strong { display: block; font-size: 15px; }
  table { margin-top: 8px; }
  h3 { font-size: 14px; margin: 16px 0 0; }
  `)}
</style>
</head>
<body>
  <header>
    <h1>${e(this.institucion.nombre || 'Institución')}</h1>
    ${contacto ? `<p class="contacto">${contacto}</p>` : ''}
  </header>
  <h2>ESTADO DE CUENTA</h2>
  <p class="meta">
    Alumno: ${e(this.estado.alumno.nombreCompleto)}
    ${identidad ? ` · ${identidad}` : ''}
  </p>
  <div class="resumen">
    <div>Obligaciones<strong>${e(resumen.totalObligaciones)}</strong></div>
    <div>Monto original<strong>${e(this.formatoMoneda(resumen.totalMontoOriginal))}</strong></div>
    <div>Pendiente<strong>${e(this.formatoMoneda(resumen.totalPendiente))}</strong></div>
    <div>Vencido<strong>${e(this.formatoMoneda(resumen.totalVencido))}</strong></div>
    <div>Aplicado<strong>${e(this.formatoMoneda(resumen.totalAplicado))}</strong></div>
    <div>Anulado<strong>${e(this.formatoMoneda(resumen.totalAnulado))}</strong></div>
  </div>
  <h3>Cargos</h3>
  <table>
    <thead>
      <tr><th>Concepto</th><th class="num">Monto</th><th class="num">Saldo</th><th>Vencimiento</th><th>Estado</th></tr>
    </thead>
    <tbody>${filasCargos}</tbody>
  </table>
  <h3>Pagos</h3>
  <table>
    <thead>
      <tr><th>Recibo</th><th>Fecha</th><th>Método</th><th class="num">Monto</th><th>Estado</th></tr>
    </thead>
    <tbody>${filasPagos}</tbody>
  </table>
</body>
</html>`;
  }

}
