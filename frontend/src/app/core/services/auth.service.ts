import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, of, map } from 'rxjs';

export interface ApplicationUserDto {
  id: string;
  username: string;
  fullName: string;
  role: string;
  email: string;
}

export interface LoginResponse {
  success: boolean;
  message: string;
  token?: string;
  user?: ApplicationUserDto;
}

const DEFAULT_ADMIN: ApplicationUserDto = {
  id: '00000000-0000-0000-0000-000000000001',
  username: 'admin',
  fullName: 'System Administrator',
  role: 'Admin',
  email: 'admin@wercsgcs.internal'
};

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  private apiBaseUrl = '/api';

  public users = signal<ApplicationUserDto[]>([DEFAULT_ADMIN]);
  public currentUser = signal<ApplicationUserDto>(this.getStoredUser() ?? DEFAULT_ADMIN);
  public isAuthenticated = signal<boolean>(this.checkInitialAuth());

  public currentUserId = computed(() => this.currentUser().id);
  public currentUserName = computed(() => this.currentUser().fullName);
  public currentUserRole = computed(() => this.currentUser().role);

  constructor() {
    this.refreshUsers();
  }

  private checkInitialAuth(): boolean {
    const token = localStorage.getItem('gcs_auth_token');
    return !!token;
  }

  private getStoredUser(): ApplicationUserDto | null {
    const raw = localStorage.getItem('gcs_auth_user');
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }

  public refreshUsers(): void {
    this.loadUsers().subscribe();
  }

  public loadUsers(): Observable<ApplicationUserDto[]> {
    return this.http.get<ApplicationUserDto[]>(`${this.apiBaseUrl}/users`).pipe(
      tap((loadedUsers) => {
        if (loadedUsers && loadedUsers.length > 0) {
          this.users.set(loadedUsers);
          const current = this.currentUser();
          const match = loadedUsers.find(u => u.id === current.id || u.username === current.username);
          if (match) {
            this.setCurrentUser(match);
          }
        }
      }),
      catchError((err) => {
        console.warn('Could not load users from /api/users, fallback active:', err);
        return of([DEFAULT_ADMIN]);
      })
    );
  }

  public login(username: string, password: string): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.apiBaseUrl}/auth/login`, { username, password }).pipe(
      tap((res) => {
        if (res.success && res.user && res.token) {
          this.currentUser.set(res.user);
          this.isAuthenticated.set(true);
          localStorage.setItem('gcs_auth_token', res.token);
          localStorage.setItem('gcs_auth_user', JSON.stringify(res.user));
          localStorage.setItem('gcs_selected_user_id', res.user.id);
        }
      }),
      catchError((err) => {
        // Fallback for standalone offline dev: accept admin/admin123
        if (username.toLowerCase() === 'admin' && password === 'admin123') {
          this.currentUser.set(DEFAULT_ADMIN);
          this.isAuthenticated.set(true);
          localStorage.setItem('gcs_auth_token', 'offline-admin-session-token');
          localStorage.setItem('gcs_auth_user', JSON.stringify(DEFAULT_ADMIN));
          localStorage.setItem('gcs_selected_user_id', DEFAULT_ADMIN.id);
          return of({
            success: true,
            message: 'Authenticated in offline developer mode.',
            token: 'offline-admin-session-token',
            user: DEFAULT_ADMIN
          });
        }
        return of({
          success: false,
          message: err.error?.message || 'Invalid username or password.'
        });
      })
    );
  }

  public logout(): void {
    this.http.post(`${this.apiBaseUrl}/auth/logout`, {}).subscribe({
      next: () => {},
      error: () => {}
    });

    localStorage.removeItem('gcs_auth_token');
    localStorage.removeItem('gcs_auth_user');
    localStorage.removeItem('gcs_selected_user_id');
    this.isAuthenticated.set(false);
    this.currentUser.set(DEFAULT_ADMIN);
    this.router.navigate(['/login']);
  }

  public setCurrentUser(user: ApplicationUserDto): void {
    this.currentUser.set(user);
    localStorage.setItem('gcs_selected_user_id', user.id);
    localStorage.setItem('gcs_auth_user', JSON.stringify(user));
  }
}
