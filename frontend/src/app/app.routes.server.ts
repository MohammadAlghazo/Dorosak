import { RenderMode, type ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  { path: ':locale/dashboard', renderMode: RenderMode.Client },
  { path: ':locale/my-learning', renderMode: RenderMode.Client },
  { path: ':locale/chat', renderMode: RenderMode.Client },
  { path: ':locale/chat/**', renderMode: RenderMode.Client },
  { path: ':locale/notifications', renderMode: RenderMode.Client },
  { path: ':locale/learn/**', renderMode: RenderMode.Client },
  { path: ':locale/instructor/**', renderMode: RenderMode.Client },
  { path: ':locale/admin/**', renderMode: RenderMode.Client },
  { path: ':locale/settings', renderMode: RenderMode.Client },
  { path: ':locale/settings/**', renderMode: RenderMode.Client },
  { path: ':locale/not-found', renderMode: RenderMode.Server, status: 404 },
  { path: ':locale', renderMode: RenderMode.Server },
  { path: ':locale/courses', renderMode: RenderMode.Server },
  { path: ':locale/courses/:slug', renderMode: RenderMode.Server },
  { path: ':locale/search', renderMode: RenderMode.Server },
  { path: ':locale/auth/**', renderMode: RenderMode.Server },
  { path: ':locale/offline', renderMode: RenderMode.Server },
  { path: '', renderMode: RenderMode.Server },
  { path: '**', renderMode: RenderMode.Server, status: 404 },
];
