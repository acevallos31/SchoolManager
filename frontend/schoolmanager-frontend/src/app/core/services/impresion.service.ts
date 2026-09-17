import { Injectable } from '@angular/core';
import { jsPDF } from 'jspdf';
import { DocumentoImprimible } from '../documents/documento-imprimible';

@Injectable({ providedIn: 'root' })
export class ImpresionService {
  async descargarPdf(documento: DocumentoImprimible): Promise<void> {
    const pdf = await this.crearPdf(documento);
    pdf.save(documento.nombreArchivo);
  }

  async generarPdfBlob(documento: DocumentoImprimible): Promise<Blob> {
    const pdf = await this.crearPdf(documento);
    return pdf.output('blob');
  }

  imprimir(documento: DocumentoImprimible): void {
    if (typeof window === 'undefined') return;

    const ventana = window.open('', '_blank', 'noopener,noreferrer');
    if (!ventana) {
      throw new Error('El navegador bloqueó la ventana de impresión.');
    }

    ventana.document.open();
    ventana.document.write(documento.renderizarHtml());
    ventana.document.close();

    ventana.addEventListener('load', () => {
      ventana.focus();
      ventana.print();
    }, { once: true });
  }

  private async crearPdf(documento: DocumentoImprimible): Promise<jsPDF> {
    const pdf = new jsPDF({
      orientation: 'portrait',
      unit: 'mm',
      format: 'letter',
    });
    await documento.renderizarPdf(pdf);
    return pdf;
  }
}
