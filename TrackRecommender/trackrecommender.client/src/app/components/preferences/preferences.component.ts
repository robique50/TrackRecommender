import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { MainNavbarComponent } from '../main-navbar/main-navbar.component';
import { UserPreferencesService } from '../../services/user-preferences/user-preferences.service';
import {
  DIFFICULTY_COLORS,
  PreferenceOptions,
  TAG_CATEGORIES,
  TagGroup,
  UserPreferences,
} from '../../models/user-preferences.model';

@Component({
  selector: 'app-preferences',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MainNavbarComponent],
  templateUrl: './preferences.component.html',
  styleUrl: './preferences.component.scss',
})
export class PreferencesComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  protected isLoading = true;
  protected isSaving = false;
  protected error: string | null = null;
  protected successMessage: string | null = null;

  protected preferencesForm!: FormGroup;
  protected options: PreferenceOptions = {
    trailTypes: [],
    difficulties: [],
    categories: [],
    availableTags: [],
    regions: [],
    minDistance: 0,
    maxDistance: 100,
    minDuration: 0,
    maxDuration: 24,
  };

  protected groupedTags: TagGroup[] = [];
  private selectedTags: Set<string> = new Set();
  private selectedRegions: Set<string> = new Set();
  private selectedTrailTypes: Set<string> = new Set();
  private selectedCategories: Set<string> = new Set();

  constructor(
    private fb: FormBuilder,
    private preferencesService: UserPreferencesService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.initializeForm();
    this.loadPreferenceOptions();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.preferencesForm = this.fb.group({
      preferredTrailTypes: [[]],
      preferredDifficulty: ['Moderate'],
      preferredTags: [[]],
      maxDistance: [20],
      maxDuration: [8],
      preferredCategories: [[]],
      minimumRating: [0],
      preferredRegionNames: [[]],
    });
  }

  private loadPreferenceOptions(): void {
    this.isLoading = true;
    this.error = null;

    this.preferencesService
      .getPreferenceOptions()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (options) => {
          this.options = options;
          this.groupTags();
          this.loadUserPreferences();
        },
        error: (error) => {
          this.error = 'Failed to load preference options. Please try again.';
          this.isLoading = false;
          console.error('Error loading options:', error);
        },
      });
  }

  private loadUserPreferences(): void {
    this.preferencesService
      .getUserPreferences()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (preferences) => {
          this.populateForm(preferences);
          this.isLoading = false;
        },
        error: (error) => {
          if (error.status !== 404) {
            this.error = 'Failed to load your preferences. Using defaults.';
          }
          this.isLoading = false;
        },
      });
  }

  private populateForm(preferences: UserPreferences): void {
    this.preferencesForm.patchValue({
      preferredDifficulty: preferences.preferredDifficulty || 'Moderate',
      maxDistance: preferences.maxDistance || 20,
      maxDuration: preferences.maxDuration || 8,
      minimumRating: preferences.minimumRating || 0,
    });

    if (preferences.preferredTrailTypes) {
      this.selectedTrailTypes = new Set(preferences.preferredTrailTypes);
    }
    if (preferences.preferredCategories) {
      this.selectedCategories = new Set(preferences.preferredCategories);
    }
    if (preferences.preferredTags) {
      this.selectedTags = new Set(preferences.preferredTags);
    }
    if (preferences.preferredRegionNames) {
      this.selectedRegions = new Set(preferences.preferredRegionNames);
    }
  }

  private groupTags(): void {
    const groups: { [key: string]: string[] } = {};

    this.options.availableTags.forEach((tag) => {
      if (tag.includes('=')) {
        const [key] = tag.split('=', 2);
        const category = TAG_CATEGORIES[key] || 'Other';

        if (!groups[category]) {
          groups[category] = [];
        }
        groups[category].push(tag);
      }
    });

    this.groupedTags = Object.entries(groups).map(([category, tags]) => ({
      category,
      tags: tags.sort(),
    }));
  }

  protected onTrailTypeChange(event: Event, type: string): void {
    const checkbox = event.target as HTMLInputElement;

    if (checkbox.checked) {
      this.selectedTrailTypes.add(type);
    } else {
      this.selectedTrailTypes.delete(type);
    }

    this.preferencesForm.patchValue({
      preferredTrailTypes: Array.from(this.selectedTrailTypes),
    });
  }

  protected onCategoryChange(event: Event, category: string): void {
    const checkbox = event.target as HTMLInputElement;

    if (checkbox.checked) {
      this.selectedCategories.add(category);
    } else {
      this.selectedCategories.delete(category);
    }

    this.preferencesForm.patchValue({
      preferredCategories: Array.from(this.selectedCategories),
    });
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

  protected toggleTag(tag: string): void {
    if (this.selectedTags.has(tag)) {
      this.selectedTags.delete(tag);
    } else {
      this.selectedTags.add(tag);
    }

    this.preferencesForm.patchValue({
      preferredTags: Array.from(this.selectedTags),
    });
  }

  protected isRegionSelected(regionName: string): boolean {
    return this.selectedRegions.has(regionName);
  }

  protected isTagSelected(tag: string): boolean {
    return this.selectedTags.has(tag);
  }

  protected getDifficultyColor(difficulty: string): string {
    return DIFFICULTY_COLORS[difficulty] || '#9E9E9E';
  }

  protected formatDuration(hours: number): string {
    if (!hours) return '0h';

    const h = Math.floor(hours);
    const m = Math.round((hours - h) * 60);

    if (h === 0) return `${m}m`;
    if (m === 0) return `${h}h`;
    return `${h}h ${m}m`;
  }

  protected formatTag(tag: string): string {
    if (!tag.includes('=')) return tag;

    const [, value] = tag.split('=', 2);
    return value
      .replace(/_/g, ' ')
      .split(' ')
      .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }

  protected setMinimumRating(rating: number): void {
    this.preferencesForm.patchValue({ minimumRating: rating });
  }

  protected resetForm(): void {
    this.preferencesForm.reset({
      preferredDifficulty: 'Moderate',
      maxDistance: 20,
      maxDuration: 8,
      minimumRating: 0,
    });

    this.selectedTrailTypes.clear();
    this.selectedCategories.clear();
    this.selectedTags.clear();
    this.selectedRegions.clear();

    this.preferencesForm.patchValue({
      preferredTrailTypes: [],
      preferredCategories: [],
      preferredTags: [],
      preferredRegionNames: [],
    });

    this.successMessage = 'Preferences reset to defaults';
    setTimeout(() => (this.successMessage = null), 3000);
  }

  protected onSubmit(): void {
    if (this.preferencesForm.invalid || this.isSaving) return;

    this.isSaving = true;
    this.error = null;
    this.successMessage = null;

    const preferences: UserPreferences = {
      ...this.preferencesForm.value,
      preferredTrailTypes: Array.from(this.selectedTrailTypes),
      preferredCategories: Array.from(this.selectedCategories),
      preferredTags: Array.from(this.selectedTags),
      preferredRegionNames: Array.from(this.selectedRegions),
    };

    this.preferencesService
      .saveUserPreferences(preferences)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.isSaving = false;
          this.successMessage =
            'Your preferences have been saved successfully!';
          setTimeout(() => {
            this.successMessage = null;
            this.router.navigate(['/dashboard']);
          }, 2000);
        },
        error: (error) => {
          this.isSaving = false;
          this.error = 'Failed to save preferences. Please try again.';
          console.error('Error saving preferences:', error);
        },
      });
  }
}
