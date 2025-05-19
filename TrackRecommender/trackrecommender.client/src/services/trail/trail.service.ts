import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class TrailService {

  constructor(private http: HttpClient) { }

  getTrails(): Observable<any[]> {
    return this.http.get<any[]>('http://localhost:5219/api/mapdata/trails');
  }
  
  getTrailById(id: number): Observable<any> {
    return this.http.get<any>(`http://localhost:5219/api/mapdata/trails/${id}`);
  }
}
