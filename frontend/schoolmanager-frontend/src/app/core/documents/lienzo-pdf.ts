/**
 * Puerto de dibujo PDF (DIP). Define únicamente la superficie que los
 * documentos concretos necesitan para renderizarse, desacoplada por completo
 * de jsPDF. La implementación concreta (JsPdfLienzoAdapter) la aporta la capa
 * de infraestructura (ImpresionService). Gracias a esta abstracción,
 * `DocumentoImprimible` y sus subclases pueden testearse con un stub sin
 * depender de la librería de PDF ni de un entorno de dibujo real.
 */
export interface LienzoPdf {
  // Tipografía y texto.
  setFontSize(size: number): void;
  setFont(style: 'normal' | 'bold' | 'italic' | 'bolditalic'): void;
  setTextColor(r: number, g: number, b: number): void;
  text(text: string | string[], x: number, y: number): void;
  splitTextToSize(text: string, maxWidth: number): string[];
  getTextWidth(text: string): number;

  // Geometría de la página.
  getWidth(): number;
  getHeight(): number;

  // Trazos y rellenos.
  setDrawColor(r: number, g: number, b: number): void;
  setLineWidth(width: number): void;
  line(x1: number, y1: number, x2: number, y2: number): void;
  rect(x: number, y: number, w: number, h: number, style?: 'S' | 'F' | 'FD'): void;

  // Exportación.
  save(nombreArchivo: string): void;
  output(tipo: 'blob'): Blob;
}
