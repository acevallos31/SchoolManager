import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DocumentoImprimible } from '../documents/documento-imprimible';
import { JsPdfLienzoAdapter } from '../documents/jspdf-lienzo-adapter';
import type { LienzoPdf } from '../documents/lienzo-pdf';
import { ImpresionService } from './impresion.service';

class DocumentoPrueba extends DocumentoImprimible {
  readonly renderizarPdfSpy = vi.fn();
  readonly renderizarHtmlSpy = vi.fn(() => '<html><body>Recibo</body></html>');

  constructor() {
    super('recibo-1.pdf', 'Recibo #1', { nombre: 'Colegio' });
  }

  renderizarPdf(lienzo: LienzoPdf): void {
    this.renderizarPdfSpy(lienzo);
    lienzo.setFontSize(10);
    lienzo.text('Recibo', 10, 10);
  }

  renderizarHtml(): string {
    return this.renderizarHtmlSpy();
  }
}

describe('ImpresionService', () => {
  let service: ImpresionService;
  let documento: DocumentoPrueba;

  beforeEach(() => {
    vi.restoreAllMocks();
    service = new ImpresionService();
    documento = new DocumentoPrueba();
  });

  it('renderiza el documento y guarda el PDF con el nombre del documento', async () => {
    const saveSpy = vi.spyOn(JsPdfLienzoAdapter.prototype, 'save').mockImplementation(() => undefined);

    await service.descargarPdf(documento);

    expect(documento.renderizarPdfSpy).toHaveBeenCalledOnce();
    expect(saveSpy).toHaveBeenCalledWith('recibo-1.pdf');
  });

  it('genera un Blob PDF después de renderizar el documento', async () => {
    const blob = new Blob(['pdf'], { type: 'application/pdf' });
    const outputSpy = vi.spyOn(JsPdfLienzoAdapter.prototype, 'output').mockReturnValue(blob);

    const resultado = await service.generarPdfBlob(documento);

    expect(documento.renderizarPdfSpy).toHaveBeenCalledOnce();
    expect(outputSpy).toHaveBeenCalledWith('blob');
    expect(resultado).toBe(blob);
  });

  it('abre una ventana, escribe HTML y dispara impresión cuando termina de cargar', () => {
    const abrir = vi.fn();
    const escribir = vi.fn();
    const cerrar = vi.fn();
    const focus = vi.fn();
    const print = vi.fn();
    const addEventListener = vi.fn((_evento: string, callback: () => void) => callback());
    const ventana = {
      document: { open: abrir, write: escribir, close: cerrar },
      focus,
      print,
      addEventListener,
    };
    const openSpy = vi.spyOn(window, 'open').mockReturnValue(ventana as unknown as Window);

    service.imprimir(documento);

    expect(openSpy).toHaveBeenCalledWith('', '_blank', 'noopener,noreferrer');
    expect(abrir).toHaveBeenCalledOnce();
    expect(documento.renderizarHtmlSpy).toHaveBeenCalledOnce();
    expect(escribir).toHaveBeenCalledWith('<html><body>Recibo</body></html>');
    expect(cerrar).toHaveBeenCalledOnce();
    expect(addEventListener).toHaveBeenCalledWith('load', expect.any(Function), { once: true });
    expect(focus).toHaveBeenCalledOnce();
    expect(print).toHaveBeenCalledOnce();
  });

  it('falla de forma explícita cuando el navegador bloquea la ventana de impresión', () => {
    vi.spyOn(window, 'open').mockReturnValue(null);

    expect(() => service.imprimir(documento))
      .toThrow('El navegador bloqueó la ventana de impresión.');
  });
});
