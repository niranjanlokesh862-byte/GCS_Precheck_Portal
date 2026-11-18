import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, catchError, of } from 'rxjs';

export interface ApplicationUserDto {
  id: string;
  username: string;
  fullName: string;
  role: string;
  email: string;
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
  private apiBaseUrl = '/api';

  public users = signal<ApplicationUserDto[]>([DEFAULT_ADMIN]);
  public currentUser = signal<ApplicationUserDto>(DEFAULT_ADMIN);

  public currentUserId = computed(() => this.currentUser().id);
  public currentUserName = computed(() => this.currentUser().fullName);
  public currentUserRole = computed(() => this.currentUser().role);

  constructor() {
    this.refreshUsers();
  }

  public refreshUsers(): void {
    this.loadUsers().subscribe();
  }

  public loadUsers(): Observable<ApplicationUserDto[]> {
    return this.http.get<ApplicationUserDto[]>(`${this.apiBaseUrl}/users`).pipe(
      tap((loadedUsers) => {
        if (loadedUsers && loadedUsers.length > 0) {
          this.users.set(loadedUsers);
          const savedId = localStorage.getItem('gcs_selected_user_id');
          const found = loadedUsers.find(u => u.id === savedId) ?? loadedUsers[0];
          this.setCurrentUser(found);
        }
      }),
      catchError((err) => {
        console.warn('Could not load users from /api/users, using default session user:', err);
        return of([DEFAULT_ADMIN]);
      })
    );
  }

  public setCurrentUser(user: ApplicationUserDto): void {
    this.currentUser.set(user);
    localStorage.setItem('gcs_selected_user_id', user.id);
  }
}
