import { Routes } from '@angular/router';
import { DualPaneReviewComponent } from './features/ticket-wizard/dual-pane-review/dual-pane-review';
import { PmQueueComponent } from './features/pm-queue/pm-queue';
import { PreCheckConsoleComponent } from './features/precheck-console/precheck-console';

export const routes: Routes = [
  { path: 'review/:ticketId', component: DualPaneReviewComponent },
  { path: 'console/:ticketId', component: PreCheckConsoleComponent },
  { path: 'pm-queue', component: PmQueueComponent },
  { path: '', redirectTo: 'pm-queue', pathMatch: 'full' }
];
