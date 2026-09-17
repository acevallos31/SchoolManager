import { beforeEach, describe, expect, it, vi } from 'vitest';
import { JsPdfLienzoAdapter } from './jspdf-lienzo-adapter';

const mock = vi.hoisted(() => {
  const blob = new Blob(['pdf'], { type: 'application/pdf' });
  return {
    ctor: vi.fn(),
    blob,
    pdf: {
      setFontSize: vi.fn(),
      setFont: vi.fn(),
      setTextColor: vi.fn(),
      text: vi.fn(),
      splitTextToSize: vi.fn(() => ['a', 'b']),
      getTextWidth: vi.fn(() => 42),
      internal: {
        pageSize: {
          getWidth: vi.fn(() => 216),
          getHeight: vi.fn(() => 279),
        },
      },
      setDrawColor: vi.fn(),
      setLineWidth: vi.fn(),
      line: vi.fn(),
      rect: vi.fn(),
      save: vi.fn(),
      output: vi.fn(() => blob),
    },
  };
});

vi.mock('jspdf', () => ({
  jsPDF: class {
    constructor(options: unknown) {
      mock.ctor(options);
      return mock.pdf;
    }
  },
}));

describe('JsPdfLienzoAdapter', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mock.pdf.splitTextToSize.mockReturnValue(['a', 'b']);
    mock.pdf.getTextWidth.mockReturnValue(42);
    mock.pdf.internal.pageSize.getWidth.mockReturnValue(216);
    mock.pdf.internal.pageSize.getHeight.mockReturnValue(279);
    mock.pdf.output.mockReturnValue(mock.blob);
  });

  it('crea jsPDF con letter portrait y milímetros por defecto', () => {
    new JsPdfLienzoAdapter();

    expect(mock.ctor).toHaveBeenCalledWith({
      orientation: 'portrait',
      unit: 'mm',
      format: 'letter',
    });
  });

  it('respeta el formato solicitado por el consumidor', () => {
    new JsPdfLienzoAdapter({ orientation: 'landscape', unit: 'mm', format: 'a4' });

    expect(mock.ctor).toHaveBeenCalledWith({
      orientation: 'landscape',
      unit: 'mm',
      format: 'a4',
    });
  });

  it('delega operaciones de texto y medición en jsPDF', () => {
    const lienzo = new JsPdfLienzoAdapter();

    lienzo.setFontSize(12);
    lienzo.setFont('bold');
    lienzo.setTextColor(1, 2, 3);
    lienzo.text('hola', 10, 20);

    expect(lienzo.splitTextToSize('texto largo', 80)).toEqual(['a', 'b']);
    expect(lienzo.getTextWidth('hola')).toBe(42);
    expect(lienzo.getWidth()).toBe(216);
    expect(lienzo.getHeight()).toBe(279);

    expect(mock.pdf.setFontSize).toHaveBeenCalledWith(12);
    expect(mock.pdf.setFont).toHaveBeenCalledWith('helvetica', 'bold');
    expect(mock.pdf.setTextColor).toHaveBeenCalledWith(1, 2, 3);
    expect(mock.pdf.text).toHaveBeenCalledWith('hola', 10, 20);
    expect(mock.pdf.splitTextToSize).toHaveBeenCalledWith('texto largo', 80);
    expect(mock.pdf.getTextWidth).toHaveBeenCalledWith('hola');
  });

  it('delega trazos, rectángulos y exportación en jsPDF', () => {
    const lienzo = new JsPdfLienzoAdapter();

    lienzo.setDrawColor(4, 5, 6);
    lienzo.setLineWidth(0.5);
    lienzo.line(1, 2, 3, 4);
    lienzo.rect(5, 6, 7, 8, 'FD');
    lienzo.save('recibo.pdf');
    const resultado = lienzo.output('blob');

    expect(mock.pdf.setDrawColor).toHaveBeenCalledWith(4, 5, 6);
    expect(mock.pdf.setLineWidth).toHaveBeenCalledWith(0.5);
    expect(mock.pdf.line).toHaveBeenCalledWith(1, 2, 3, 4);
    expect(mock.pdf.rect).toHaveBeenCalledWith(5, 6, 7, 8, 'FD');
    expect(mock.pdf.save).toHaveBeenCalledWith('recibo.pdf');
    expect(mock.pdf.output).toHaveBeenCalledWith('blob');
    expect(resultado).toBe(mock.blob);
  });
});
