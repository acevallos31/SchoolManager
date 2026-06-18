import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Alumnos } from './alumnos';
import { Router } from '@angular/router';

describe('Alumnos', () => {
  let component: Alumnos;
  let fixture: ComponentFixture<Alumnos>;

  const mockRouter = {
    navigate: () => null
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Alumnos],
      providers: [
        { provide: Router, useValue: mockRouter }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(Alumnos);
    component = fixture.componentInstance;

    spyOn(component, 'cargarAlumnos').and.callFake(() => Promise.resolve());

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('debería tener la lista de grados definida', () => {
    expect(component.grados.length).toBe(9);
  });

  it('debería filtrar alumnos correctamente', () => {
    component.alumnos = [
      { id: 1, nombre: 'Juan Perez', identidad: '0801199012345', grado: '1er Grado', estado: 'activo' },
      { id: 2, nombre: 'Maria Lopez', identidad: '0802199554321', grado: '2do Grado', estado: 'activo' }
    ];
    
    component.busqueda = 'Maria';
    expect(component.alumnosFiltrados.length).toBe(1);
    expect(component.alumnosFiltrados[0].nombre).toBe('Maria Lopez');
  });
});