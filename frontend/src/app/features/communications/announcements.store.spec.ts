import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { CommunicationsApiClient } from '../../core/api/communications-api.client';
import type {
  Announcement,
  CommunicationRealtimeEvent,
} from '../../core/api/communications-api.types';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CommunicationsRealtimeService } from '../../core/realtime/communications-realtime.service';
import { AnnouncementsStore } from './announcements.store';

describe('AnnouncementsStore', () => {
  let store: AnnouncementsStore;
  let api: ReturnType<typeof createApi>;
  let online: ReturnType<typeof signal<boolean>>;

  beforeEach(() => {
    api = createApi();
    online = signal(true);
    TestBed.configureTestingModule({
      providers: [
        AnnouncementsStore,
        { provide: CommunicationsApiClient, useValue: api },
        { provide: ConnectivityStore, useValue: { isOnline: online.asReadonly() } },
        {
          provide: CommunicationsRealtimeService,
          useValue: {
            events$: new Subject<CommunicationRealtimeEvent>(),
            resync$: new Subject<void>(),
          },
        },
      ],
    });
    store = TestBed.inject(AnnouncementsStore);
  });

  it('paginates announcements without duplicating an item', () => {
    api.getAnnouncements
      .mockReturnValueOnce(of({ items: [announcement], nextCursor: 'cursor-1', hasMore: true }))
      .mockReturnValueOnce(
        of({ items: [announcement, secondAnnouncement], nextCursor: null, hasMore: false }),
      );
    store.load('course-1');
    store.loadMore();

    expect(api.getAnnouncements).toHaveBeenLastCalledWith('course-1', 20, 'cursor-1');
    expect(store.state().items.map((item) => item.id)).toEqual([
      'announcement-2',
      'announcement-1',
    ]);
  });

  it('reports a version conflict without replacing the current announcement', () => {
    api.getAnnouncements.mockReturnValue(
      of({ items: [announcement], nextCursor: null, hasMore: false }),
    );
    api.updateAnnouncement.mockReturnValue(
      throwError(
        () =>
          new ApiProblem(
            409,
            'ANNOUNCEMENT.VERSION_CONFLICT',
            null,
            null,
            null,
            {},
            'The version changed.',
          ),
      ),
    );
    store.load('course-1');

    store.update(
      'course-1',
      'announcement-1',
      'My retained draft',
      'Retained body',
      1,
      'stable-key',
    );

    expect(store.action()).toMatchObject({
      status: 'conflict',
      operation: 'update',
      announcementId: 'announcement-1',
    });
    expect(store.state().items[0]).toEqual(announcement);
    expect(api.updateAnnouncement).toHaveBeenCalledWith(
      'course-1',
      'announcement-1',
      { title: 'My retained draft', body: 'Retained body', expectedVersion: 1 },
      'stable-key',
    );
  });

  it('does not start a mutation while offline', () => {
    online.set(false);

    store.create('course-1', 'Notice', 'Body', 'stable-key');

    expect(api.createAnnouncement).not.toHaveBeenCalled();
    expect(store.action().status).toBe('offline');
  });
});

const createApi = () => ({
  getAnnouncements: vi.fn<CommunicationsApiClient['getAnnouncements']>(() =>
    of({ items: [], nextCursor: null, hasMore: false }),
  ),
  createAnnouncement: vi.fn<CommunicationsApiClient['createAnnouncement']>(() => of(announcement)),
  updateAnnouncement: vi.fn<CommunicationsApiClient['updateAnnouncement']>(() => of(announcement)),
  deleteAnnouncement: vi.fn<CommunicationsApiClient['deleteAnnouncement']>(() => of(true)),
});

const announcement: Announcement = {
  id: 'announcement-1',
  courseId: 'course-1',
  createdByUserId: 'user-1',
  title: 'Course notice',
  body: 'Bounded notice body.',
  version: 1,
  targetCount: 14,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
};

const secondAnnouncement: Announcement = {
  ...announcement,
  id: 'announcement-2',
  title: 'Later notice',
  createdAt: '2030-01-02T00:00:00Z',
  updatedAt: '2030-01-02T00:00:00Z',
};
