import { Component, Input } from '@angular/core';
import { ShapeType, TrailMarking } from '../../models/trail-marking.model';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-marking-display',
  imports: [CommonModule],
  templateUrl: './marking-display.component.html',
  styleUrl: './marking-display.component.scss',
})
export class MarkingDisplayComponent {
  @Input() marking!: TrailMarking;
  protected ShapeType = ShapeType;
}
