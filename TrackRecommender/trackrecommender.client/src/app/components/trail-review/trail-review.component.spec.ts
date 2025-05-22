import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TrailReviewComponent } from './trail-review.component';

describe('TrailReviewComponent', () => {
  let component: TrailReviewComponent;
  let fixture: ComponentFixture<TrailReviewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TrailReviewComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(TrailReviewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
