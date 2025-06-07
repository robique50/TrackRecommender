import { Component, OnInit, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import * as L from 'leaflet';
import 'leaflet.markercluster';
import { RegionService } from '../../services/region/region.service';
import { TrailService } from '../../services/trail/trail.service';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { Region } from '../../models/region.model';
import { Trail } from '../../models/trail.model';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { TrailReviewComponent } from '../trail-review/trail-review.component';
import { MapMode } from '../../helpers/map-mode';

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MainNavbarComponent, TrailReviewComponent],
  templateUrl: './map.component.html',
  styleUrls: ['./map.component.scss'],
})
export class MapComponent implements OnInit, AfterViewInit, OnDestroy {
  private map!: L.Map;
  protected MapMode = MapMode;
  protected currentMode: MapMode = MapMode.ALL_TRAILS;

  private allTrails: Trail[] = [];
  protected regions: Region[] = [];
  protected selectedRegion: Region | null = null;
  protected regionTrails: Trail[] = [];
  protected regionDifficultyCache: Map<number, string> = new Map();

  protected visibleTrails: Trail[] = [];
  protected selectedTrail: Trail | null = null;
  protected isLoadingTrails = true;
  protected showReviewPanel = false;

  private updateSubject = new Subject<void>();

  private markerClusterGroup!: L.MarkerClusterGroup;
  private regionBoundariesLayer = new L.LayerGroup();
  private regionMarkersLayer = new L.LayerGroup();
  private regionTrailsLayer = new L.LayerGroup();
  private currentTrailPathLayer = new L.LayerGroup();
  private regionClustersLayer!: L.MarkerClusterGroup;

  protected loading = false;
  protected error: string | null = null;
  protected sidebarCollapsed = false;

  constructor(
    private regionService: RegionService,
    private trailService: TrailService
  ) {}

  ngOnInit(): void {
    this.loadRegions();
    this.setupDebounce();
  }

  ngAfterViewInit(): void {
    setTimeout(() => {
      this.initializeMap();
      this.setupMarkerCluster();
      this.setupRegionClusters();

      this.setMapMode(MapMode.ALL_TRAILS);
    }, 100);
  }

  ngOnDestroy(): void {
    this.updateSubject.complete();
  }

