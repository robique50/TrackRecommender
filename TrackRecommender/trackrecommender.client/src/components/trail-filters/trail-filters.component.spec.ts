import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TrailFiltersComponent } from './trail-filters.component';

describe('TrailFiltersComponent', () => {
  let component: TrailFiltersComponent;
  let fixture: ComponentFixture<TrailFiltersComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TrailFiltersComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TrailFiltersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
