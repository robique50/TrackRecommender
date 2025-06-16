import { Component, OnInit, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  FormsModule,
} from '@angular/forms';
import { Router } from '@angular/router';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { UserPreferencesService } from '../../services/user-preferences/user-preferences.service';
import { TrailMarkingService } from '../../services/trail-marking/trail-marking.service';
import { trigger, transition, style, animate } from '@angular/animations';
import {
  RegionOption,
  UserPreferences,
} from '../../models/user-preferences.model';
import { TrailMarking } from '../../models/trail-marking.model';
import { MarkingDisplayComponent } from '../marking-display/marking-display.component';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-preferences',
  templateUrl: './preferences.component.html',
  styleUrls: ['./preferences.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MainNavbarComponent,
    MarkingDisplayComponent,
  ],
  animations: [
    trigger('slideIn', [
      transition(':enter', [
        style({ transform: 'translateX(100%)', opacity: 0 }),
        animate(
          '300ms ease-out',
          style({ transform: 'translateX(0)', opacity: 1 })
        ),
      ]),
    ]),
  ],
})
export class PreferencesComponent implements OnInit {
  @ViewChild('regionsContainer') regionsContainer!: ElementRef;

  protected isLoading = true;
  protected hasPreferences = false;
  protected isEditing = false;
  protected isSaving = false;
  protected showResetModal = false;
  protected showSuccessMessage = false;
  protected successMessage = '';

  protected preferencesForm!: FormGroup;
  protected currentPreferences: UserPreferences = {};

  protected availableTrailTypes: string[] = [];
  protected availableDifficulties: string[] = [
    'Easy',
    'Moderate',
    'Difficult',
    'Very Difficult',
    'Expert',
  ];
  protected availableCategories: string[] = [
    'International',
    'National',
    'Regional',
    'Local',
  ];
  protected availableRegions: RegionOption[] = [];

  protected allMarkings: TrailMarking[] = [];
  protected filteredMarkings: TrailMarking[] = [];
  protected selectedMarkings = new Set<string>();
  protected markingSearchQuery = '';
  protected markingFilterColor = '';
  protected markingFilterShape = '';
  protected uniqueColors: string[] = [];
  protected uniqueShapes: string[] = [];
  protected isLoadingMarkings = false;

  protected regionSearchQuery = '';
  protected visibleRegions: RegionOption[] = [];
  protected regionScrollIndex = 0;
  protected maxRegionScroll = 0;
  private regionsPerPage = 4;
  private filteredRegions: RegionOption[] = [];

  private selectedTrailTypes = new Set<string>();
  private selectedRegions = new Set<string>();
  private selectedCategories = new Set<string>();

  constructor(
    private fb: FormBuilder,
    private preferencesService: UserPreferencesService,
    private trailMarkingService: TrailMarkingService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.initializeForm();
    this.loadPreferences();
    this.loadAvailableOptions();
    this.loadMarkings();
  }

  initializeForm(): void {
    this.preferencesForm = this.fb.group({
      preferredTrailTypes: [[]],
      preferredDifficulty: ['Moderate'],
      maxDistance: [20],
      maxDuration: [8],
      minimumRating: [0],
      preferredRegionNames: [[]],
      preferredCategories: [[]],
      preferredMarkings: [[]],
    });
  }

  async loadPreferences(): Promise<void> {
    try {
      this.isLoading = true;
      this.currentPreferences =
        (await firstValueFrom(this.preferencesService.getUserPreferences())) ||
        {};
      this.hasPreferences = Object.keys(this.currentPreferences).length > 0;

      if (this.hasPreferences) {
        this.populateFormWithPreferences();
      }
    } catch (error) {
      console.error('Error loading preferences:', error);
      this.hasPreferences = false;
    } finally {
      this.isLoading = false;
    }
  }

  async loadAvailableOptions(): Promise<void> {
    try {
      const options = await firstValueFrom(
        this.preferencesService.getPreferenceOptions()
      );
      if (options) {
        this.availableTrailTypes = options.trailTypes || [];
        this.availableRegions = options.regions || [];
        this.filteredRegions = [...this.availableRegions];
        this.updateVisibleRegions();
      }
    } catch (error) {
      console.error('Error loading options:', error);
    }
  }

