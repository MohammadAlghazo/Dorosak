import { HttpErrorResponse } from '@angular/common/http';
import { ApiProblem, normalizeApiProblem } from './api-problem';

describe('normalizeApiProblem', () => {
  it('maps the stable RFC 9457 contract', () => {
    const problem = normalizeApiProblem(
      new HttpErrorResponse({
        status: 422,
        error: {
          code: 'PROFILE.INVALID',
          detail: 'The profile is invalid.',
          traceId: 'trace-1',
          correlationId: 'correlation-1',
          errors: { displayName: ['Display name is required.'] },
        },
      }),
    );

    expect(problem).toBeInstanceOf(ApiProblem);
    expect(problem.code).toBe('PROFILE.INVALID');
    expect(problem.validationErrors['displayName']).toEqual(['Display name is required.']);
    expect(problem.traceId).toBe('trace-1');
  });

  it('falls back to the HTTP status without exposing arbitrary response values', () => {
    const problem = normalizeApiProblem(
      new HttpErrorResponse({ status: 503, error: 'upstream detail' }),
    );

    expect(problem.code).toBe('HTTP.503');
    expect(problem.message).toBe('The request could not be completed.');
    expect(problem.validationErrors).toEqual({});
  });
});
