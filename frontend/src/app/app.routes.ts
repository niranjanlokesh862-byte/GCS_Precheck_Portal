import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login';
import { DashboardComponent } from './features/dashboard/dashboard';
import { TicketUploadComponent } from './features/ticket-upload/ticket-upload';
import { TicketHistoryComponent } from './features/ticket-history/ticket-history';
import { DualPaneReviewComponent } from './features/ticket-wizard/dual-pane-review/dual-pane-review';
import { PmQueueComponent } from './features/pm-queue/pm-queue';
import { PreCheckConsoleComponent } from './features/precheck-console/precheck-console';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'upload', component: TicketUploadComponent },
  { path: 'history', component: TicketHistoryComponent },
  { path: 'review/:ticketId', component: DualPaneReviewComponent },
  { path: 'console/:ticketId', component: PreCheckConsoleComponent },
  { path: 'pm-queue', component: PmQueueComponent },
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: '**', redirectTo: 'dashboard' }
];
