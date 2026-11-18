import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService, ApplicationUserDto } from './core/services/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrls: ['./app.scss']
})
export class AppComponent {
  title = 'GCS Pre-Check Automation Portal';
  public authService = inject(AuthService);

  onUserChange(userId: string): void {
    const selected = this.authService.users().find(u => u.id === userId);
    if (selected) {
      this.authService.setCurrentUser(selected);
    }
  }
}
