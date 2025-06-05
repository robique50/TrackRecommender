export interface GeoJsonGeometry {
  type:
    | 'LineString'
    | 'MultiLineString'
    | 'Point'
    | 'MultiPoint'
    | 'Polygon'
    | 'MultiPolygon';
  coordinates: any;
}

export interface GeoJsonFeature {
  type: 'Feature';
  geometry: GeoJsonGeometry;
  properties?: any;
}

export interface Trail {
  id: number;
  osmId: number;
  name: string;
  description: string;
  distance: number;
  duration: number;
  difficulty: string;
  trailType: string;
  startLocation: string;
  endLocation: string;
  category: string;
  network?: string;
  geoJsonData: string;
  regionNames: string[];
  regionIds: number[];
  tags: string[];
  matchScore: number;
  averageRating: number;
  reviewsCount: number;
  lastUpdated: Date;
  hasReview?: boolean; // Adaugă această proprietate
}

export interface TrailFilters {
  difficulty?: string;
  regionIds?: number[];
  trailType?: string;
  maxDistance?: number;
  category?: string;
  maxDuration?: number;
  tags?: string[];
}

export type DifficultyLevel =
  | 'Easy'
  | 'Moderate'
  | 'Difficult'
  | 'Very Difficult'
  | 'Expert';
export type TrailCategory = 'International' | 'National' | 'Regional' | 'Local';
export type TrailType = 'Hiking' | 'Walking' | 'Cycling' | 'Mountain Biking';