  private initializeMap(): void {
    const mapElement = document.getElementById('map');
    if (!mapElement) {
      console.error('Map element not found!');
      setTimeout(() => this.initializeMap(), 100);
      return;
    }

    this.map = L.map('map').setView([45.9443, 25.0094], 7);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 18,
    }).addTo(this.map);

    setTimeout(() => {
      this.map.invalidateSize();
    }, 100);

    this.regionBoundariesLayer.addTo(this.map);
    this.regionMarkersLayer.addTo(this.map);
    this.regionTrailsLayer.addTo(this.map);
    this.currentTrailPathLayer.addTo(this.map);

    this.map.on('click', (e) => {
      if (
        (e as any).originalEvent.target ===
        this.map.getContainer().querySelector('.leaflet-map-pane')
      ) {
        this.clearTrailSelection();
      }
    });
  }

  private setupMarkerCluster(): void {
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
      if (this.currentMode === MapMode.ALL_TRAILS) {
        this.updateSubject.next();
      }
    });

    this.map.on('zoomend', () => {
      if (this.currentMode === MapMode.ALL_TRAILS) {
        this.updateSubject.next();
      }
    });
  }

  private setupRegionClusters(): void {
    this.regionClustersLayer = L.markerClusterGroup({
      chunkedLoading: true,
      maxClusterRadius: 80,
      spiderfyOnMaxZoom: true,
      showCoverageOnHover: false,
      zoomToBoundsOnClick: true,
      iconCreateFunction: (cluster) => {
        const markers = cluster.getAllChildMarkers();
        let totalTrails = 0;
        const difficulties: string[] = [];

        markers.forEach((marker) => {
          totalTrails += (marker as any).trailCount || 0;
          const trailData = (marker as any).trailData;
          if (trailData?.difficulty) {
            difficulties.push(trailData.difficulty);
          }
        });

        const difficultyCount: { [key: string]: number } = {};
        difficulties.forEach((diff) => {
          difficultyCount[diff] = (difficultyCount[diff] || 0) + 1;
        });

        let predominant = 'Easy';
        let maxCount = 0;
        Object.entries(difficultyCount).forEach(([difficulty, count]) => {
          if (count > maxCount) {
            maxCount = count;
            predominant = difficulty;
          }
        });

        const color = this.getDifficultyColor(predominant);

        return L.divIcon({
          html: `<div style="
          background: ${color};
          width: 50px;
          height: 50px;
          border-radius: 50%;
          display: flex;
          align-items: center;
          justify-content: center;
          color: white;
          font-weight: bold;
          font-size: 16px;
          border: 3px solid white;
          box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
        ">${totalTrails}</div>`,
          className: 'custom-cluster-icon',
          iconSize: L.point(50, 50, true),
        });
      },
    });
  }

  private setupDebounce(): void {
    this.updateSubject.pipe(debounceTime(300)).subscribe(() => {
      this.updateVisibleTrails();
    });
  }

  protected setMapMode(mode: MapMode): void {
    if (this.currentMode === mode && mode !== MapMode.ALL_TRAILS) return;

    this.currentMode = mode;
    this.error = null;
    this.clearAllLayers();

    switch (mode) {
      case MapMode.ALL_TRAILS:
        this.loadAllTrails();
        break;
      case MapMode.REGION_EXPLORER:
        this.loadRegionExplorer();
        break;
      case MapMode.REGION_FOCUSED:
        if (this.selectedRegion) {
          this.loadRegionFocused(this.selectedRegion.id);
        }
        break;
    }
  }

  private loadRegions(): void {
    this.regionService.getAllRegions().subscribe({
      next: (regions) => {
        this.regions = regions;
      },
      error: (error) => {
        this.error = 'Failed to load regions';
        console.error('Error loading regions:', error);
      },
    });
  }

  private loadAllTrails(): void {
    this.loading = true;
    this.isLoadingTrails = true;

    this.trailService.getTrails().subscribe({
      next: (trails) => {
        this.allTrails = trails;
        this.addTrailsToMap();
        this.updateVisibleTrails();
        this.loading = false;
        this.isLoadingTrails = false;
      },
      error: (error) => {
        this.error = 'Failed to load trails';
        this.loading = false;
        this.isLoadingTrails = false;
        console.error('Error loading trails:', error);
      },
    });
  }

  private loadRegionExplorer(): void {
    this.regions.sort((a, b) => a.trailCount - b.trailCount);

    this.addRegionBoundariesToMap();
  }

  private loadRegionFocused(regionId: number): void {
    this.loading = true;

    if (this.regionClustersLayer) {
      this.map.removeLayer(this.regionClustersLayer);
      this.regionClustersLayer.clearLayers();
    }

    this.regionService.getTrailsByRegionId(regionId).subscribe({
      next: (trails) => {
        this.regionTrails = trails;
        this.addRegionBoundaryToMap(regionId);
        this.addRegionTrailClusters(trails);
        this.zoomToRegion(regionId);
        this.loading = false;
      },
      error: (error) => {
        this.error = 'Failed to load region trails';
        this.loading = false;
        console.error('Error loading region trails:', error);
      },
    });
  }

  private addRegionTrailClusters(trails: Trail[]): void {
    this.regionClustersLayer.clearLayers();

    trails.forEach((trail) => {
      try {
        const startLatLng = this.getStartCoordinatesFromGeoJson(
          trail.geoJsonData
        );
        if (!startLatLng) return;

        const marker = L.marker(startLatLng, {
          icon: this.createTrailIcon(trail.difficulty),
        });

        (marker as any).trailData = trail;
        (marker as any).trailCount = 1;

        marker.on('click', () => {
          this.onTrailClick(trail);
          this.highlightTrailPath(trail);
        });

        marker.on('dblclick', (e) => {
          L.DomEvent.stopPropagation(e);
          this.clearSelection();
        });

        this.regionClustersLayer.addLayer(marker);
      } catch (error) {
        console.error(`Error adding trail ${trail.id} to cluster:`, error);
      }
    });

    this.map.addLayer(this.regionClustersLayer);
  }

  private addTrailsToMap(): void {
    this.markerClusterGroup.clearLayers();

    let successCount = 0;
    let errorCount = 0;

    this.allTrails.forEach((trail: Trail) => {
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
          this.onTrailClickFromMarker(trail);
        });

        marker.on('dblclick', (e) => {
          L.DomEvent.stopPropagation(e);
          this.clearSelection();
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

  private highlightTrailPath(trail: Trail): void {
    this.clearTrailSelection();

    try {
      const geoJson = JSON.parse(trail.geoJsonData);
      const feature: GeoJSON.Feature<GeoJSON.Geometry> = {
        type: 'Feature',
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

      highlightLayer.on('dblclick', (e) => {
        L.DomEvent.stopPropagation(e);
        this.clearSelection();
      });

      highlightLayer.addTo(this.map);
      this.selectedTrail = trail;
    } catch (error) {
      console.error('Error highlighting trail:', error);
    }
  }

  private updateVisibleTrails(): void {
    if (this.currentMode !== MapMode.ALL_TRAILS) return;

    const bounds = this.map.getBounds();

    this.visibleTrails = this.allTrails.filter((trail: Trail) => {
      try {
        const startLatLng = this.getStartCoordinatesFromGeoJson(
          trail.geoJsonData
        );
        return startLatLng ? bounds.contains(startLatLng) : false;
      } catch {
        return false;
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

    const color = this.getDifficultyColor(predominantDifficulty);
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
    const color = this.getDifficultyColor(difficulty);

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

  private getStartCoordinatesFromGeoJson(geoJsonData: string): L.LatLng | null {
    try {
      const geoJson = JSON.parse(geoJsonData);

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

  private onTrailClickFromMarker(trail: Trail): void {
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

      highlightLayer.on('dblclick', (e) => {
        L.DomEvent.stopPropagation(e);
        this.clearSelection();
      });

      highlightLayer.addTo(this.map);
      this.selectedTrail = trail;

      this.scrollToTrailInSidebar(trail);

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

  private getPredominantDifficulty(trails: Trail[]): string {
    if (!trails || trails.length === 0) return 'Easy';

    const difficultyCount: { [key: string]: number } = {};

    trails.forEach((trail) => {
      const difficulty = trail.difficulty || 'Unknown';
      difficultyCount[difficulty] = (difficultyCount[difficulty] || 0) + 1;
    });

    let predominant = 'Easy';
    let maxCount = 0;

    Object.entries(difficultyCount).forEach(([difficulty, count]) => {
      if (count > maxCount) {
        maxCount = count;
        predominant = difficulty;
      }
    });

    return predominant;
  }

  private scrollToTrailInSidebar(trail: Trail): void {
    setTimeout(() => {
      const trailElements = document.querySelectorAll('.trail-item');
      trailElements.forEach((element) => {
        const nameElement = element.querySelector('.trail-name');
        if (nameElement && nameElement.textContent?.trim() === trail.name) {
          element.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
      });
    }, 100);
  }

  protected selectTrailFromSidebar(trail: Trail): void {
    this.markerClusterGroup.eachLayer((layer) => {
      if ((layer as any).trailData?.id === trail.id) {
        this.onTrailClickFromMarker(trail);

        const marker = layer as L.Marker;
        const markerLatLng = marker.getLatLng();
        if (!this.map.getBounds().contains(markerLatLng)) {
          this.map.setView(markerLatLng, Math.max(this.map.getZoom(), 12));
        }
      }
    });
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

  private addRegionBoundariesToMap(): void {
    this.regionBoundariesLayer.clearLayers();
    this.regionMarkersLayer.clearLayers();

    this.regions.forEach((region) => {
      if (region.boundaryGeoJson) {
        try {
          const geoJson = JSON.parse(region.boundaryGeoJson);
          const geoJsonLayer = L.geoJSON(geoJson, {
            style: {
              color: '#4a8c50',
              weight: 2,
              opacity: 0.8,
              fillOpacity: 0.1,
              fillColor: '#4a8c50',
            },
          });

          geoJsonLayer.on('click', () => this.selectRegion(region.id));
          this.regionBoundariesLayer.addLayer(geoJsonLayer);

          if (region.trailCount > 0) {
            const center = this.getRegionCenter(geoJsonLayer);

            if (this.regionDifficultyCache.has(region.id)) {
              const color = this.getDifficultyColor(
                this.regionDifficultyCache.get(region.id)!
              );
              this.addRegionMarker(center, region, color);
            } else {
              this.regionService
                .getTrailsByRegionId(region.id)
                .subscribe((trails) => {
                  const predominantDifficulty =
                    this.getPredominantDifficulty(trails);
                  this.regionDifficultyCache.set(
                    region.id,
                    predominantDifficulty
                  );
                  const color = this.getDifficultyColor(predominantDifficulty);
                  this.addRegionMarker(center, region, color);
                });
            }
          }
        } catch (error) {
          console.error(`Error parsing region ${region.id} boundary:`, error);
        }
      }
    });
  }

  private addRegionMarker(
    center: L.LatLng,
    region: Region,
    color?: string
  ): void {
    const markerColor = color || this.getRegionColor(region.trailCount);

    const marker = L.marker(center, {
      icon: L.divIcon({
        html: `<div style="
        background: ${markerColor};
        width: 40px;
        height: 40px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        color: white;
        font-weight: bold;
        font-size: 16px;
        border: 3px solid white;
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
      ">${region.trailCount}</div>`,
        className: 'custom-region-marker',
        iconSize: L.point(50, 50, true),
        iconAnchor: L.point(25, 25, true),
      }),
    });

    marker.on('click', () => {
      this.selectRegion(region.id);
    });

    this.regionMarkersLayer.addLayer(marker);
  }

  private getRegionCenter(geoJsonLayer: L.GeoJSON): L.LatLng {
    const bounds = geoJsonLayer.getBounds();
    return bounds.getCenter();
  }

  protected toggleSidebar(): void {
    this.sidebarCollapsed = !this.sidebarCollapsed;

    setTimeout(() => {
      if (this.map) {
        this.map.invalidateSize();
      }
    }, 350);
  }

  private addRegionBoundaryToMap(regionId: number): void {
    const region = this.regions.find((r) => r.id === regionId);
    if (region?.boundaryGeoJson) {
      try {
        const geoJsonLayer = L.geoJSON(JSON.parse(region.boundaryGeoJson), {
          style: {
            color: '#2e5d32',
            weight: 3,
            opacity: 0.8,
            fillOpacity: 0,
            fillColor: 'transparent',
          },
        });

        this.regionBoundariesLayer.addLayer(geoJsonLayer);
      } catch (error) {
        console.error(`Error adding boundary for region ${regionId}:`, error);
      }
    }
  }

  protected selectRegion(regionId: number): void {
    const region = this.regions.find((r) => r.id === regionId);
    if (!region) return;

    this.selectedRegion = region;
    this.setMapMode(MapMode.REGION_FOCUSED);
  }

  protected onTrailClick(trail: Trail): void {
    this.selectedTrail = trail;
    this.highlightTrailPath(trail);
    this.scrollToTrailInSidebar(trail);
  }

  protected showTrailPath(trailId: number): void {
    this.trailService.getTrailById(trailId).subscribe({
      next: (fullTrail) => {
        this.displayTrailPath(fullTrail);
      },
      error: (error) => {
        console.error('Error loading trail path:', error);
      },
    });
  }

  private displayTrailPath(trail: Trail): void {
    if (!trail.geoJsonData) return;

    try {
      this.currentTrailPathLayer.clearLayers();

      const geoJsonLayer = L.geoJSON(JSON.parse(trail.geoJsonData), {
        style: {
          color: '#ff4444',
          weight: 4,
          opacity: 0.8,
        },
      });

      this.currentTrailPathLayer.addLayer(geoJsonLayer);
      this.map.fitBounds(geoJsonLayer.getBounds());
    } catch (error) {
      console.error('Error displaying trail path:', error);
    }
  }

  protected openReviewPanel(): void {
    this.showReviewPanel = true;
  }

  protected closeReviewPanel(): void {
    this.showReviewPanel = false;
  }

  protected onReviewSubmitted(review: any): void {
    this.closeReviewPanel();
    if (this.currentMode === MapMode.ALL_TRAILS) {
      this.loadAllTrails();
    }
  }

  private clearAllLayers(): void {
    if (this.markerClusterGroup) {
      this.markerClusterGroup.clearLayers();
    }

    if (this.regionClustersLayer) {
      this.map.removeLayer(this.regionClustersLayer);
      this.regionClustersLayer.clearLayers();
    }

    this.regionBoundariesLayer.clearLayers();
    this.regionMarkersLayer.clearLayers();
    this.regionTrailsLayer.clearLayers();
    this.currentTrailPathLayer.clearLayers();

    this.clearTrailSelection();
  }

  private zoomToRegion(regionId: number): void {
    const region = this.regions.find((r) => r.id === regionId);
    if (region?.boundaryGeoJson) {
      try {
        const boundary = JSON.parse(region.boundaryGeoJson);
        const geoJsonLayer = L.geoJSON(boundary);
        this.map.fitBounds(geoJsonLayer.getBounds());
      } catch (error) {
        console.error('Error zooming to region:', error);
      }
    }
  }

  protected getDifficultyColor(difficulty: string): string {
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

  protected getRegionColor(trailCount: number): string {
    if (trailCount === 0) return '#cccccc';
    if (trailCount < 5) return '#ffffcc';
    if (trailCount < 10) return '#c7e9b4';
    if (trailCount < 20) return '#7fcdbb';
    if (trailCount < 50) return '#41b6c4';
    return '#2c7fb8';
  }

  protected getMarkerSize(trailCount: number): number {
    return Math.min(Math.max(trailCount / 2 + 5, 8), 25);
  }

  protected clearSelection(): void {
    this.clearTrailSelection();
    this.selectedTrail = null;
  }

  protected getSortedRegions(): Region[] {
    return [...this.regions].sort((a, b) => b.trailCount - a.trailCount);
  }

  protected formatDuration(hours: number): string {
    if (!hours) return '0h';
    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  protected trackByTrailId(index: number, trail: Trail): number {
    return trail.id;
  }

  protected get isLoading(): boolean {
    return this.loading;
  }

  protected get hasError(): boolean {
    return this.error !== null;
  }

  protected get showBackButton(): boolean {
    return this.currentMode === MapMode.REGION_FOCUSED;
  }

  protected goBackToRegionExplorer(): void {
    this.selectedRegion = null;
    this.setMapMode(MapMode.REGION_EXPLORER);
  }

  protected getTotalTrails(): number {
    return this.regions.reduce((total, region) => total + region.trailCount, 0);
  }

  protected getOtherRegions(regionNames: string[]): string {
    if (!regionNames || regionNames.length <= 1) return '';

    const otherRegions = regionNames.filter(
      (name) => name !== this.selectedRegion?.name
    );

    return otherRegions.join(', ');
  }
}
