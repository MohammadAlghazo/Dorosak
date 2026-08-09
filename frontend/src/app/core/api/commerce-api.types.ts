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
