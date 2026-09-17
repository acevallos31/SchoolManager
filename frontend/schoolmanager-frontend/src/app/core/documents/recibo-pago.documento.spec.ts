import { ReciboPagoDocumento } from './recibo-pago.documento';
import type { LienzoPdf } from './lienzo-pdf';
import type { ReciboPago } from '../services/pagos.service';

/**
 * Stub del puerto LienzoPdf: prueba que el documento depende de la abstracción
 * (DIP) y no de jsPDF, y registra lo dibujado para inspeccionarlo.
 */
class StubLienzo implements LienzoPdf {
  textos: (string | string[])[] = [];
  trazos = 0;
  guardado: string | null = null;
  private ancho = 215.9;

  setFontSize(): void {}
  setFont(): void {}
  setTextColor(): void {}
  setDrawColor(): void {}
  setLineWidth(): void {}
  text(texto: string | string[]): void { this.textos.push(texto); }
  splitTextToSize(texto: string, _maxWidth: number): string[] { return [texto]; }
  getTextWidth(): number { return 0; }
  getWidth(): number { return this.ancho; }
  getHeight(): number { return 279.4; }
  line(): void { this.trazos++; }
  rect(): void {}
  save(nombre: string): void { this.guardado = nombre; }
  output(): Blob { return new Blob(); }

  get plano(): string {
    return this.textos.map((t) => (Array.isArray(t) ? t.join(' ') : t)).join('\n');
  }
}

const RECIBO: ReciboPago = {
  pagoId: 'p1',
  numeroRecibo: 3,
  fechaPago: '2026-09-15T10:00:00Z',
  montoTotal: 999,
  metodoPago: 'efectivo',
  referenciaExterna: null,
  estado: 'registrado',
  fechaAnulacion: null,
  motivoAnulacion: null,
  institucion: {
    id: 'i1', nombre: 'Colegio Ejemplo', nombreCorto: 'CE', direccion: 'Av. 1',
    telefono: '123', correo: 'a@b.c', logoUrl: null,
  },
  alumno: { id: 'a1', nombreCompleto: 'Ana Pérez', rne: 'RNE-1', codigoInterno: 'A-01' },
  detalles: [
    { cargoId: 'c1', concepto: 'Colegiatura', montoAplicado: 1, estado: 'aplicado' },
    { cargoId: 'c2', concepto: 'Matrícula', montoAplicado: 2, estado: 'aplicado' },
  ],
};

describe('ReciboPagoDocumento (047A)', () => {
  it('expone nombre de archivo y título a partir del DTO', () => {
    const doc = new ReciboPagoDocumento(RECIBO);
    expect(doc.nombreArchivo).toBe('recibo-3.pdf');
    expect(doc.titulo).toBe('Recibo de pago #3');
    expect(doc.institucion.nombre).toBe('Colegio Ejemplo');
  });

  it('dibuja la cabecera, el alumno y el detalle sin recalcular importes', () => {
    const doc = new ReciboPagoDocumento(RECIBO);
    const lienzo = new StubLienzo();

    doc.renderizarPdf(lienzo);

    expect(lienzo.plano).toContain('RECIBO DE PAGO');
    expect(lienzo.plano).toContain('Colegio Ejemplo');
    expect(lienzo.plano).toContain('Ana Pérez');
    expect(lienzo.plano).toContain('Colegiatura');
    expect(lienzo.plano).toContain('Matrícula');
    // El total es el del backend (999), no la suma del detalle (3).
    expect(lienzo.plano).toContain('$999.00');
    expect(lienzo.plano).not.toContain('$3.00');
    expect(lienzo.trazos).toBeGreaterThan(0);
  });

  it('señala visualmente un recibo anulado con su motivo', () => {
    const doc = new ReciboPagoDocumento({
      ...RECIBO, estado: 'anulado', motivoAnulacion: 'Pago duplicado',
    });
    const lienzo = new StubLienzo();

    doc.renderizarPdf(lienzo);

    expect(lienzo.plano).toContain('ANULADO');
    expect(lienzo.plano).toContain('Pago duplicado');
  });

  it('genera HTML autocontenido que escapa el contenido no confiable', () => {
    const doc = new ReciboPagoDocumento({
      ...RECIBO,
      alumno: { id: 'a1', nombreCompleto: '<script>alert(1)</script>', rne: null, codigoInterno: null },
    });

    const html = doc.renderizarHtml();

    expect(html).toContain('<!doctype html>');
    expect(html).toContain('Colegio Ejemplo');
    expect(html).toContain('$999.00');
    expect(html).not.toContain('<script>alert(1)</script>');
    expect(html).toContain('&lt;script&gt;');
  });
});
