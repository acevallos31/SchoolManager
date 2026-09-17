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
