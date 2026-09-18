import { DocumentoImprimible } from './documento-imprimible';
import type { LienzoPdf } from './lienzo-pdf';
import type { ReciboPago } from '../services/pagos.service';

/**
 * Documento concreto del recibo de pago (047A).
 *
 * SOLO presentación: toma el DTO autoritativo que compone el backend
 * (ReciboPago) y lo dibuja en PDF (a través del puerto LienzoPdf) o lo
 * expone como HTML autocontenido. No recalcula importes ni decide estados.
 */
export class ReciboPagoDocumento extends DocumentoImprimible {
  private readonly recibo: ReciboPago;

  constructor(recibo: ReciboPago) {
    super(
      `recibo-${recibo.numeroRecibo}.pdf`,
      `Recibo de pago #${recibo.numeroRecibo}`,
      {
        nombre: recibo.institucion?.nombre ?? '',
        nombreCorto: recibo.institucion?.nombreCorto ?? null,
        direccion: recibo.institucion?.direccion ?? null,
        telefono: recibo.institucion?.telefono ?? null,
        correo: recibo.institucion?.correo ?? null,
        logoUrl: recibo.institucion?.logoUrl ?? null,
      },
    );
    this.recibo = recibo;
  }

  renderizarPdf(lienzo: LienzoPdf): void {
    const margen = 15;
    const ancho = lienzo.getWidth();
    const anchoUtil = ancho - margen * 2;
    const xDerecha = ancho - margen;
    let y = margen + 5;

    // Encabezado: institución emisora.
    lienzo.setFont('bold');
    lienzo.setFontSize(14);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text(this.institucion.nombre || 'Institución', margen, y);
    y += 5;

    lienzo.setFont('normal');
    lienzo.setFontSize(9);
    lienzo.setTextColor(71, 85, 105);
    const contacto = [this.institucion.telefono, this.institucion.correo].filter(Boolean).join(' · ');
    for (const linea of [this.institucion.direccion, contacto]) {
      if (linea) {
        lienzo.text(linea, margen, y);
        y += 4;
      }
    }

    y += 3;
    lienzo.setDrawColor(203, 213, 225);
    lienzo.setLineWidth(0.3);
    lienzo.line(margen, y, xDerecha, y);
    y += 8;

    // Título y número de recibo.
    lienzo.setFont('bold');
    lienzo.setFontSize(13);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text('RECIBO DE PAGO', margen, y);
    const numero = `N° ${this.recibo.numeroRecibo}`;
    lienzo.setFontSize(10);
    lienzo.text(numero, xDerecha - lienzo.getTextWidth(numero), y);
    y += 7;

    // Datos del pago y del alumno.
    lienzo.setFont('normal');
    lienzo.setFontSize(9);
    lienzo.setTextColor(51, 65, 85);
    lienzo.text(`Fecha: ${this.formatoFecha(this.recibo.fechaPago)}`, margen, y);
    y += 5;
    lienzo.text(`Alumno: ${this.recibo.alumno.nombreCompleto}`, margen, y);
    y += 5;

    const identidad = [
      this.recibo.alumno.rne ? `RNE: ${this.recibo.alumno.rne}` : null,
      this.recibo.alumno.codigoInterno ? `Código: ${this.recibo.alumno.codigoInterno}` : null,
    ].filter(Boolean).join(' · ');
    if (identidad) {
      lienzo.text(identidad, margen, y);
      y += 5;
    }
    if (this.recibo.metodoPago) {
      lienzo.text(`Método de pago: ${this.recibo.metodoPago}`, margen, y);
      y += 5;
    }
    if (this.recibo.referenciaExterna) {
      lienzo.text(`Referencia: ${this.recibo.referenciaExterna}`, margen, y);
      y += 5;
    }

    // Detalle aplicado a cargos.
    y += 3;
    lienzo.setFont('bold');
    lienzo.setFontSize(9);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text('Concepto', margen, y);
    const cabMonto = 'Monto aplicado';
    lienzo.text(cabMonto, xDerecha - lienzo.getTextWidth(cabMonto), y);
    y += 2;
    lienzo.setDrawColor(203, 213, 225);
    lienzo.line(margen, y, xDerecha, y);
    y += 5;

    lienzo.setFont('normal');
    lienzo.setTextColor(51, 65, 85);
    const anchoConcepto = Math.max(40, anchoUtil - 40);
    for (const detalle of this.recibo.detalles) {
      const lineas = lienzo.splitTextToSize(detalle.concepto || 'Cargo', anchoConcepto);
      lienzo.text(lineas, margen, y);
      const monto = this.formatoMoneda(detalle.montoAplicado);
      lienzo.text(monto, xDerecha - lienzo.getTextWidth(monto), y);
      y += lineas.length * 4.5 + 1.5;
    }
    if (this.recibo.detalles.length === 0) {
      lienzo.text('Sin aplicaciones registradas.', margen, y);
      y += 5;
    }

    y += 2;
    lienzo.setDrawColor(203, 213, 225);
    lienzo.line(margen, y, xDerecha, y);
    y += 6;
    lienzo.setFont('bold');
    lienzo.setFontSize(11);
    lienzo.setTextColor(15, 23, 42);
    lienzo.text('Total', margen, y);
    const total = this.formatoMoneda(this.recibo.montoTotal);
    lienzo.text(total, xDerecha - lienzo.getTextWidth(total), y);

    // Estado de anulación (si aplica).
    if (this.recibo.estado === 'anulado') {
      y += 9;
      lienzo.setFont('bold');
      lienzo.setFontSize(9);
      lienzo.setTextColor(185, 28, 28);
      lienzo.text('ANULADO', margen, y);
      y += 5;
      if (this.recibo.motivoAnulacion) {
        lienzo.setFont('normal');
        lienzo.setTextColor(120, 53, 15);
        for (const linea of lienzo.splitTextToSize(`Motivo: ${this.recibo.motivoAnulacion}`, anchoUtil)) {
          lienzo.text(linea, margen, y);
          y += 4;
        }
      }
    }
  }

