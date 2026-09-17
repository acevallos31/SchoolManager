import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { jsPDF } from 'jspdf';
import { JsPdfLienzoAdapter } from './jspdf-lienzo-adapter';

type AdaptadorInterno = { pdf: jsPDF };

describe('JsPdfLienzoAdapter', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('crea un lienzo letter portrait por defecto', () => {
    const lienzo = new JsPdfLienzoAdapter();

    expect(lienzo.getWidth()).toBeGreaterThan(200);
    expect(lienzo.getHeight()).toBeGreaterThan(lienzo.getWidth());
  });

  it('respeta orientación landscape y formato solicitado', () => {
    const lienzo = new JsPdfLienzoAdapter({ orientation: 'landscape', unit: 'mm', format: 'a4' });

    expect(lienzo.getWidth()).toBeGreaterThan(lienzo.getHeight());
    expect(lienzo.getWidth()).toBeGreaterThan(290);
  });

  it('delega operaciones de texto y medición en jsPDF', () => {
    const lienzo = new JsPdfLienzoAdapter();
    const pdf = (lienzo as unknown as AdaptadorInterno).pdf;

    const setFontSize = vi.spyOn(pdf, 'setFontSize');
    const setFont = vi.spyOn(pdf, 'setFont');
    const setTextColor = vi.spyOn(pdf, 'setTextColor');
    const text = vi.spyOn(pdf, 'text');
    const splitTextToSize = vi.spyOn(pdf, 'splitTextToSize');
    const getTextWidth = vi.spyOn(pdf, 'getTextWidth');

    lienzo.setFontSize(12);
    lienzo.setFont('bold');
    lienzo.setTextColor(1, 2, 3);
    lienzo.text('hola', 10, 20);
    const lineas = lienzo.splitTextToSize('texto largo', 20);
    const ancho = lienzo.getTextWidth('hola');

    expect(setFontSize).toHaveBeenCalledWith(12);
    expect(setFont).toHaveBeenCalledWith('helvetica', 'bold');
    expect(setTextColor).toHaveBeenCalledWith(1, 2, 3);
    expect(text).toHaveBeenCalledWith('hola', 10, 20);
    expect(splitTextToSize).toHaveBeenCalledWith('texto largo', 20);
    expect(getTextWidth).toHaveBeenCalledWith('hola');
    expect(Array.isArray(lineas)).toBe(true);
    expect(ancho).toBeGreaterThan(0);
  });

  it('delega trazos, rectángulos y exportación en jsPDF', () => {
    const lienzo = new JsPdfLienzoAdapter();
    const pdf = (lienzo as unknown as AdaptadorInterno).pdf;
    const blob = new Blob(['pdf'], { type: 'application/pdf' });

    const setDrawColor = vi.spyOn(pdf, 'setDrawColor');
    const setLineWidth = vi.spyOn(pdf, 'setLineWidth');
    const line = vi.spyOn(pdf, 'line');
    const rect = vi.spyOn(pdf, 'rect');
    const save = vi.spyOn(pdf, 'save').mockImplementation(() => pdf);
    const output = vi.spyOn(pdf, 'output').mockReturnValue(blob as never);

    lienzo.setDrawColor(4, 5, 6);
    lienzo.setLineWidth(0.5);
    lienzo.line(1, 2, 3, 4);
    lienzo.rect(5, 6, 7, 8, 'FD');
    lienzo.save('recibo.pdf');
    const resultado = lienzo.output('blob');

    expect(setDrawColor).toHaveBeenCalledWith(4, 5, 6);
    expect(setLineWidth).toHaveBeenCalledWith(0.5);
    expect(line).toHaveBeenCalledWith(1, 2, 3, 4);
    expect(rect).toHaveBeenCalledWith(5, 6, 7, 8, 'FD');
    expect(save).toHaveBeenCalledWith('recibo.pdf');
    expect(output).toHaveBeenCalledWith('blob');
    expect(resultado).toBe(blob);
  });
});
