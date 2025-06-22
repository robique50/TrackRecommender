import { TestBed } from '@angular/core/testing';

import { RecommendationSettingsService } from './recommendation-settings.service';

describe('RecommendationSettingsService', () => {
  let service: RecommendationSettingsService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(RecommendationSettingsService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
