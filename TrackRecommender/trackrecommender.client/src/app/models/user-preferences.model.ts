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

// Constante pentru valorile posibile bazate pe backend
export const DIFFICULTY_LEVELS = [
  'Easy',
  'Moderate',
  'Difficult',
  'Very Difficult',
  'Expert',
] as const;

export const TRAIL_TYPES = [
  'Hiking',
  'Walking',
  'Cycling',
  'Mountain Biking',
] as const;

export const TRAIL_CATEGORIES = [
  'International',
  'National',
  'Regional',
  'Local',
] as const;

export const RATING_OPTIONS = [1, 2, 3, 4, 5] as const;

export interface Region {
  id: number;
  name: string;
}
