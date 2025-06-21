import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class MapService {
  private selectedTrailSubject = new BehaviorSubject<any>(null);
  public selectedTrail$ = this.selectedTrailSubject.asObservable();

  private mapModeSubject = new BehaviorSubject<string>('all-trails');
  public mapMode$ = this.mapModeSubject.asObservable();

  constructor() {}

  public setSelectedTrail(trail: any): void {
    this.selectedTrailSubject.next(trail);
  }

  public getSelectedTrail(): any {
    return this.selectedTrailSubject.value;
  }

  public clearSelectedTrail(): void {
    this.selectedTrailSubject.next(null);
  }

  public setMapMode(mode: string): void {
    this.mapModeSubject.next(mode);
  }
}
