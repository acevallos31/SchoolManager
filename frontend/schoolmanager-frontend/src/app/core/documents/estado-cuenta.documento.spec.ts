import { EstadoCuentaDocumento } from './estado-cuenta.documento';
import type { LienzoPdf } from './lienzo-pdf';
import type { EstadoCuenta } from '../services/estado-cuenta.service';

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

const ESTADO: EstadoCuenta = {
  institucion: {
    id: 'i1', nombre: 'Colegio Ejemplo', nombreCorto: 'CE', direccion: 'Av. 1',
    telefono: '123', correo: 'a@b.c', logoUrl: null,
  },
  alumno: { id: 'a1', nombreCompleto: 'Ana Pérez', rne: 'RNE-1', codigoInterno: 'A-01' },
  resumen: {
    alumnoId: 'a1', institucionId: 'i1', totalObligaciones: 2, totalMontoOriginal: 300,
    totalPendiente: 200, totalVencido: 100, totalAnulado: 0, totalAplicado: 100,
  },
  cargos: [
    {
      id: 'c1', matriculaId: 'm1', alumnoId: 'a1', planPagoId: 'p1', orden: 1,
      conceptoId: null, conceptoNombre: 'Colegiatura', descripcion: 'Cuota 1',
      montoOriginal: 200, fechaVencimiento: '2026-09-30', estado: 'pendiente',
      fechaGeneracion: new Date().toISOString(), fechaAnulacion: null, motivoAnulacion: null,
      esVencido: false, saldo: 200, aplicado: 0,
    },
  ],
  pagos: [
    {
      id: 'p1', institucionId: 'i1', alumnoId: 'a1', responsableId: null,
      numeroRecibo: 3, montoTotal: 100, fechaPago: '2026-09-15T10:00:00Z',
      metodoPago: 'efectivo', referenciaExterna: null, estado: 'registrado',
      registradoPor: null, fechaAnulacion: null, anuladoPor: null, motivoAnulacion: null,
      createdAt: '2026-09-15T10:00:00Z',
    },
  ],
};

describe('EstadoCuentaDocumento (047B)', () => {
  it('expone nombre de archivo y título a partir del DTO', () => {
    const doc = new EstadoCuentaDocumento(ESTADO);
    expect(doc.nombreArchivo).toBe('estado-cuenta-A-01.pdf');
    expect(doc.titulo).toBe('Estado de cuenta');
    expect(doc.institucion.nombre).toBe('Colegio Ejemplo');
  });

  it('dibuja cabecera, resumen, cargos y pagos sin recalcular importes', () => {
    const doc = new EstadoCuentaDocumento(ESTADO);
    const lienzo = new StubLienzo();

    doc.renderizarPdf(lienzo);

    expect(lienzo.plano).toContain('ESTADO DE CUENTA');
    expect(lienzo.plano).toContain('Colegio Ejemplo');
    expect(lienzo.plano).toContain('Ana Pérez');
    expect(lienzo.plano).toContain('Colegiatura');
    // Totales autoritativos del backend (no recalculados en el frontend).
    expect(lienzo.plano).toContain('$300.00');
    expect(lienzo.plano).toContain('$200.00');
    expect(lienzo.plano).toContain('N° 3');
    expect(lienzo.trazos).toBeGreaterThan(0);
  });

  it('genera HTML autocontenido que escapa el contenido no confiable', () => {
    const doc = new EstadoCuentaDocumento({
      ...ESTADO,
      alumno: { id: 'a1', nombreCompleto: '<script>alert(1)</script>', rne: null, codigoInterno: null },
    });

    const html = doc.renderizarHtml();

    expect(html).toContain('<!doctype html>');
    expect(html).toContain('Colegio Ejemplo');
    expect(html).toContain('$300.00');
    expect(html).not.toContain('<script>alert(1)</script>');
    expect(html).toContain('&lt;script&gt;');
  });
});
