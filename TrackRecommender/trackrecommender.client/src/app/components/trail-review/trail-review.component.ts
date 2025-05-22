import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, ValidatorFn, AbstractControl, ValidationErrors } from '@angular/forms';
import { ReviewService } from '../../services/review/review.service';

@Component({
  selector: 'app-trail-review',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './trail-review.component.html',
  styleUrl: './trail-review.component.scss'
})
export class TrailReviewComponent implements OnInit {
  @Input() trailId: number = 0;
  @Input() trailName: string = '';
  @Input() difficulty: string = '';

  @Output() reviewSubmitted = new EventEmitter<any>();
  @Output() cancelled = new EventEmitter<void>();

  reviewForm: FormGroup;
  isSubmitting = false;
  isLoading = true;
  error: string | null = null;

  constructor(
    private fb: FormBuilder,
    private reviewService: ReviewService
  ) {
    this.reviewForm = this.fb.group({
      rating: [null, [Validators.required, Validators.min(1), Validators.max(5)]],
      comment: ['', Validators.maxLength(1000)],
      hasCompleted: [false],
      completedAt: [null],
      actualDuration: [null, [Validators.min(0.1), Validators.max(48)]],
      perceivedDifficulty: ['']
    });
  }

  ngOnInit() {
    this.isLoading = true;

    this.reviewService.canReviewTrail(this.trailId).subscribe({
      next: (response) => {
        this.isLoading = false;
        if (!response.canReview) {
          this.error = 'You have already reviewed this trail. You need to delete your existing review before adding a new one.';
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.error = 'Could not verify review status. Please try again later.';
        console.error('Error checking review status:', err);
      }
    });

    const completedAtControl = this.reviewForm.get('completedAt');
    if (completedAtControl) {
      completedAtControl.setValidators([
        this.dateValidator()
      ]);
      completedAtControl.updateValueAndValidity();
    }
  }

  dateValidator(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const selectedDate = new Date(control.value);
      const today = new Date();

      if (selectedDate > today) {
        return { futureDate: true };
      }

      const minAllowedDate = new Date();
      minAllowedDate.setFullYear(today.getFullYear() - 5);

      if (selectedDate < minAllowedDate) {
        return { tooOldDate: true };
      }

      return null;
    };
  }

  onSubmit() {
    if (this.reviewForm.invalid) return;

    this.isSubmitting = true;
    this.error = null;

    this.reviewService.createReview(this.trailId, this.reviewForm.value).subscribe({
      next: (review) => {
        this.isSubmitting = false;
        this.reviewSubmitted.emit(review);
      },
      error: (err) => {
        this.isSubmitting = false;
        this.error = typeof err === 'string' ? err : 'An error occurred while saving your review. Please try again later.';
      }
    });
  }

  cancel() {
    this.cancelled.emit();
  }

  getStarArray(): number[] {
    return [1, 2, 3, 4, 5];
  }
}
