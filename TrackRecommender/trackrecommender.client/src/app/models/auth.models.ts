export interface LoginRequest {
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiration: Date;
}

export interface UserProfile {
  id: number;
  username: string;
  email: string;
  createdAt: Date;
  lastLoginAt: Date;
  role: string;
  hasPreferences: boolean;
}