  private populateFormWithPreferences(): void {
    this.preferencesForm.patchValue(this.currentPreferences);

    if (this.currentPreferences.preferredTrailTypes) {
      this.selectedTrailTypes = new Set(
        this.currentPreferences.preferredTrailTypes
      );
    }

    if (this.currentPreferences.preferredRegionNames) {
      this.selectedRegions = new Set(
        this.currentPreferences.preferredRegionNames
      );
    }

    if (this.currentPreferences.preferredCategories) {
      this.selectedCategories = new Set(
        this.currentPreferences.preferredCategories
      );
    }

    if (this.currentPreferences.preferredMarkings) {
      this.selectedMarkings = new Set(
        this.currentPreferences.preferredMarkings.map((m) => m.symbol)
      );
    }
  }

  async loadMarkings(): Promise<void> {
    this.isLoadingMarkings = true;
    try {
      this.allMarkings = await firstValueFrom(
        this.trailMarkingService.loadAllMarkings()
      );
      this.filteredMarkings = [...this.allMarkings];

      this.uniqueColors = await firstValueFrom(
        this.trailMarkingService.getUniqueColors()
      );
      this.uniqueShapes = await firstValueFrom(
        this.trailMarkingService.getUniqueShapes()
      );
    } catch (error) {
      console.error('Error loading markings:', error);
    } finally {
      this.isLoadingMarkings = false;
    }
  }

  protected filterMarkings(): void {
    let filtered = [...this.allMarkings];

    if (this.markingSearchQuery) {
      const query = this.markingSearchQuery.toLowerCase();
      filtered = filtered.filter((m) =>
        m.displayName.toLowerCase().includes(query)
      );
    }

    if (this.markingFilterColor) {
      filtered = filtered.filter(
        (m) =>
          m.foregroundColor === this.markingFilterColor ||
          m.backgroundColor === this.markingFilterColor ||
          m.shapeColor === this.markingFilterColor
      );
    }

    if (this.markingFilterShape) {
      filtered = filtered.filter(
        (m) => m.shape.toLowerCase() === this.markingFilterShape.toLowerCase()
      );
    }

    this.filteredMarkings = filtered;
  }

  protected isMarkingSelected(marking: TrailMarking): boolean {
    return this.selectedMarkings.has(marking.symbol);
  }

  protected toggleMarking(marking: TrailMarking): void {
    if (this.selectedMarkings.has(marking.symbol)) {
      this.selectedMarkings.delete(marking.symbol);
    } else {
      this.selectedMarkings.add(marking.symbol);
    }
    this.updateMarkingsInForm();
  }

  private updateMarkingsInForm(): void {
    const selectedMarkingObjects = this.allMarkings.filter((m) =>
      this.selectedMarkings.has(m.symbol)
    );
    this.preferencesForm.patchValue({
      preferredMarkings: selectedMarkingObjects,
    });
  }

  protected getColorName(color: string): string {
    const colorMap: { [key: string]: string } = {
      '#FF0000': 'Red',
      '#0000FF': 'Blue',
      '#FFFF00': 'Yellow',
      '#008000': 'Green',
      '#FFFFFF': 'White',
      '#000000': 'Black',
      '#FFA500': 'Orange',
      '#800080': 'Purple',
      '#A52A2A': 'Brown',
    };
    return colorMap[color.toUpperCase()] || color;
  }

  // Metode pentru categorii
  protected isCategorySelected(category: string): boolean {
    return this.selectedCategories.has(category);
  }

  protected toggleCategory(category: string): void {
    if (this.selectedCategories.has(category)) {
      this.selectedCategories.delete(category);
    } else {
      this.selectedCategories.add(category);
    }
    this.preferencesForm.patchValue({
      preferredCategories: Array.from(this.selectedCategories),
    });
  }

  protected getCategoryIcon(category: string): string {
    const icons: { [key: string]: string } = {
      International: '🌍',
      National: '🏴',
      Regional: '📍',
      Local: '🏘️',
    };
    return icons[category] || '📍';
  }

  // Metodă pentru minimum rating
  protected setMinimumRating(rating: number): void {
    this.preferencesForm.patchValue({ minimumRating: rating });
  }

  protected startConfiguration(): void {
    this.isEditing = true;
  }

  protected editPreferences(): void {
    this.isEditing = true;
    this.populateFormWithPreferences();
  }

  protected cancelEdit(): void {
    this.isEditing = false;
    if (!this.hasPreferences) {
      this.router.navigate(['/dashboard']);
    }
  }

