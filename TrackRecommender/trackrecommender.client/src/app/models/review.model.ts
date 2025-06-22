export interface TrailReview {
  id: number;
  userId: number;
  username: string;
  trailId: number;
  trailName: string;
  rating: number;
  comment?: string;
  hasCompleted: boolean;
  ratedAt: Date;
  completedAt?: Date;
  actualDuration?: number;
  perceivedDifficulty?: string;
}

export interface CreateReviewRequest {
  rating: number;
  comment?: string;
  hasCompleted?: boolean;
  completedAt?: Date;
  actualDuration?: number;
  perceivedDifficulty?: string;
}

export interface TrailRatingStats {
  trailId: number;
  averageRating: number;
  reviewCount: number;
}

export interface CanReviewResponse {
  canReview: boolean;
  hasReviewed: boolean;
  trailId: number;
}

export interface ReviewFilters {
  rating?: number | null;
  hasCompleted?: boolean | null;
  perceivedDifficulty?: string | null;
  startDate?: Date;
  endDate?: Date;
  trailId?: number;
  userId?: number;
}

export interface ReviewsResponse {
  reviews: TrailReview[];
  totalCount: number;
  page: number;
  pageSize: number;
}
