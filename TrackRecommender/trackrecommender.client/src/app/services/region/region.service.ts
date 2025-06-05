import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Region, RegionStatistics } from '../../models/region.model';
import { Trail } from '../../models/trail.model';

@Injectable({
  providedIn: 'root',
})
export class RegionService {
  private readonly baseUrl = '/api/regions';

  constructor(private http: HttpClient) {}

  public getAllRegions(): Observable<Region[]> {
    return this.http.get<Region[]>(this.baseUrl);
  }

  public getAllRegionBoundaries(): Observable<Region[]> {
    return this.http.get<Region[]>(`${this.baseUrl}/boundaries`);
  }

  public getRegionById(id: number): Observable<Region> {
    return this.http.get<Region>(`${this.baseUrl}/${id}`);
  }

  public getTrailsByRegionId(regionId: number): Observable<Trail[]> {
    return this.http.get<Trail[]>(`${this.baseUrl}/${regionId}/trails`);
  }

  public getRegionStatistics(): Observable<RegionStatistics> {
    return this.http.get<RegionStatistics>(`${this.baseUrl}/statistics`);
  }
}
