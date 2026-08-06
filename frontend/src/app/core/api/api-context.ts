import { HttpContextToken } from '@angular/common/http';

export const API_REQUEST = new HttpContextToken<boolean>(() => false);
export const PUBLIC_API_REQUEST = new HttpContextToken<boolean>(() => false);
export const DEADLINE_MS = new HttpContextToken<number>(() => 15_000);
export const RETRY_IDEMPOTENT_GET = new HttpContextToken<boolean>(() => true);
export const SKIP_AUTH = new HttpContextToken<boolean>(() => false);
export const SKIP_REFRESH = new HttpContextToken<boolean>(() => false);
