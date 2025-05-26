import {
  Component,
  OnInit,
  OnDestroy,
  ViewChild,
  ElementRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as L from 'leaflet';
import 'leaflet.markercluster';
import { TrailService } from '../../services/trail/trail.service';
import { ReviewService } from '../../services/review/review.service';
import { Subject, BehaviorSubject } from 'rxjs';
import { debounceTime, takeUntil } from 'rxjs/operators';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { TrailReviewComponent } from '../trail-review/trail-review.component';

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MainNavbarComponent, TrailReviewComponent],
  templateUrl: './map.component.html',
  styleUrl: './map.component.scss',
})
export class MapComponent implements OnInit, OnDestroy {
  private map!: L.Map;
  private markerClusterGroup!: L.MarkerClusterGroup;
  private updateSubject = new Subject<void>();
  private destroy$ = new Subject<void>();

  private trailsCache$ = new BehaviorSubject<any[]>([]);
  private lastFetchTime: number = 0;
  private readonly CACHE_DURATION = 5 * 60 * 1000;

  private activeTrailLayer: L.GeoJSON | null = null;
  private selectedMarkerId: number | null = null;

  trails: any[] = [];
  visibleTrails: any[] = [];
  selectedTrail: any = null;
  isLoadingTrails = false;
  showReviewPanel = false;

  @ViewChild('trailsList') trailsListElement!: ElementRef;

  constructor(
    private trailService: TrailService,
    private reviewService: ReviewService
  ) {}

  ngOnInit(): void {
    this.initMap();
    this.loadTrailsWithCache();
    this.setupDebounce();
    this.setupCacheSubscription();
  }

  ngOnDestroy(): void {
    this.updateSubject.complete();
    this.destroy$.next();
    this.destroy$.complete();
  }

  private setupCacheSubscription(): void {
    this.trailsCache$.pipe(takeUntil(this.destroy$)).subscribe((trails) => {
      if (trails.length > 0) {
        this.trails = trails;
        this.addTrailsToMap();
        this.updateVisibleTrails();
      }
    });
  }

  private loadTrailsWithCache(forceRefresh: boolean = false): void {
    const currentTime = Date.now();
    const cachedTrails = this.trailsCache$.value;

    if (
      !forceRefresh &&
      cachedTrails.length > 0 &&
      currentTime - this.lastFetchTime < this.CACHE_DURATION
    ) {
      return;
    }

    this.isLoadingTrails = true;

    this.trailService.getTrails().subscribe({
      next: (trails) => {
        this.lastFetchTime = currentTime;
        this.trailsCache$.next(trails);
        this.isLoadingTrails = false;
      },
      error: (error) => {
        console.error('Error loading trails:', error);
        this.isLoadingTrails = false;
      },
    });
  }

  private setupDebounce(): void {
    this.updateSubject
      .pipe(debounceTime(300), takeUntil(this.destroy$))
      .subscribe(() => {
        this.updateVisibleTrails();
      });
  }

