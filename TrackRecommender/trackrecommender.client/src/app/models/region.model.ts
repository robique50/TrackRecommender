export interface Region {
  id: number;
  name: string;
  trailCount: number;
  boundaryGeoJson: string;
}

export interface RegionStatistics {
  totalRegions: number;
  regionsWithTrails: number;
  totalTrails: number;
  topRegions: Array<{
    name: string;
    trailCount: number;
  }>;
}
