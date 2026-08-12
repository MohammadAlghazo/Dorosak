export interface AdminAnalyticsOverview {
  generatedAt: string;
  totalUsers: number;
  activeUsers: number;
  totalCourses: number;
  publishedCourses: number;
  totalEnrollments: number;
  completedEnrollments: number;
  completedDemoOrders: number;
  activeDemoSubscriptions: number;
  issuedCertificates: number;
  activeCertificates: number;
  pendingPublicationReviews: number;
  openModerationCases: number;
  pendingOutboxMessages: number;
  retryingOutboxMessages: number;
}
