import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import * as L from 'leaflet';
import 'leaflet.markercluster';
import { TrailService } from '../../services/trail/trail.service';
import { ReviewService } from '../../services/review/review.service';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { TrailReviewComponent } from '../trail-review/trail-review.component';
import { Trail } from '../../models/trail.model';

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

  protected trails: Trail[] = [];
  protected visibleTrails: Trail[] = [];
  protected selectedTrail: Trail | null = null;
  protected isLoadingTrails = true;
  protected showReviewPanel = false;

  constructor(
    private trailService: TrailService,
    private reviewService: ReviewService
  ) {}

  ngOnInit(): void {
    this.initMap();
    this.loadTrails();
    this.setupDebounce();
  }

  ngOnDestroy(): void {
    this.updateSubject.complete();
  }

  private setupDebounce(): void {
    this.updateSubject.pipe(debounceTime(300)).subscribe(() => {
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

    this.map.on('click', (e) => {
      if (
        (e as any).originalEvent.target ===
        this.map.getContainer().querySelector('.leaflet-map-pane')
      ) {
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

  private createTrailIcon(difficulty: string): L.DivIcon {
    const color = this.getTrailColor(difficulty);

    return L.divIcon({
      html: `<div style="
        background-color: ${color};
        width: 16px;
        height: 16px;
        border-radius: 50%;
        border: 2px solid white;
        box-shadow: 0 2px 4px rgba(0,0,0,0.3);
      "></div>`,
      className: 'custom-trail-icon',
      iconSize: L.point(16, 16, true),
      iconAnchor: L.point(8, 8, true),
    });
  }

  private loadTrails(): void {
    this.isLoadingTrails = true;

    this.trailService.getTrails().subscribe({
      next: (trails: Trail[]) => {
        this.trails = trails;
        this.addTrailsToMap();
        this.updateVisibleTrails();
        this.isLoadingTrails = false;
      },
      error: (error) => {
        console.error('Error loading trails:', error);
        this.isLoadingTrails = false;
      },
    });
  }

  private getStartCoordinatesFromGeoJson(geoJsonData: string): L.LatLng | null {
    try {
      const geoJson = JSON.parse(geoJsonData);

      if (!geoJson || !geoJson.coordinates) {
        console.warn('Invalid GeoJSON structure: missing coordinates');
        return null;
      }

      let startCoordinates: [number, number] | null = null;

      switch (geoJson.type) {
        case 'LineString':
          if (
            Array.isArray(geoJson.coordinates) &&
            geoJson.coordinates.length > 0
          ) {
            startCoordinates = geoJson.coordinates[0];
          }
          break;

        case 'MultiLineString':
          if (
            Array.isArray(geoJson.coordinates) &&
            geoJson.coordinates.length > 0 &&
            Array.isArray(geoJson.coordinates[0]) &&
            geoJson.coordinates[0].length > 0
          ) {
            startCoordinates = geoJson.coordinates[0][0];
          }
          break;

        default:
          console.warn(`Unsupported geometry type: ${geoJson.type}`);
          return null;
      }

      if (
        startCoordinates &&
        Array.isArray(startCoordinates) &&
        startCoordinates.length >= 2
      ) {
        return L.latLng(startCoordinates[1], startCoordinates[0]);
      }

      return null;
    } catch (error) {
      console.error('Error parsing GeoJSON:', error);
      return null;
    }
  }

  private addTrailsToMap(): void {
    this.markerClusterGroup.clearLayers();

    let successCount = 0;
    let errorCount = 0;

    this.trails.forEach((trail: Trail) => {
      try {
        const startLatLng = this.getStartCoordinatesFromGeoJson(
          trail.geoJsonData
        );

        if (!startLatLng) {
          errorCount++;
          return;
        }

        const marker = L.marker(startLatLng, {
          icon: this.createTrailIcon(trail.difficulty),
        });

        (marker as any).trailData = trail;

        marker.on('click', () => {
          this.onTrailClick(trail);
        });

        this.markerClusterGroup.addLayer(marker);
        successCount++;
      } catch (error) {
        console.error(
          `Error processing trail ${trail.id} (${trail.name}):`,
          error
        );
        errorCount++;
      }
    });
  }

  private updateVisibleTrails(): void {
    const bounds = this.map.getBounds();

    this.visibleTrails = this.trails.filter((trail: Trail) => {
      try {
        const startLatLng = this.getStartCoordinatesFromGeoJson(
          trail.geoJsonData
        );
        return startLatLng ? bounds.contains(startLatLng) : false;
      } catch {
        return false;
      }
    });

    console.log(
      `Visible trails: ${this.visibleTrails.length} of ${this.trails.length}`
    );
  }

  private onTrailClick(trail: Trail): void {
    this.clearTrailSelection();

    try {
      const geoJson = JSON.parse(trail.geoJsonData);

      const feature: GeoJSON.Feature<GeoJSON.Geometry> = {
        type: 'Feature' as const,
        geometry: geoJson,
        properties: {},
      };

      const highlightLayer = L.geoJSON(feature, {
        style: {
          color: '#FF1744',
          weight: 4,
          opacity: 0.9,
          dashArray: '5, 5',
        },
      });

      (highlightLayer as any).isTrailHighlight = true;
      highlightLayer.addTo(this.map);

      this.selectedTrail = trail;

      const bounds = highlightLayer.getBounds();
      if (bounds.isValid()) {
        this.map.fitBounds(bounds, {
          padding: [20, 20],
          maxZoom: 10,
        });
      }
    } catch (error) {
      console.error('Error highlighting trail:', error);
    }
  }

  private clearTrailSelection(): void {
    const layersToRemove: L.Layer[] = [];

    this.map.eachLayer((layer) => {
      if (layer instanceof L.GeoJSON && (layer as any).isTrailHighlight) {
        layersToRemove.push(layer);
      }
    });

    layersToRemove.forEach((layer) => {
      this.map.removeLayer(layer);
    });

    this.selectedTrail = null;
  }

  protected clearSelection(): void {
    this.clearTrailSelection();
  }

  protected selectTrailFromSidebar(trail: Trail): void {
    this.markerClusterGroup.eachLayer((layer) => {
      if ((layer as any).trailData?.id === trail.id) {
        this.onTrailClick(trail);

        const marker = layer as L.Marker;
        const markerLatLng = marker.getLatLng();
        if (!this.map.getBounds().contains(markerLatLng)) {
          this.map.setView(markerLatLng, Math.max(this.map.getZoom(), 12));
        }
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

  protected getDifficultyColor(difficulty: string): string {
    return this.getTrailColor(difficulty);
  }

  protected formatDuration(hours: number): string {
    return this.reviewService.formatDuration(hours);
  }

  protected trackByTrailId(index: number, trail: Trail): number {
    return trail.id;
  }

  protected openReviewPanel(): void {
    this.showReviewPanel = true;
  }

  protected closeReviewPanel(): void {
    this.showReviewPanel = false;
  }

  protected onReviewSubmitted(review: any): void {
    this.closeReviewPanel();
    this.loadTrails();
  }
}
