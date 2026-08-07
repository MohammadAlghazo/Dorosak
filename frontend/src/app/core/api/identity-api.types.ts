export interface IdentitySnapshot {
  userId: string;
  sessionId: string;
  displayName: string;
  email: string;
  emailVerified: boolean;
  mfaEnabled: boolean;
  authenticatedAt: string;
  recentAuthenticationExpiresAt: string;
  authorizationVersion: number;
  roles: readonly string[];
  permissions: readonly string[];
  authenticationMethods: readonly string[];
}

export interface AuthSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  identity: IdentitySnapshot;
}

export interface RegisterRequest {
  displayName: string;
  email: string;
  password: string;
}

export interface SignInRequest {
  email: string;
  password: string;
}

export interface AuthenticatedSignInResult {
  outcome: 'authenticated';
  session: AuthSession;
  challengeToken: null;
  challengeExpiresAt: null;
}

export interface MfaRequiredSignInResult {
  outcome: 'mfaRequired';
  session: null;
  challengeToken: string;
  challengeExpiresAt: string;
}

export type SignInResult = AuthenticatedSignInResult | MfaRequiredSignInResult;

export interface AcceptedResult {
  accepted: boolean;
}

export interface CompletedResult {
  completed: boolean;
}

export interface MfaSetupResult {
  secret: string;
  otpAuthUri: string;
}

export interface MfaConfirmResult {
  recoveryCodes: readonly string[];
}

export interface SessionSummary {
  sessionId: string;
  isCurrent: boolean;
  deviceName: string;
  createdAt: string;
  lastUsedAt: string;
  idleExpiresAt: string;
  absoluteExpiresAt: string;
}

export interface SessionsResult {
  sessions: readonly SessionSummary[];
}

export interface CredentialsChangeRequest {
  currentPassword: string;
  newPassword: string;
}

export interface PasswordResetRequest {
  userId: string;
  token: string;
  newPassword: string;
}

export interface EmailVerificationConfirmRequest {
  userId: string;
  token: string;
}
