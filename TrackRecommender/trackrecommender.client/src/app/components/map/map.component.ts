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

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MainNavbarComponent, TrailReviewComponent],
  templateUrl: './map.component.html',
  styleUrl: './map.component.scss'
})
export class MapComponent implements OnInit, OnDestroy {
  private map!: L.Map;
  private markerClusterGroup!: L.MarkerClusterGroup;
  private updateSubject = new Subject<void>();

  trails: any[] = [];
  visibleTrails: any[] = [];
  selectedTrail: any = null;
  isLoadingTrails = true;
  showReviewPanel = false;

  constructor(
    private trailService: TrailService,
    private reviewService: ReviewService
  ) { }

  ngOnInit(): void {
    this.initMap();
    this.loadTrails();
    this.setupDebounce();
  }

  ngOnDestroy(): void {
    this.updateSubject.complete();
  }

  private setupDebounce(): void {
    this.updateSubject
      .pipe(debounceTime(300))
      .subscribe(() => {
        this.updateVisibleTrails();
      });
  }

  private initMap(): void {
    this.map = L.map('map').setView([45.9443, 25.0094], 7);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© OpenStreetMap contributors'
    }).addTo(this.map);

    this.markerClusterGroup = L.markerClusterGroup({
      chunkedLoading: true,
      maxClusterRadius: 60,
      spiderfyOnMaxZoom: true,
      showCoverageOnHover: false,
      zoomToBoundsOnClick: true,
      iconCreateFunction: (cluster) => this.createClusterIcon(cluster)
    });

    this.map.addLayer(this.markerClusterGroup);

    this.map.on('moveend', () => {
      this.updateSubject.next();
    });

    this.map.on('zoomend', () => {
      this.updateSubject.next();
    });

    this.map.on('click', (e) => {
      if ((e as any).originalEvent.target === this.map.getContainer().querySelector('.leaflet-map-pane')) {
        this.clearTrailSelection();
      }
    });
  }

  private createClusterIcon(cluster: L.MarkerCluster): L.DivIcon {
    const markers = cluster.getAllChildMarkers();
    const difficulties = markers.map(marker => (marker as any).trailData?.difficulty);

    const difficultyCount: { [key: string]: number } = {};
    difficulties.forEach(diff => {
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
      iconSize: L.point(40, 40, true)
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
      iconAnchor: L.point(8, 8, true)
    });
  }

  private loadTrails(): void {
    this.isLoadingTrails = true;

    this.trailService.getTrails().subscribe({
      next: (trails) => {
        this.trails = trails;
        this.addTrailsToMap();
        this.updateVisibleTrails();
        this.isLoadingTrails = false;
      },
      error: (error) => {
        console.error('Error loading trails:', error);
        this.isLoadingTrails = false;
      }
    });
  }

  private addTrailsToMap(): void {
    this.markerClusterGroup.clearLayers();

    this.trails.forEach(trail => {
      try {
        const geoJson = JSON.parse(trail.geoJsonData);

        const startCoordinates = geoJson.coordinates[0];
        const startLatLng = L.latLng(startCoordinates[1], startCoordinates[0]);

        const marker = L.marker(startLatLng, {
          icon: this.createTrailIcon(trail.difficulty)
        });

        (marker as any).trailData = trail;

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

    this.visibleTrails = this.trails.filter(trail => {
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
    this.clearTrailSelection();

    try {
      const geoJson = JSON.parse(trail.geoJsonData);
      const highlightLayer = L.geoJSON(geoJson, {
        style: {
          color: '#FF1744',
          weight: 4,
          opacity: 0.9,
          dashArray: '5, 5'
        }
      });

      (highlightLayer as any).isTrailHighlight = true;
      highlightLayer.addTo(this.map);

      this.selectedTrail = trail;

      const bounds = highlightLayer.getBounds();
      if (bounds.isValid()) {
        this.map.fitBounds(bounds, {
          padding: [20, 20],
          maxZoom: 10
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

    layersToRemove.forEach(layer => {
      this.map.removeLayer(layer);
    });

    this.selectedTrail = null;
  }

  clearSelection(): void {
    this.clearTrailSelection();
  }

  selectTrailFromSidebar(trail: any): void {
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
      case 'easy': return '#4CAF50';
      case 'moderate': return '#FFC107';
      case 'difficult': return '#FF9800';
      case 'very difficult': return '#F44336';
      case 'expert': return '#9C27B0';
      default: return '#2196F3';
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
    this.loadTrails();
  }
}
