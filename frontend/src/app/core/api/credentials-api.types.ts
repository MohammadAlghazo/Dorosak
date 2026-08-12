export interface Certificate {
  id: string;
  learnerName: string;
  courseTitle: string;
  locale: 'ar' | 'en';
  completedAt: string;
  issuedAt: string;
  verificationCode: string;
  status: 'Active' | 'Revoked';
  revokedAt: string | null;
}

export type PublicCertificate = Omit<Certificate, 'id'>;
