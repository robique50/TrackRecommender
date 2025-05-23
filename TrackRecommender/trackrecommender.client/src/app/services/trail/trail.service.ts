import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class TrailService {
  constructor(private http: HttpClient) {}

  getTrails(filters?: any): Observable<any[]> {
    let params = new HttpParams();

    if (filters) {
      Object.keys(filters).forEach((key) => {
        if (filters[key] !== undefined && filters[key] !== null) {
          params = params.append(key, filters[key]);
        }
      });
    }

    return this.http.get<any[]>('/api/mapdata/trails', { params });
  }

  getTrailById(id: number): Observable<any> {
    return this.http.get<any>(`/api/mapdata/trails/${id}`);
  }

  getRecommendedTrails(): Observable<any[]> {
    return this.http.get<any[]>('/api/mapdata/recommended');
  }
}