  protected async savePreferences(): Promise<void> {
    if (this.isSaving) return;

    try {
      this.isSaving = true;

      const selectedMarkingObjects = this.allMarkings.filter((m) =>
        this.selectedMarkings.has(m.symbol)
      );

      const preferences: UserPreferences = {
        ...this.preferencesForm.value,
        preferredTrailTypes: Array.from(this.selectedTrailTypes),
        preferredRegionNames: Array.from(this.selectedRegions),
        preferredCategories: Array.from(this.selectedCategories),
        preferredMarkings: selectedMarkingObjects,
      };

      await firstValueFrom(
        this.preferencesService.saveUserPreferences(preferences)
      );

      this.currentPreferences = preferences;
      this.hasPreferences = true;
      this.isEditing = false;
      this.showSuccess('Preferences saved successfully!');
    } catch (error) {
      console.error('Error saving preferences:', error);
      this.showSuccess('Error saving preferences. Please try again.');
    } finally {
      this.isSaving = false;
    }
  }

  protected showResetConfirmation(): void {
    this.showResetModal = true;
  }

  protected closeResetModal(): void {
    this.showResetModal = false;
  }

  protected async confirmReset(): Promise<void> {
    try {
      this.preferencesService.resetUserPreferences().subscribe({
        next: () => {
          this.currentPreferences = {};
          this.hasPreferences = false;
          this.showResetModal = false;
          this.selectedTrailTypes.clear();
          this.selectedRegions.clear();
          this.selectedCategories.clear();
          this.selectedMarkings.clear();
          this.initializeForm();
          this.showSuccess('Preferences reset successfully!');
        },
        error: (error: any) => {
          console.error('Error resetting preferences:', error);
          this.showSuccess('Error resetting preferences. Please try again.');
        },
      });
    } catch (error) {
      console.error('Error resetting preferences:', error);
      this.showSuccess('Error resetting preferences. Please try again.');
    }
  }

  protected isTrailTypeSelected(type: string): boolean {
    return this.selectedTrailTypes.has(type);
  }

  protected toggleTrailType(type: string): void {
    if (this.selectedTrailTypes.has(type)) {
      this.selectedTrailTypes.delete(type);
    } else {
      this.selectedTrailTypes.add(type);
    }
    this.preferencesForm.patchValue({
      preferredTrailTypes: Array.from(this.selectedTrailTypes),
    });
  }

  protected getTrailTypeIcon(type: string): string {
    const icons: { [key: string]: string } = {
      Hiking: '🥾',
      Walking: '🚶',
      Cycling: '🚴',
      'Mountain Biking': '🚵',
      Running: '🏃',
      'Horseback Riding': '🐎',
    };
    return icons[type] || '🏃';
  }

  protected isRegionSelected(regionName: string): boolean {
    return this.selectedRegions.has(regionName);
  }

  protected toggleRegion(regionName: string): void {
    if (this.selectedRegions.has(regionName)) {
      this.selectedRegions.delete(regionName);
    } else {
      this.selectedRegions.add(regionName);
    }
    this.preferencesForm.patchValue({
      preferredRegionNames: Array.from(this.selectedRegions),
    });
  }

  protected filterRegions(): void {
    const query = this.regionSearchQuery.toLowerCase();
    this.filteredRegions = this.availableRegions.filter((region) =>
      region.name.toLowerCase().includes(query)
    );
    this.regionScrollIndex = 0;
    this.updateVisibleRegions();
  }

  private updateVisibleRegions(): void {
    const start = this.regionScrollIndex;
    const end = start + this.regionsPerPage;
    this.visibleRegions = this.filteredRegions.slice(start, end);
    this.maxRegionScroll = Math.max(
      0,
      this.filteredRegions.length - this.regionsPerPage
    );
  }

  protected scrollRegions(direction: 'left' | 'right'): void {
    if (direction === 'left' && this.regionScrollIndex > 0) {
      this.regionScrollIndex--;
    } else if (
      direction === 'right' &&
      this.regionScrollIndex < this.maxRegionScroll
    ) {
      this.regionScrollIndex++;
    }
    this.updateVisibleRegions();
  }

  protected getDifficultyColor(difficulty?: string): string {
    const colors: { [key: string]: string } = {
      Easy: '#4CAF50',
      Moderate: '#FFC107',
      Difficult: '#FF9800',
      'Very Difficult': '#F44336',
      Expert: '#9C27B0',
    };
    return colors[difficulty || ''] || '#9E9E9E';
  }

  protected formatDuration(hours?: number): string {
    if (!hours) return '0h';
    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  private showSuccess(message: string): void {
    this.successMessage = message;
    this.showSuccessMessage = true;
    setTimeout(() => {
      this.showSuccessMessage = false;
    }, 3000);
  }
}
