import { DOCUMENT } from '@angular/common';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  it('does not read browser-only dataset state during server rendering', () => {
    const documentElement = Object.defineProperty({}, 'dataset', {
      get: () => {
        throw new Error('SSR must not read HTMLElement.dataset.');
      },
    });
    TestBed.configureTestingModule({
      providers: [
        ThemeService,
        { provide: PLATFORM_ID, useValue: 'server' },
        { provide: DOCUMENT, useValue: { documentElement } },
      ],
    });

    expect(TestBed.inject(ThemeService).effectiveTheme()).toBe('light');
  });
});
