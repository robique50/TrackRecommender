import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from './services/auth/auth.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: true,
  imports: [RouterOutlet],
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'trackrecommender.client';
  
  constructor(private authService: AuthService) {
  }
}
