import { jsPDF } from 'jspdf';
import type { LienzoPdf } from './lienzo-pdf';

/**
 * Adaptador que hace cumplir a jsPDF el contrato `LienzoPdf`. Es el ÚNICO
 * punto de la aplicación que conoce la librería concreta de PDF; el resto
 * depende exclusivamente del puerto `LienzoPdf` (DIP).
 */
export class JsPdfLienzoAdapter implements LienzoPdf {
  private readonly pdf: jsPDF;

  constructor(formato: { orientation?: 'portrait' | 'landscape'; unit?: 'mm'; format?: string } = {}) {
    this.pdf = new jsPDF({
      orientation: formato.orientation ?? 'portrait',
      unit: formato.unit ?? 'mm',
      format: formato.format ?? 'letter',
    });
  }

  setFontSize(size: number): void { this.pdf.setFontSize(size); }
  setFont(style: 'normal' | 'bold' | 'italic' | 'bolditalic'): void { this.pdf.setFont('helvetica', style); }
  setTextColor(r: number, g: number, b: number): void { this.pdf.setTextColor(r, g, b); }
  text(text: string | string[], x: number, y: number): void { this.pdf.text(text, x, y); }
  splitTextToSize(text: string, maxWidth: number): string[] { return this.pdf.splitTextToSize(text, maxWidth); }
  getTextWidth(text: string): number { return this.pdf.getTextWidth(text); }

  getWidth(): number { return this.pdf.internal.pageSize.getWidth(); }
  getHeight(): number { return this.pdf.internal.pageSize.getHeight(); }

  setDrawColor(r: number, g: number, b: number): void { this.pdf.setDrawColor(r, g, b); }
  setLineWidth(width: number): void { this.pdf.setLineWidth(width); }
  line(x1: number, y1: number, x2: number, y2: number): void { this.pdf.line(x1, y1, x2, y2); }
  rect(x: number, y: number, w: number, h: number, style?: 'S' | 'F' | 'FD'): void {
    this.pdf.rect(x, y, w, h, style);
  }

  save(nombreArchivo: string): void { this.pdf.save(nombreArchivo); }
  output(tipo: 'blob'): Blob { return this.pdf.output(tipo); }
}
