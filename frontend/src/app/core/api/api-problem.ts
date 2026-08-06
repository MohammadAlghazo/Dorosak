import type { HttpErrorResponse } from '@angular/common/http';

export type ValidationErrors = Readonly<Record<string, readonly string[]>>;

export class ApiProblem extends Error {
  constructor(
    readonly status: number,
    readonly code: string,
    readonly traceId: string | null,
    readonly correlationId: string | null,
    readonly validationErrors: ValidationErrors,
    message: string,
  ) {
    super(message);
    this.name = 'ApiProblem';
  }
}

export const normalizeApiProblem = (response: HttpErrorResponse): ApiProblem => {
  const body = isRecord(response.error) ? response.error : {};
  return new ApiProblem(
    response.status,
    stringValue(body['code']) ?? `HTTP.${String(response.status || 0)}`,
    stringValue(body['traceId']),
    stringValue(body['correlationId']),
    validationErrors(body['errors']),
    stringValue(body['detail']) ??
      stringValue(body['title']) ??
      'The request could not be completed.',
  );
};

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value);

const stringValue = (value: unknown): string | null => (typeof value === 'string' ? value : null);

const validationErrors = (value: unknown): ValidationErrors => {
  if (!isRecord(value)) return {};
  return Object.fromEntries(
    Object.entries(value)
      .filter(
        (entry): entry is [string, string[]] =>
          Array.isArray(entry[1]) && entry[1].every((message) => typeof message === 'string'),
      )
      .map(([field, messages]) => [field, messages]),
  );
};