  private initMap(): void {
    this.map = L.map('map').setView([45.9443, 25.0094], 7);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors',
    }).addTo(this.map);

    this.markerClusterGroup = L.markerClusterGroup({
      chunkedLoading: true,
      maxClusterRadius: 60,
      spiderfyOnMaxZoom: true,
      showCoverageOnHover: false,
      zoomToBoundsOnClick: true,
      iconCreateFunction: (cluster) => this.createClusterIcon(cluster),
    });

    this.map.addLayer(this.markerClusterGroup);

    this.map.on('moveend', () => {
      this.updateSubject.next();
    });

    this.map.on('zoomend', () => {
      this.updateSubject.next();
    });

    this.map.on('click', (e: L.LeafletMouseEvent) => {
      const target = e.originalEvent.target as HTMLElement;
      if (
        target.classList.contains('leaflet-container') ||
        target.closest('.leaflet-tile-pane') ||
        (target.closest('.leaflet-overlay-pane') &&
          !target.closest('.leaflet-marker-icon'))
      ) {
        this.clearTrailSelection();
      }
    });

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        this.clearTrailSelection();
      }
    });
  }

  private createClusterIcon(cluster: L.MarkerCluster): L.DivIcon {
    const markers = cluster.getAllChildMarkers();
    const difficulties = markers.map(
      (marker) => (marker as any).trailData?.difficulty
    );

    const difficultyCount: { [key: string]: number } = {};
    difficulties.forEach((diff) => {
      if (diff) {
        difficultyCount[diff] = (difficultyCount[diff] || 0) + 1;
      }
    });

    const predominantDifficulty = Object.keys(difficultyCount).reduce((a, b) =>
      difficultyCount[a] > difficultyCount[b] ? a : b
    );

    const color = this.getTrailColor(predominantDifficulty);
    const count = cluster.getChildCount();

    return L.divIcon({
      html: `<div style="
        background-color: ${color};
        width: 40px;
        height: 40px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        color: white;
        font-weight: bold;
        font-size: 14px;
        border: 3px solid white;
        box-shadow: 0 2px 6px rgba(0,0,0,0.3);
      ">${count}</div>`,
      className: 'custom-cluster-icon',
      iconSize: L.point(40, 40, true),
    });
  }

  private createTrailIcon(
    difficulty: string,
    isSelected: boolean = false
  ): L.DivIcon {
    const color = this.getTrailColor(difficulty);
    const size = isSelected ? 20 : 16;
    const borderWidth = isSelected ? 3 : 2;

    return L.divIcon({
      html: `<div style="
        background-color: ${color};
        width: ${size}px;
        height: ${size}px;
        border-radius: 50%;
        border: ${borderWidth}px solid white;
        box-shadow: 0 2px 4px rgba(0,0,0,0.3);
        ${isSelected ? 'animation: pulse 2s infinite;' : ''}
      "></div>`,
      className: 'custom-trail-icon',
      iconSize: L.point(size, size, true),
      iconAnchor: L.point(size / 2, size / 2, true),
    });
  }

  private addTrailsToMap(): void {
    this.markerClusterGroup.clearLayers();

    this.trails.forEach((trail) => {
      try {
        const geoJson = JSON.parse(trail.geoJsonData);
        const startCoordinates = geoJson.coordinates[0];
        const startLatLng = L.latLng(startCoordinates[1], startCoordinates[0]);

        const marker = L.marker(startLatLng, {
          icon: this.createTrailIcon(trail.difficulty),
        });

        (marker as any).trailData = trail;
        (marker as any).trailId = trail.id;

        marker.on('click', () => {
          this.onTrailClick(trail);
        });

        this.markerClusterGroup.addLayer(marker);
      } catch (error) {
        console.error(`Error processing trail ${trail.id}:`, error);
      }
    });
  }

  private updateVisibleTrails(): void {
    const bounds = this.map.getBounds();

    this.visibleTrails = this.trails.filter((trail) => {
      try {
        const geoJson = JSON.parse(trail.geoJsonData);
        const startCoordinates = geoJson.coordinates[0];
        const latLng = L.latLng(startCoordinates[1], startCoordinates[0]);
        return bounds.contains(latLng);
      } catch {
        return false;
      }
    });
  }

  private onTrailClick(trail: any): void {
    if (this.selectedTrail?.id === trail.id) {
      this.clearTrailSelection();
      return;
    }

    this.clearTrailSelection();

    try {
      const geoJson = JSON.parse(trail.geoJsonData);
      const highlightLayer = L.geoJSON(geoJson, {
        style: {
          color: '#FF1744',
          weight: 5,
          opacity: 0.9,
        },
      });

      this.activeTrailLayer = highlightLayer;
      highlightLayer.addTo(this.map);

      this.selectedTrail = trail;
      this.selectedMarkerId = trail.id;

      this.updateMarkerIcon(trail.id, true);

      const bounds = highlightLayer.getBounds();
      if (bounds.isValid()) {
        this.map.fitBounds(bounds, {
          padding: [50, 50],
          maxZoom: 13,
        });
      }

      this.scrollToSelectedTrail();
    } catch (error) {
      console.error('Error highlighting trail:', error);
    }
  }

  private updateMarkerIcon(trailId: number, isSelected: boolean): void {
    this.markerClusterGroup.eachLayer((layer) => {
      if ((layer as any).trailId === trailId) {
        const marker = layer as L.Marker;
        const trailData = (marker as any).trailData;
        marker.setIcon(this.createTrailIcon(trailData.difficulty, isSelected));
      }
    });
  }

  private clearTrailSelection(): void {
    if (this.activeTrailLayer) {
      this.map.removeLayer(this.activeTrailLayer);
      this.activeTrailLayer = null;
    }

    if (this.selectedMarkerId) {
      this.updateMarkerIcon(this.selectedMarkerId, false);
      this.selectedMarkerId = null;
    }

    this.selectedTrail = null;
    this.showReviewPanel = false;
  }

  private scrollToSelectedTrail(): void {
    setTimeout(() => {
      const selectedElement = document.querySelector('.trail-item.selected');
      if (selectedElement) {
        selectedElement.scrollIntoView({
          behavior: 'smooth',
          block: 'center',
        });
      }
    }, 100);
  }

  clearSelection(): void {
    this.clearTrailSelection();
  }

  selectTrailFromSidebar(trail: any): void {
    this.markerClusterGroup.eachLayer((layer) => {
      if ((layer as any).trailData?.id === trail.id) {
        const marker = layer as L.Marker;
        const markerLatLng = marker.getLatLng();

        this.map.setView(markerLatLng, Math.max(this.map.getZoom(), 12), {
          animate: true,
        });

        setTimeout(() => {
          this.onTrailClick(trail);
        }, 500);
      }
    });
  }

  private getTrailColor(difficulty: string): string {
    switch (difficulty?.toLowerCase()) {
      case 'easy':
        return '#4CAF50';
      case 'moderate':
        return '#FFC107';
      case 'difficult':
        return '#FF9800';
      case 'very difficult':
        return '#F44336';
      case 'expert':
        return '#9C27B0';
      default:
        return '#2196F3';
    }
  }

  getDifficultyColor(difficulty: string): string {
    return this.getTrailColor(difficulty);
  }

  formatDuration(hours: number): string {
    return this.reviewService.formatDuration(hours);
  }

  trackByTrailId(index: number, trail: any): number {
    return trail.id;
  }

  openReviewPanel(): void {
    this.showReviewPanel = true;
  }

  closeReviewPanel(): void {
    this.showReviewPanel = false;
  }

  onReviewSubmitted(review: any): void {
    this.closeReviewPanel();
    if (this.selectedTrail) {
      this.selectedTrail.hasReview = true;
      this.selectedTrail.userRating = review.rating;
    }
  }

  refreshTrails(): void {
    this.loadTrailsWithCache(true);
  }
}
