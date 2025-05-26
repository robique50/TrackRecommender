import { Routes } from '@angular/router';
import { MapComponent } from '../components/map/map.component';
import { RegisterComponent } from '../components/register/register.component';
import { LoginComponent } from '../components/login/login.component';
import { DashboardComponent } from '../components/dashboard/dashboard.component';
import { AuthGuard } from '../guards/auth.guard';
import { ProfileComponent } from '../components/profile/profile.component';
import { MyReviewsComponent } from '../components/my-reviews/my-reviews.component';
import { TrailsComponent } from '../components/trails/trails.component';

export const routes: Routes = [
  { path: '', redirectTo: 'register', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  {
    path: 'dashboard',
    component: DashboardComponent,
    canActivate: [AuthGuard],
  },
  { path: 'profile', component: ProfileComponent, canActivate: [AuthGuard] },
  {
    path: 'my-reviews',
    component: MyReviewsComponent,
    canActivate: [AuthGuard],
  },
  { path: 'map', component: MapComponent, canActivate: [AuthGuard] },
  { path: 'trails', component: TrailsComponent, canActivate: [AuthGuard] },
];
