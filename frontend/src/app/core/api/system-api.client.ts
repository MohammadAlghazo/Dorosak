import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable } from 'rxjs';
import { API_REQUEST } from './api-context';

interface SystemStatusDto {
  service: string;
  environment: string;
  utcTime: string;
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
      .get<SystemStatusDto>('system/status', {
        context: new HttpContext().set(API_REQUEST, true),
      })
      .pipe(map((status) => ({ service: status.service, available: Boolean(status.utcTime) })));
  }
}
