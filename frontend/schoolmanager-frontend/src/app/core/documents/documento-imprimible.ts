import type { LienzoPdf } from './lienzo-pdf';

export interface EncabezadoInstitucionDocumento {
  nombre: string;
  nombreCorto?: string | null;
  direccion?: string | null;
  telefono?: string | null;
  correo?: string | null;
  logoUrl?: string | null;
}

/**
 * Contrato base para cualquier documento imprimible/exportable de SchoolManager.
 *
 * Cada documento concreto conoce su contenido y presentación, mientras que
 * ImpresionService se encarga de orquestar impresión, descarga y Blob PDF.
 *
 * Los helpers compartidos de formato/cabecera/CSS viven aquí para evitar que
 * recibos y estados de cuenta diverjan visualmente al evolucionar.
 */
export abstract class DocumentoImprimible {
  protected constructor(
    public readonly nombreArchivo: string,
    public readonly titulo: string,
    public readonly institucion: EncabezadoInstitucionDocumento,
  ) {}

  /** Dibuja el documento sobre un lienzo PDF. El contrato (LienzoPdf) está
   *  desacoplado de jsPDF; el adaptador concreto lo inyecta ImpresionService. */
  abstract renderizarPdf(lienzo: LienzoPdf): Promise<void> | void;

  /** Genera HTML autocontenido para vista previa e impresión física. */
  abstract renderizarHtml(): string;

  protected formatoFecha(iso: string): string {
    const fecha = new Date(iso);
    if (Number.isNaN(fecha.getTime())) return iso;
    const dd = String(fecha.getDate()).padStart(2, '0');
    const mm = String(fecha.getMonth() + 1).padStart(2, '0');
    return `${dd}/${mm}/${fecha.getFullYear()}`;
  }

  protected formatoMoneda(valor: number): string {
    return `$${Number(valor).toFixed(2)}`;
  }

  /**
   * Dibuja la cabecera institucional común y devuelve la siguiente coordenada Y.
   * Conserva exactamente el layout que 047A/047B ya utilizaban por separado.
   */
  protected crearEncabezadoPdf(lienzo: LienzoPdf, margen: number): number {
    const xDerecha = lienzo.getWidth() - margen;
    let y = margen + 5;

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
    return y;
  }

  /**
   * CSS base de documentos HTML. Cada documento agrega únicamente sus reglas
   * específicas (p. ej. resumen o estado anulado).
   */
  protected estilosHtml(especificos = ''): string {
    return `
  :root { color-scheme: light; }
  body { font-family: Arial, Helvetica, sans-serif; color: #0f172a; margin: 24px; }
  header { border-bottom: 1px solid #cbd5e1; padding-bottom: 8px; margin-bottom: 16px; }
  h1 { font-size: 20px; margin: 0; }
  .contacto { color: #475569; font-size: 12px; margin-top: 4px; }
  h2 { font-size: 16px; margin: 0 0 4px; }
  .meta { color: #334155; font-size: 13px; margin: 0 0 12px; }
  table { width: 100%; border-collapse: collapse; }
  th, td { border-bottom: 1px solid #e2e8f0; padding: 6px 4px; font-size: 13px; text-align: left; }
  th.num, td.num { text-align: right; }
  .vacio { color: #64748b; text-align: center; }
${especificos}`;
  }

  protected escaparHtml(valor: unknown): string {
    return String(valor ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  protected textoOpcional(valor: string | null | undefined, fallback = '—'): string {
    const normalizado = valor?.trim();
    return normalizado ? normalizado : fallback;
  }
}
