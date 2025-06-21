import { Trail } from './trail.model';

export interface TrailRecommendation {
  trail: Trail;
  recommendationScore: number;
  matchReasons: string[];
  scoreBreakdown: { [key: string]: number };
  weather?: {
    temperature: number;
    condition: string;
    icon: string;
  };
}

export interface RecommendationResponse {
  recommendations: TrailRecommendation[];
  totalCount: number;
  generatedAt: Date;
  weatherDataAvailable: boolean;
}
