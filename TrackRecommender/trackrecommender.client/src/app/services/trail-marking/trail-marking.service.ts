import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of } from 'rxjs';
import { tap, map, catchError } from 'rxjs/operators';
import { TrailMarking } from '../../models/trail-marking.model';
import { OsmcColorsHelper } from '../../shared/osmc-colors.helper';

@Injectable({
  providedIn: 'root',
})
export class TrailMarkingService {
  private markingsCache$ = new BehaviorSubject<TrailMarking[]>([]);
  private isLoaded = false;

  constructor(private http: HttpClient) {}

  loadAllMarkings(): Observable<TrailMarking[]> {
    if (this.isLoaded) {
      return this.markingsCache$.asObservable();
    }

    return this.http.get<TrailMarking[]>('/api/trailmarkings').pipe(
      map((markings) =>
        markings.map((marking) => ({
          ...marking,
          backgroundColor: OsmcColorsHelper.getColorHex(
            marking.backgroundColor
          ),
          foregroundColor: OsmcColorsHelper.getColorHex(
            marking.foregroundColor
          ),
          shapeColor: marking.shapeColor
            ? OsmcColorsHelper.getColorHex(marking.shapeColor)
            : undefined,
          displayName: OsmcColorsHelper.formatMarkingName(marking),
        }))
      ),
      tap((markings) => {
        this.markingsCache$.next(markings);
        this.isLoaded = true;
      }),
      catchError((error) => {
        console.error('Error loading trail markings:', error);
        return of([]);
      })
    );
  }

  getAllMarkings(): Observable<TrailMarking[]> {
    if (this.isLoaded) {
      return this.markingsCache$.asObservable();
    }

    return this.loadAllMarkings();
  }

  getUniqueColors(): Observable<string[]> {
    return this.getAllMarkings().pipe(
      map((markings) => {
        const colors = new Set<string>();
        markings.forEach((m) => {
          if (m.foregroundColor) colors.add(m.foregroundColor);
          if (m.backgroundColor && m.backgroundColor !== '#FFFFFF')
            colors.add(m.backgroundColor);
          if (m.shapeColor) colors.add(m.shapeColor);
        });
        return Array.from(colors).sort();
      })
    );
  }

  getUniqueShapes(): Observable<string[]> {
    return this.getAllMarkings().pipe(
      map((markings) => {
        const shapes = new Set<string>();
        markings.forEach((m) => {
          if (m.shape) shapes.add(m.shape);
        });
        return Array.from(shapes).sort();
      })
    );
  }

  getColorNameRo(color: string): string {
    return OsmcColorsHelper.getColorNameRo(color);
  }

  getShapeNameRo(shape: string): string {
    return OsmcColorsHelper.getShapeNameRo(shape);
  }
}