  renderizarHtml(): string {
    const e = (v: unknown) => this.escaparHtml(v);
    const contacto = [
      this.institucion.direccion,
      this.institucion.telefono,
      this.institucion.correo,
    ].filter((v): v is string => !!v).map(e).join(' · ');

    const filas = this.recibo.detalles.length
      ? this.recibo.detalles.map((d) => `
          <tr>
            <td>${e(d.concepto || 'Cargo')}</td>
            <td class="num">${e(this.formatoMoneda(d.montoAplicado))}</td>
          </tr>`).join('')
      : '<tr><td colspan="2" class="vacio">Sin aplicaciones registradas.</td></tr>';

    const identidad = [
      this.recibo.alumno.rne ? `RNE: ${e(this.recibo.alumno.rne)}` : null,
      this.recibo.alumno.codigoInterno ? `Código: ${e(this.recibo.alumno.codigoInterno)}` : null,
    ].filter((v): v is string => !!v).join(' · ');

    const anulado = this.recibo.estado === 'anulado'
      ? `<p class="anulado">ANULADO${this.recibo.motivoAnulacion ? ` — ${e(this.recibo.motivoAnulacion)}` : ''}</p>`
      : '';

    return `<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8" />
<title>${e(this.titulo)}</title>
<style>
  :root { color-scheme: light; }
  body { font-family: Arial, Helvetica, sans-serif; color: #0f172a; margin: 24px; }
  header { border-bottom: 1px solid #cbd5e1; padding-bottom: 8px; margin-bottom: 16px; }
  h1 { font-size: 20px; margin: 0; }
  .contacto { color: #475569; font-size: 12px; margin-top: 4px; }
  h2 { font-size: 16px; margin: 0 0 4px; }
  .meta { color: #334155; font-size: 13px; margin: 0 0 12px; }
  table { width: 100%; border-collapse: collapse; margin-top: 12px; }
  th, td { border-bottom: 1px solid #e2e8f0; padding: 6px 4px; font-size: 13px; text-align: left; }
  th.num, td.num { text-align: right; }
  .vacio { color: #64748b; text-align: center; }
  tfoot td { font-weight: bold; border-top: 2px solid #0f172a; }
  .anulado { color: #b91c1c; font-weight: bold; margin-top: 16px; }
  .cab { display: flex; justify-content: space-between; align-items: baseline; }
</style>
</head>
<body>
  <header>
    <h1>${e(this.institucion.nombre || 'Institución')}</h1>
    ${contacto ? `<p class="contacto">${contacto}</p>` : ''}
  </header>
  <div class="cab">
    <h2>RECIBO DE PAGO</h2>
    <strong>N° ${e(this.recibo.numeroRecibo)}</strong>
  </div>
  <p class="meta">
    Fecha: ${e(this.formatoFecha(this.recibo.fechaPago))} ·
    Alumno: ${e(this.recibo.alumno.nombreCompleto)}
    ${identidad ? ` · ${identidad}` : ''}
    ${this.recibo.metodoPago ? ` · Método: ${e(this.recibo.metodoPago)}` : ''}
    ${this.recibo.referenciaExterna ? ` · Referencia: ${e(this.recibo.referenciaExterna)}` : ''}
  </p>
  <table>
    <thead>
      <tr><th>Concepto</th><th class="num">Monto aplicado</th></tr>
    </thead>
    <tbody>${filas}</tbody>
    <tfoot>
      <tr><td>Total</td><td class="num">${e(this.formatoMoneda(this.recibo.montoTotal))}</td></tr>
    </tfoot>
  </table>
  ${anulado}
</body>
</html>`;
  }

  private formatoFecha(iso: string): string {
    const fecha = new Date(iso);
    if (Number.isNaN(fecha.getTime())) return iso;
    const dd = String(fecha.getDate()).padStart(2, '0');
    const mm = String(fecha.getMonth() + 1).padStart(2, '0');
    return `${dd}/${mm}/${fecha.getFullYear()}`;
  }

  private formatoMoneda(valor: number): string {
    return `$${Number(valor).toFixed(2)}`;
  }
}
