import { Injectable } from '@angular/core';
import { DocumentoImprimible } from '../documents/documento-imprimible';
import { JsPdfLienzoAdapter } from '../documents/jspdf-lienzo-adapter';
import type { LienzoPdf } from '../documents/lienzo-pdf';

/**
 * Orquesta impresión/descarga/Blob de cualquier DocumentoImprimible.
 *
 * Depende del puerto LienzoPdf (DIP): solicita el adaptador concreto, que
 * encapsula jsPDF, y lo entrega al documento. Ni este servicio ni los
 * documentos conocen la librería de PDF.
 */
@Injectable({ providedIn: 'root' })
export class ImpresionService {
  async descargarPdf(documento: DocumentoImprimible): Promise<void> {
    const lienzo = await this.crearLienzo(documento);
    lienzo.save(documento.nombreArchivo);
  }

  async generarPdfBlob(documento: DocumentoImprimible): Promise<Blob> {
    const lienzo = await this.crearLienzo(documento);
    return lienzo.output('blob');
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

  private async crearLienzo(documento: DocumentoImprimible): Promise<LienzoPdf> {
    const lienzo = new JsPdfLienzoAdapter();
    await documento.renderizarPdf(lienzo);
    return lienzo;
  }
}
