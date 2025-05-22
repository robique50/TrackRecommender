import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import * as L from 'leaflet';
import { TrailService } from '../../services/trail/trail.service';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';


@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MainNavbarComponent],
  templateUrl: './map.component.html',
  styleUrl: './map.component.scss'
})
export class MapComponent implements OnInit {
  private map!: L.Map;
  
  constructor(private trailService: TrailService) { }
  
  ngOnInit(): void {
    this.initMap();
    this.loadTrails();
  }
  
  private initMap(): void {
    this.map = L.map('map').setView([45.9443, 25.0094], 7);
    
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    }).addTo(this.map);
  }
  
  private loadTrails(): void {
    this.trailService.getTrails().subscribe(trails => {
      trails.forEach(trail => {
        try {
          const geoJson = JSON.parse(trail.geoJsonData);
          
          L.geoJSON(geoJson, {
            style: {
              color: this.getTrailColor(trail.difficulty),
              weight: 3,
              opacity: 0.7
            },
            onEachFeature: (feature, layer) => {
              layer.bindPopup(`
                <div>
                  <h3>${trail.name}</h3>
                  <p>Distance: ${trail.distance} km</p>
                  <p>Duration: ${trail.duration} ore</p>
                  <p>Difficulty: ${trail.difficulty}</p>
                </div>
              `);
            }
          }).addTo(this.map);
        } catch (error) {
          console.error(`Error displaying trail ${trail.id}:`, error);
        }
      });
    });
  }
  
  private getTrailColor(difficulty: string): string {
    switch(difficulty) {
      case 'Easy': return '#4CAF50'; 
      case 'Moderate': return '#FFC107'; 
      case 'Difficult': return '#FF9800'; 
      case 'Very Difficult': case 'Expert': return '#F44336'; 
      default: return '#2196F3';
    }
  }
}
