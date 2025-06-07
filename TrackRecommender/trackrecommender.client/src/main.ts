import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './app/interceptors/auth.interceptor';
import { routes } from './app/routes/app.routes';
import { TokenStorageService } from './app/services/token-storage/token-storage.service';
import { provideAnimations } from '@angular/platform-browser/animations';

bootstrapApplication(AppComponent, {
  providers: [
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes),
    TokenStorageService,
    provideAnimations(),
  ],
}).catch((err) => console.error('Error bootstrapping app:', err));
