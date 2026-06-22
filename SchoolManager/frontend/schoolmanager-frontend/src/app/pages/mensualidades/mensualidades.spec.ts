import { ComponentFixture, TestBed } from '@angular/core/testing';

import { Mensualidades } from './mensualidades';

describe('Mensualidades', () => {
  let component: Mensualidades;
  let fixture: ComponentFixture<Mensualidades>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Mensualidades],
    }).compileComponents();

    fixture = TestBed.createComponent(Mensualidades);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
