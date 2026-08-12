import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { CommerceApiClient } from './commerce-api.client';
import { IdentityApiClient } from './identity-api.client';

describe('CommerceApiClient', () => {
  let client: CommerceApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf: vi.fn(() => of(undefined)) } },
      ],
    });
    client = TestBed.inject(CommerceApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('sends a demo checkout without card data', async () => {
    const promise = firstValueFrom(client.demoCheckout('course-1', 'success'));
    const request = http.expectOne('commerce/demo-checkout');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ courseId: 'course-1', outcome: 'success' });
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    expect(request.request.body).toEqual({ courseId: 'course-1', outcome: 'success' });
    request.flush({
      data: {
        orderId: 'order-1',
        paymentId: 'payment-1',
        courseId: 'course-1',
        enrollmentId: 'enrollment-1',
        orderStatus: 'Completed',
        paymentStatus: 'Succeeded',
        amountCredits: 100,
        currency: 'DEMO',
      },
    });
    await expect(promise).resolves.toMatchObject({ paymentStatus: 'Succeeded', currency: 'DEMO' });
  });

  it('activates and cancels the local demo subscription without billing data', async () => {
    const activatePromise = firstValueFrom(client.activateDemoSubscription());
    const activateRequest = http.expectOne('subscriptions');
    expect(activateRequest.request.method).toBe('POST');
    expect(activateRequest.request.body).toEqual({});
    expect(activateRequest.request.headers.get('Idempotency-Key')).toBeTruthy();
    activateRequest.flush({ data: subscription('Active') });
    const activated = await activatePromise;

    const cancelPromise = firstValueFrom(client.cancelDemoSubscription(activated.id));
    const cancelRequest = http.expectOne('subscriptions/subscription-1/cancel');
    expect(cancelRequest.request.method).toBe('POST');
    expect(cancelRequest.request.body).toEqual({});
    cancelRequest.flush({ data: subscription('Cancelled') });

    await expect(cancelPromise).resolves.toMatchObject({ status: 'Cancelled' });
  });
});

const subscription = (status: 'Active' | 'Cancelled') => ({
  id: 'subscription-1',
  planCode: 'portfolio-demo',
  status,
  activatedAt: '2026-08-12T00:00:00Z',
  updatedAt: '2026-08-12T00:00:00Z',
  cancelledAt: status === 'Cancelled' ? '2026-08-12T00:00:00Z' : null,
});
