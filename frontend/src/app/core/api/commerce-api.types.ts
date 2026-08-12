export interface DemoCheckout {
  orderId: string;
  paymentId: string;
  courseId: string;
  enrollmentId: string | null;
  orderStatus: 'Completed' | 'Failed';
  paymentStatus: 'Succeeded' | 'Failed';
  amountCredits: number;
  currency: 'DEMO';
}

export interface DemoSubscription {
  id: string;
  planCode: 'portfolio-demo';
  status: 'Active' | 'Cancelled';
  activatedAt: string;
  updatedAt: string;
  cancelledAt: string | null;
}

export interface DemoSubscriptionState {
  subscription: DemoSubscription | null;
}
