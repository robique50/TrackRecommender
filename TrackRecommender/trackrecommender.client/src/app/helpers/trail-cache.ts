export interface TrailCache<T = any> {
  data: T[];
  timestamp: number;
  filters: string;
}
