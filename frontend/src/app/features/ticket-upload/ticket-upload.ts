import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { TicketService } from '../../core/services/ticket.service';
import { AuthService } from '../../core/services/auth.service';
import { CustomerClassification } from '../../core/models/ticket.models';

interface SelectedFilePreview {
  file: File;
  name: string;
  sizeFormatted: string;
  previewUrl: string | null;
}

@Component({
  selector: 'app-ticket-upload',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './ticket-upload.html',
  styleUrls: ['./ticket-upload.scss']
})
export class TicketUploadComponent {
  private ticketService = inject(TicketService);
  private authService = inject(AuthService);
  private router = inject(Router);

  public ticketNumber = signal<string>('');
  public customerName = signal<string>('');
  public classification = signal<CustomerClassification>('eLite');
  public selectedFiles = signal<SelectedFilePreview[]>([]);

  public isUploading = signal<boolean>(false);
  public uploadProgressStep = signal<string>('');
  public errorMessage = signal<string | null>(null);

  public onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) {
      this.addFiles(Array.from(input.files));
    }
  }

  public onFileDrop(event: DragEvent): void {
    event.preventDefault();
    if (event.dataTransfer?.files) {
      this.addFiles(Array.from(event.dataTransfer.files));
    }
  }

  public onDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  private addFiles(files: File[]): void {
    this.errorMessage.set(null);
    const newFiles: SelectedFilePreview[] = [];

    for (const f of files) {
      // Validate extensions: png, jpg, jpeg, pdf
      const ext = f.name.toLowerCase();
      if (!ext.endsWith('.png') && !ext.endsWith('.jpg') && !ext.endsWith('.jpeg') && !ext.endsWith('.pdf')) {
        this.errorMessage.set(`File '${f.name}' is not supported. Please upload PNG, JPG, or PDF files.`);
        continue;
      }

      // Max 15MB
      if (f.size > 15 * 1024 * 1024) {
        this.errorMessage.set(`File '${f.name}' exceeds the 15 MB enterprise limit.`);
        continue;
      }

      const isImage = f.type.startsWith('image/');
      const previewUrl = isImage ? URL.createObjectURL(f) : null;

      newFiles.push({
        file: f,
        name: f.name,
        sizeFormatted: this.formatBytes(f.size),
        previewUrl
      });
    }

    this.selectedFiles.update(current => [...current, ...newFiles]);
  }

  public removeFile(index: number): void {
    this.selectedFiles.update(files => {
      const copy = [...files];
      if (copy[index]?.previewUrl) {
        URL.revokeObjectURL(copy[index].previewUrl!);
      }
      copy.splice(index, 1);
      return copy;
    });
  }

  public submitTicket(): void {
    if (!this.ticketNumber().trim() || !this.customerName().trim()) {
      this.errorMessage.set('Ticket Number and Customer Name are required.');
      return;
    }

    const files = this.selectedFiles();
    if (files.length === 0) {
      this.errorMessage.set('At least one screenshot or PDF attachment is required for verification.');
      return;
    }

    this.isUploading.set(true);
    this.errorMessage.set(null);
    this.uploadProgressStep.set('Transferring attachment evidence to secure quarantine storage...');

    const rawFiles = files.map(f => f.file);
    const userId = this.authService.currentUserId();

    this.ticketService.uploadTicket(
      this.ticketNumber().trim(),
      this.customerName().trim(),
      this.classification(),
      userId,
      rawFiles
    ).subscribe({
      next: (ticket) => {
        this.uploadProgressStep.set('Executing Antivirus scanning & OCR parameter extraction...');
        setTimeout(() => {
          this.isUploading.set(false);
          // Navigate directly to dual-pane review screen
          this.router.navigate(['/review', ticket.id]);
        }, 1000);
      },
      error: (err) => {
        this.isUploading.set(false);
        this.errorMessage.set(err.error?.message || 'Failed to ingest customer ticket.');
      }
    });
  }

  private formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
  }
}
