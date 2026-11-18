import { Component } from '@angular/core';
import {inject} from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { toSignal } from '@angular/core/rxjs-interop';
import { AppInfo } from '../../app/app';
@Component({
  selector: 'app-home',
  imports: [],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  private readonly http = inject(HttpClient);
  protected readonly info = toSignal(this.http.get<AppInfo>('/api/info'));
}
