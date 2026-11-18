import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class LoginComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  public username = signal<string>('admin');
  public password = signal<string>('admin123');
  public showPassword = signal<boolean>(false);
  public isLoading = signal<boolean>(false);
  public errorMessage = signal<string | null>(null);

  public togglePasswordVisibility(): void {
    this.showPassword.update(v => !v);
  }

  public fillAdminCredentials(): void {
    this.username.set('admin');
    this.password.set('admin123');
    this.errorMessage.set(null);
  }

  public onLogin(event: Event): void {
    event.preventDefault();
    this.errorMessage.set(null);

    const user = this.username().trim();
    const pass = this.password().trim();

    if (!user || !pass) {
      this.errorMessage.set('Please provide both username and password.');
      return;
    }

    this.isLoading.set(true);

    this.authService.login(user, pass).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        if (res.success) {
          this.router.navigate(['/dashboard']);
        } else {
          this.errorMessage.set(res.message || 'Authentication failed. Please verify credentials.');
        }
      },
      error: () => {
        this.isLoading.set(false);
        this.errorMessage.set('Connection error occurred while attempting authentication.');
      }
    });
  }
}
