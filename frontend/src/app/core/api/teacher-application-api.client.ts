import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, type Observable, switchMap } from 'rxjs';
import type { ApiEnvelope } from './api-envelope';
import { authenticatedMutationContext, authenticatedReadContext } from './phase6-api.helpers';
import type { TeacherApplication, TeacherApplicationRequest } from './phase6-api.types';
import { IdentityApiClient } from './identity-api.client';

@Injectable({ providedIn: 'root' })
export class TeacherApplicationApiClient {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityApiClient);

  getCurrent(): Observable<TeacherApplication> {
    return this.http
      .get<ApiEnvelope<TeacherApplication>>('me/teacher-application', {
        context: authenticatedReadContext(),
      })
      .pipe(map((response) => response.data));
  }

  submit(request: TeacherApplicationRequest): Observable<TeacherApplication> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.post<ApiEnvelope<TeacherApplication>>('me/teacher-application', request, {
          context: authenticatedMutationContext(),
        }),
      ),
      map((response) => response.data),
    );
  }

  withdraw(): Observable<TeacherApplication> {
    return this.identity.bootstrapCsrf().pipe(
      switchMap(() =>
        this.http.delete<ApiEnvelope<TeacherApplication>>('me/teacher-application', {
          context: authenticatedMutationContext(),
        }),
      ),
      map((response) => response.data),
    );
  }
}
