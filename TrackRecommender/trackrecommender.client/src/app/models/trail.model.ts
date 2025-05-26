export interface Trail {
  id: number;
  name: string;
  difficulty: string;
  category: string;
  trailType: string;
  distance: number;
  duration: number;
  regionNames: string[];
  averageRating?: number;
  reviewCount?: number;
  geoJsonData?: string;
}
