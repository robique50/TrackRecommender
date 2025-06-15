import { TestBed } from '@angular/core/testing';

import { TrailMarkingService } from './trail-marking.service';

describe('TrailMarkingService', () => {
  let service: TrailMarkingService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(TrailMarkingService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
