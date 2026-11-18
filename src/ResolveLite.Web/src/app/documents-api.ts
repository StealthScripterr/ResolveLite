import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';

export type KnowledgeCategory = 'Refunds' | 'AccountAccess' | 'Delivery' | 'Subscriptions';
export type DocumentStatus = 'Pending' | 'Processing' | 'Ready' | 'Failed' | 'Superseded';

/** Mirrors DocumentDto in ResolveLite.Api. */
export interface KnowledgeDocument {
  id: string;
  fileName: string;
  category: KnowledgeCategory;
  status: DocumentStatus;
  version: number;
  uploadedAt: string;
  failureReason?: string | null;
}

/** Thin, typed wrapper around the /api/documents endpoints. */
@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);

  list(category?: KnowledgeCategory) {
    const params = category ? new HttpParams().set('category', category) : undefined;
    return this.http.get<KnowledgeDocument[]>('/api/documents', { params });
  }

  upload(file: File, category: KnowledgeCategory) {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('category', category);
    // No Content-Type header: the browser sets multipart/form-data with the boundary.
    return this.http.post<KnowledgeDocument>('/api/documents', form);
  }
}
