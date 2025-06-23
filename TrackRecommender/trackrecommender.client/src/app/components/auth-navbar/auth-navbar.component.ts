import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-auth-navbar',
  templateUrl: './auth-navbar.component.html',
  styleUrl: './auth-navbar.component.scss',
  standalone: true,
  imports: [CommonModule, RouterModule],
})
export class AuthNavbarComponent {}
