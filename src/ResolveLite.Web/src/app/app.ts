import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';

import { DocumentsApi, KnowledgeCategory, KnowledgeDocument } from './documents-api';
import { Home } from '../components/home/home';

export interface AppInfo {
  name: string;
  version: string;
  environment: string;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, DatePipe],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly http = inject(HttpClient);
  private readonly api = inject(DocumentsApi);

  protected readonly info = toSignal(this.http.get<AppInfo>('/api/info'));

  protected readonly categories: KnowledgeCategory[] = ['Refunds', 'AccountAccess', 'Delivery', 'Subscriptions'];
  protected readonly category = signal<KnowledgeCategory>('Refunds');
  protected readonly file = signal<File | null>(null);
  protected readonly uploading = signal(false);
  protected readonly refreshing = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly documents = signal<KnowledgeDocument[]>([]);
  /** Newest first. */
  protected readonly sortedDocuments = computed(() =>
    [...this.documents()].sort((a, b) => b.uploadedAt.localeCompare(a.uploadedAt)),
  );

  constructor() {
    this.refresh();
  }

  protected onCategoryChange(event: Event): void {
    this.category.set((event.target as HTMLSelectElement).value as KnowledgeCategory);
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
    this.error.set(null);
  }

  protected upload(fileInput: HTMLInputElement): void {
    const file = this.file();
    if (!file) {
      return;
    }

    this.uploading.set(true);
    this.error.set(null);

    this.api.upload(file, this.category()).subscribe({
      next: () => {
        this.uploading.set(false);
        this.file.set(null);
        fileInput.value = '';
        this.refresh();
      },
      error: (err: HttpErrorResponse) => {
        this.uploading.set(false);
        this.error.set(this.describe(err));
      },
    });
  }

  protected refresh(): void {
    if (this.refreshing()) {
      return;
    }

    this.refreshing.set(true);
    const startedAt = Date.now();

    this.api.list().subscribe({
      next: (docs) => this.documents.set(docs),
      error: () => this.finishRefresh(startedAt),
      complete: () => this.finishRefresh(startedAt),
    });
  }

  private finishRefresh(startedAt: number): void {
    const remaining = Math.max(0, 1000 - (Date.now() - startedAt));
    setTimeout(() => this.refreshing.set(false), remaining);
  }

  /** Turns a ProblemDetails / ValidationProblem response into one readable sentence. */
  private describe(err: HttpErrorResponse): string {
    const errors = err.error?.errors as Record<string, string[]> | undefined;
    if (errors) {
      return Object.values(errors).flat().join(' ');
    }
    return err.error?.title ?? `Upload failed (HTTP ${err.status}).`;
  }
}
