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
import { trigger, transition, style, animate } from '@angular/animations';
import {
  RegionOption,
  UserPreferences,
} from '../../models/user-preferences.model';
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
  protected availableRegions: RegionOption[] = [];

  protected regionSearchQuery = '';
  protected visibleRegions: RegionOption[] = [];
  protected regionScrollIndex = 0;
  protected maxRegionScroll = 0;
  private regionsPerPage = 4;
  private filteredRegions: RegionOption[] = [];

  private selectedTrailTypes = new Set<string>();
  private selectedRegions = new Set<string>();

  constructor(
    private fb: FormBuilder,
    private preferencesService: UserPreferencesService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.initializeForm();
    this.loadPreferences();
    this.loadAvailableOptions();
  }

  initializeForm(): void {
    this.preferencesForm = this.fb.group({
      preferredTrailTypes: [[]],
      preferredDifficulty: ['Moderate'],
      maxDistance: [20],
      maxDuration: [8],
      minimumRating: [0],
      preferredRegionNames: [[]],
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

      const preferences: UserPreferences = {
        ...this.preferencesForm.value,
        preferredTrailTypes: Array.from(this.selectedTrailTypes),
        preferredRegionNames: Array.from(this.selectedRegions),
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
