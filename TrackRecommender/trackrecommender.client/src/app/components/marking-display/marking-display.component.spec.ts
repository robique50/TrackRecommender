import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MarkingDisplayComponent } from './marking-display.component';

describe('MarkingDisplayComponent', () => {
  let component: MarkingDisplayComponent;
  let fixture: ComponentFixture<MarkingDisplayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MarkingDisplayComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(MarkingDisplayComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
