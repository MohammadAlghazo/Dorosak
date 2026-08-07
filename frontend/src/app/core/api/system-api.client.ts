import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable } from 'rxjs';
import { API_REQUEST, PUBLIC_API_REQUEST } from './api-context';

interface SystemStatusDto {
  service: string;
  version: string;
  utcTime: string;
}

interface ApiEnvelope<T> {
  data: T;
}

export interface SystemStatus {
  service: string;
  available: boolean;
}

@Injectable({ providedIn: 'root' })
export class SystemApiClient {
  private readonly http = inject(HttpClient);

  getStatus(): Observable<SystemStatus> {
    return this.http
      .get<ApiEnvelope<SystemStatusDto>>('system/status', {
        context: new HttpContext().set(API_REQUEST, true).set(PUBLIC_API_REQUEST, true),
      })
      .pipe(
        map((response) => ({
          service: response.data.service,
          available: Boolean(response.data.utcTime),
        })),
      );
  }
}
