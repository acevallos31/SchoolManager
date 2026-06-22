import { ComponentFixture, TestBed } from '@angular/core/testing';

import { PortalPadre } from './portal-padre';

describe('PortalPadre', () => {
  let component: PortalPadre;
  let fixture: ComponentFixture<PortalPadre>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PortalPadre],
    }).compileComponents();

    fixture = TestBed.createComponent(PortalPadre);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
