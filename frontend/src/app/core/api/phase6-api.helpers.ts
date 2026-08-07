import { HttpContext, type HttpResponse } from '@angular/common/http';
import type { ApiEnvelope } from './api-envelope';
import { API_REQUEST, DEADLINE_MS, RETRY_IDEMPOTENT_GET } from './api-context';
import type { VersionedResult } from './phase6-api.types';

export const authenticatedReadContext = (): HttpContext =>
  new HttpContext().set(API_REQUEST, true).set(DEADLINE_MS, 15_000);

export const authenticatedMutationContext = (): HttpContext =>
  authenticatedReadContext().set(RETRY_IDEMPOTENT_GET, false);

export const unwrapVersioned = <T extends { draftVersion: number }>(
  response: HttpResponse<ApiEnvelope<T>>,
): VersionedResult<T> => {
  if (response.body === null) throw new Error('The API returned an empty success response.');
  return {
    value: response.body.data,
    etag: response.headers.get('ETag') ?? formatDraftEtag(response.body.data.draftVersion),
  };
};

export const formatDraftEtag = (version: number): string => `"v${String(version)}"`;
