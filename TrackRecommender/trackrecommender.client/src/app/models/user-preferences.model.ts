export interface UserPreferences {
  preferredTrailTypes?: string[];
  preferredDifficulty?: string;
  preferredTags?: string[];
  maxDistance?: number;
  maxDuration?: number;
  preferredCategories?: string[];
  minimumRating?: number;
  preferredRegionNames?: string[];
}

export interface PreferenceOptions {
  trailTypes: string[];
  difficulties: string[];
  categories: string[];
  availableTags: string[];
  regions: RegionOption[];
  minDistance: number;
  maxDistance: number;
  minDuration: number;
  maxDuration: number;
}

export interface RegionOption {
  id: number;
  name: string;
  trailCount: number;
}

export interface TagGroup {
  category: string;
  tags: string[];
}

export const DIFFICULTY_COLORS: { [key: string]: string } = {
  Easy: '#4CAF50',
  Moderate: '#FFC107',
  Difficult: '#FF9800',
  'Very Difficult': '#F44336',
  Expert: '#9C27B0',
};

export const TAG_CATEGORIES: { [key: string]: string } = {
  route: 'Route Types',
  network: 'Networks',
  symbol: 'Trail Symbols',
  operator: 'Operators',
  mountain_feature: 'Mountain Features',
  tourist_feature: 'Tourist Features',
  seasonal_access: 'Seasonal Access',
  technical_difficulty: 'Technical Difficulty',
};
