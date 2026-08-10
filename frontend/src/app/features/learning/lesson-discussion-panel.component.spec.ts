import { signal } from '@angular/core';
import { TestBed, type ComponentFixture } from '@angular/core/testing';
import { NgModel } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { Subject, of } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { EngagementApiClient } from '../../core/api/engagement-api.client';
import type {
  DiscussionComment,
  DiscussionThread,
  DiscussionThreadPage,
  DiscussionThreadSummary,
} from '../../core/api/engagement-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { LessonDiscussionPanelComponent } from './lesson-discussion-panel.component';

describe('LessonDiscussionPanelComponent', () => {
  let api: ReturnType<typeof createApi>;

  beforeEach(async () => {
    api = createApi();
    const locale = signal<'ar' | 'en'>('en');
    const online = signal(true);
    await TestBed.configureTestingModule({
      imports: [LessonDiscussionPanelComponent],
      providers: [
        { provide: EngagementApiClient, useValue: api },
        { provide: LocaleService, useValue: { locale: locale.asReadonly() } },
        { provide: ConnectivityStore, useValue: { isOnline: online.asReadonly() } },
      ],
    }).compileComponents();
  });

  it('cancels the old list request and ignores it when the lesson input changes', async () => {
    const lessonOne = new Subject<DiscussionThreadPage>();
    const lessonTwo = new Subject<DiscussionThreadPage>();
    api.getDiscussionThreads.mockImplementation((_enrollmentId, lessonId) =>
      lessonId === 'lesson-1' ? lessonOne : lessonTwo,
    );
    const fixture = await render('lesson-1');

    fixture.componentRef.setInput('lessonId', 'lesson-2');
    fixture.detectChanges();

    expect(lessonOne.observed).toBe(false);
    lessonOne.next(threadPage([threadSummary('old-thread', { title: 'Old lesson' })]));
    lessonTwo.next(threadPage([threadSummary('new-thread', { title: 'New lesson' })]));
    fixture.detectChanges();

    expect(text(fixture)).toContain('New lesson');
    expect(text(fixture)).not.toContain('Old lesson');
  });

  it('cancels the previous detail request when another thread is opened', async () => {
    const firstDetail = new Subject<DiscussionThread>();
    const secondDetail = new Subject<DiscussionThread>();
    api.getDiscussionThreads.mockReturnValue(
      of(
        threadPage([
          threadSummary('thread-1', { title: 'First question' }),
          threadSummary('thread-2', { title: 'Second question' }),
        ]),
      ),
    );
    api.getDiscussionThread.mockImplementation((_enrollmentId, _lessonId, threadId) =>
      threadId === 'thread-1' ? firstDetail : secondDetail,
    );
    const fixture = await render();
    const cards = root(fixture).querySelectorAll<HTMLButtonElement>('.thread-card');

    cards[0]?.click();
    fixture.detectChanges();
    cards[1]?.click();
    fixture.detectChanges();

    expect(firstDetail.observed).toBe(false);
    secondDetail.next(discussionThread('thread-2', { title: 'Second detail' }));
    firstDetail.next(discussionThread('thread-1', { title: 'Stale first detail' }));
    fixture.detectChanges();

    expect(root(fixture).querySelector('.thread-detail h4')?.textContent).toContain(
      'Second detail',
    );
    expect(text(fixture)).not.toContain('Stale first detail');
  });

  it('merges pagination into the latest state, deduplicates ids, and keeps the authoritative count', async () => {
    const nextPage = new Subject<DiscussionThreadPage>();
    const initial = threadPage(
      [threadSummary('thread-1', { title: 'Existing thread', commentCount: 1 })],
      'cursor-1',
      true,
    );
    api.getDiscussionThreads.mockImplementation((_enrollmentId, _lessonId, cursor) =>
      cursor ? nextPage : of(initial),
    );
    api.getDiscussionThread.mockReturnValue(
      of(
        discussionThread('thread-1', {
          title: 'Existing thread',
          commentCount: 9,
          comments: commentPage([discussionComment('comment-1')]),
        }),
      ),
    );
    const fixture = await render();

    button(fixture, 'Load more discussions').click();
    root(fixture).querySelector<HTMLButtonElement>('.thread-card')?.click();
    fixture.detectChanges();
    nextPage.next(
      threadPage([
        threadSummary('thread-1', { title: 'Stale duplicate', commentCount: 2 }),
        threadSummary('thread-2', { title: 'Older thread', commentCount: 3 }),
        threadSummary('thread-2', { title: 'Duplicate older thread', commentCount: 3 }),
      ]),
    );
    fixture.detectChanges();

    const cards = [...root(fixture).querySelectorAll<HTMLButtonElement>('.thread-card')];
    expect(cards).toHaveLength(2);
    expect(cards[0]?.textContent).toContain('Existing thread');
    expect(cards[0]?.textContent).toContain('9 replies');
    expect(cards[0]?.textContent).not.toContain('Stale duplicate');
    expect(cards[1]?.textContent).toContain('Older thread');
  });

  it('does not issue parallel like requests for the same comment', async () => {
    const likeResult = new Subject<{ commentId: string; liked: boolean; likeCount: number }>();
    api.getDiscussionThreads.mockReturnValue(of(threadPage([threadSummary('thread-1')])));
    api.getDiscussionThread.mockReturnValue(
      of(
        discussionThread('thread-1', {
          commentCount: 1,
          comments: commentPage([discussionComment('comment-1', { likeCount: 2 })]),
        }),
      ),
    );
    api.setDiscussionCommentLike.mockReturnValue(likeResult);
    const fixture = await render();
    root(fixture).querySelector<HTMLButtonElement>('.thread-card')?.click();
    fixture.detectChanges();
    const likeButton = root(fixture).querySelector<HTMLButtonElement>('[aria-label^="Like reply"]');

    likeButton?.click();
    likeButton?.click();

    expect(api.setDiscussionCommentLike).toHaveBeenCalledTimes(1);
    likeResult.next({ commentId: 'comment-1', liked: true, likeCount: 3 });
    fixture.detectChanges();
    expect(root(fixture).querySelector('[aria-label^="Unlike reply"]')?.textContent).toContain('3');
  });

  it('merges comment pagination with newer state and uses the authoritative count', async () => {
    const nextPage = new Subject<DiscussionThread>();
    const firstComment = discussionComment('comment-1', { likeCount: 1 });
    const initial = discussionThread('thread-1', {
      commentCount: 3,
      comments: commentPage([firstComment], 'comment-cursor', true),
    });
    api.getDiscussionThreads.mockReturnValue(of(threadPage([threadSummary('thread-1')])));
    api.getDiscussionThread.mockImplementation((_enrollmentId, _lessonId, _threadId, cursor) =>
      cursor ? nextPage : of(initial),
    );
    api.setDiscussionCommentLike.mockReturnValue(
      of({ commentId: 'comment-1', liked: true, likeCount: 5 }),
    );
    const fixture = await render();
    root(fixture).querySelector<HTMLButtonElement>('.thread-card')?.click();
    fixture.detectChanges();

    button(fixture, 'Load older replies').click();
    root(fixture).querySelector<HTMLButtonElement>('[aria-label^="Like reply"]')?.click();
    nextPage.next(
      discussionThread('thread-1', {
        commentCount: 2,
        comments: commentPage([
          discussionComment('comment-1', { likeCount: 1 }),
          discussionComment('comment-2', { body: 'Older response' }),
          discussionComment('comment-2', { body: 'Duplicate response' }),
        ]),
      }),
    );
    fixture.detectChanges();

    expect(root(fixture).querySelectorAll('.comment')).toHaveLength(2);
    expect(
      root(fixture).querySelector('[aria-label^="Unlike reply"]')?.getAttribute('aria-label'),
    ).toContain('5 likes');
    expect(root(fixture).querySelector('.thread-card')?.textContent).toContain('2 replies');
    expect(text(fixture)).not.toContain('Duplicate response');
  });

  it('decrements the thread and summary counts when a published comment is deleted', async () => {
    api.getDiscussionThreads.mockReturnValue(
      of(threadPage([threadSummary('thread-1', { commentCount: 2 })])),
    );
    api.getDiscussionThread.mockReturnValue(
      of(
        discussionThread('thread-1', {
          commentCount: 2,
          comments: commentPage([discussionComment('comment-1')]),
        }),
      ),
    );
    const fixture = await render();
    root(fixture).querySelector<HTMLButtonElement>('.thread-card')?.click();
    fixture.detectChanges();

    button(fixture, 'Delete reply').click();
    fixture.detectChanges();
    button(fixture, 'Confirm reply deletion').click();
    fixture.detectChanges();

    expect(api.deleteDiscussionComment).toHaveBeenCalledTimes(1);
    expect(root(fixture).querySelector('.comments-heading span')?.textContent).toContain('1 reply');
    expect(root(fixture).querySelector('.thread-card')?.textContent).toContain('1 reply');
    expect(root(fixture).querySelector('.comment-tombstone')?.textContent).toContain(
      'This reply was removed.',
    );
  });

  it('reuses a thread idempotency key after a timeout until that payload succeeds', async () => {
    const firstAttempt = new Subject<DiscussionThread>();
    const retry = new Subject<DiscussionThread>();
    api.getDiscussionThreads.mockReturnValue(of(threadPage([])));
    api.createDiscussionThread
      .mockReturnValueOnce(firstAttempt)
      .mockReturnValueOnce(retry)
      .mockReturnValueOnce(of(discussionThread('thread-2')));
    const fixture = await render();
    await fillThreadForm(fixture, 'Retry-safe title', 'Retry-safe body');

    button(fixture, 'Start discussion').click();
    firstAttempt.error(timeoutProblem());
    fixture.detectChanges();
    button(fixture, 'Start discussion').click();

    const firstKey = api.createDiscussionThread.mock.calls[0]?.[4];
    const retryKey = api.createDiscussionThread.mock.calls[1]?.[4];
    expect(retryKey).toBe(firstKey);

    retry.next(
      discussionThread('thread-1', { title: 'Retry-safe title', body: 'Retry-safe body' }),
    );
    fixture.detectChanges();
    await fillThreadForm(fixture, 'Retry-safe title', 'Retry-safe body');
    button(fixture, 'Start discussion').click();

    expect(api.createDiscussionThread.mock.calls[2]?.[4]).not.toBe(firstKey);
  });

  it('reuses a comment idempotency key after timeout and supports replies through depth two', async () => {
    const firstAttempt = new Subject<DiscussionComment>();
    const retry = new Subject<DiscussionComment>();
    const parent = discussionComment('comment-1', { depth: 1, body: 'Parent reply' });
    api.getDiscussionThreads.mockReturnValue(of(threadPage([threadSummary('thread-1')])));
    api.getDiscussionThread.mockReturnValue(
      of(
        discussionThread('thread-1', {
          commentCount: 1,
          comments: commentPage([parent]),
        }),
      ),
    );
    api.createDiscussionComment.mockReturnValueOnce(firstAttempt).mockReturnValueOnce(retry);
    const fixture = await render();
    root(fixture).querySelector<HTMLButtonElement>('.thread-card')?.click();
    fixture.detectChanges();
    button(fixture, 'Reply').click();
    fixture.detectChanges();
    await fill(fixture, '#comment-body', 'Nested response');

    expect(text(fixture)).toContain('Replying to');
    const postReply = button(fixture, 'Post reply');
    expect(postReply.disabled).toBe(false);
    postReply.click();
    expect(api.createDiscussionComment).toHaveBeenCalledTimes(1);
    firstAttempt.error(timeoutProblem());
    fixture.detectChanges();
    button(fixture, 'Post reply').click();
    expect(api.createDiscussionComment).toHaveBeenCalledTimes(2);

    expect(api.createDiscussionComment.mock.calls[0]?.[4]).toBe('comment-1');
    expect(api.createDiscussionComment.mock.calls[1]?.[5]).toBe(
      api.createDiscussionComment.mock.calls[0]?.[5],
    );

    retry.next(
      discussionComment('comment-2', {
        parentCommentId: 'comment-1',
        depth: 2,
        body: 'Nested response',
      }),
    );
    fixture.detectChanges();
    const nestedComment = root(fixture).querySelector<HTMLElement>('.comment-depth-2');
    expect(nestedComment?.textContent).toContain('Nested response');
    expect(nestedComment?.textContent).not.toContain('Reply');
  });

  it('renders tombstones without actions and separates edit from delete permissions', async () => {
    const hidden = discussionComment('comment-hidden', {
      status: 'Hidden',
      body: 'Moderated private text',
      canEdit: true,
      canDelete: true,
    });
    const deletable = discussionComment('comment-delete', {
      depth: 1,
      body: 'Deletable response',
      canEdit: false,
      canDelete: true,
    });
    const editableAtLimit = discussionComment('comment-edit', {
      depth: 2,
      body: 'Editable response',
      canEdit: true,
      canDelete: false,
    });
    api.getDiscussionThreads.mockReturnValue(of(threadPage([threadSummary('thread-1')])));
    api.getDiscussionThread.mockReturnValue(
      of(
        discussionThread('thread-1', {
          canEdit: false,
          canDelete: true,
          commentCount: 3,
          comments: commentPage([hidden, deletable, editableAtLimit]),
        }),
      ),
    );
    const fixture = await render();
    root(fixture).querySelector<HTMLButtonElement>('.thread-card')?.click();
    fixture.detectChanges();
    const detail = root(fixture).querySelector<HTMLElement>('.thread-detail');
    const comments = detail?.querySelectorAll<HTMLElement>('.comment');

    expect(detail?.textContent).toContain('Delete discussion');
    expect(detail?.textContent).not.toContain('Edit discussion');
    expect(comments?.[0]?.textContent).toContain('This reply was hidden.');
    expect(comments?.[0]?.textContent).not.toContain('Moderated private text');
    expect(comments?.[0]?.querySelector('button')).toBeNull();
    expect(comments?.[1]?.textContent).toContain('Reply');
    expect(comments?.[1]?.textContent).toContain('Delete reply');
    expect(comments?.[1]?.textContent).not.toContain('Edit reply');
    expect(comments?.[2]?.textContent).toContain('Edit reply');
    expect(comments?.[2]?.textContent).not.toContain('Delete reply');
    expect(comments?.[2]?.textContent).not.toContain('Reply');
  });

  async function render(
    lessonId: string | null = 'lesson-1',
  ): Promise<ComponentFixture<LessonDiscussionPanelComponent>> {
    const fixture = TestBed.createComponent(LessonDiscussionPanelComponent);
    fixture.componentRef.setInput('enrollmentId', 'enrollment-1');
    fixture.componentRef.setInput('lessonId', lessonId);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }
});

const createApi = () => ({
  getDiscussionThreads: vi.fn<EngagementApiClient['getDiscussionThreads']>(() =>
    of(threadPage([])),
  ),
  getDiscussionThread: vi.fn<EngagementApiClient['getDiscussionThread']>(() =>
    of(discussionThread('thread-1')),
  ),
  createDiscussionThread: vi.fn<EngagementApiClient['createDiscussionThread']>(() =>
    of(discussionThread('thread-created')),
  ),
  updateDiscussionThread: vi.fn<EngagementApiClient['updateDiscussionThread']>(() =>
    of(discussionThread('thread-1')),
  ),
  deleteDiscussionThread: vi.fn<EngagementApiClient['deleteDiscussionThread']>(() => of(true)),
  createDiscussionComment: vi.fn<EngagementApiClient['createDiscussionComment']>(() =>
    of(discussionComment('comment-created')),
  ),
  updateDiscussionComment: vi.fn<EngagementApiClient['updateDiscussionComment']>(() =>
    of(discussionComment('comment-1')),
  ),
  deleteDiscussionComment: vi.fn<EngagementApiClient['deleteDiscussionComment']>(() => of(true)),
  setDiscussionCommentLike: vi.fn<EngagementApiClient['setDiscussionCommentLike']>(() =>
    of({ commentId: 'comment-1', liked: true, likeCount: 1 }),
  ),
});

const threadPage = (
  items: readonly DiscussionThreadSummary[],
  nextCursor: string | null = null,
  hasMore = false,
): DiscussionThreadPage => ({ items, nextCursor, hasMore });

const threadSummary = (
  id: string,
  overrides: Partial<DiscussionThreadSummary> = {},
): DiscussionThreadSummary => ({
  id,
  lessonId: 'lesson-1',
  authorUserId: 'user-1',
  authorName: 'Learner',
  title: 'Question title',
  body: 'Question body',
  status: 'Published',
  isEdited: false,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
  commentCount: 0,
  canEdit: true,
  canDelete: true,
  ...overrides,
});

const discussionThread = (
  id: string,
  overrides: Partial<DiscussionThread> = {},
): DiscussionThread => ({
  ...threadSummary(id),
  courseId: 'course-1',
  releaseId: 'release-1',
  commentCount: 0,
  comments: commentPage([]),
  ...overrides,
});

const commentPage = (
  items: readonly DiscussionComment[],
  nextCursor: string | null = null,
  hasMore = false,
) => ({
  items,
  nextCursor,
  hasMore,
});

const discussionComment = (
  id: string,
  overrides: Partial<DiscussionComment> = {},
): DiscussionComment => ({
  id,
  threadId: 'thread-1',
  parentCommentId: null,
  authorUserId: 'user-1',
  authorName: 'Learner',
  body: 'Helpful response',
  depth: 0,
  status: 'Published',
  isEdited: false,
  likeCount: 0,
  likedByViewer: false,
  createdAt: '2030-01-02T00:00:00Z',
  updatedAt: '2030-01-02T00:00:00Z',
  canEdit: true,
  canDelete: true,
  ...overrides,
});

const timeoutProblem = (): ApiProblem =>
  new ApiProblem(408, 'HTTP.408', null, null, null, {}, 'Request timeout');

const root = (fixture: ComponentFixture<LessonDiscussionPanelComponent>): HTMLElement =>
  fixture.nativeElement as HTMLElement;

const text = (fixture: ComponentFixture<LessonDiscussionPanelComponent>): string =>
  root(fixture).textContent;

const button = (
  fixture: ComponentFixture<LessonDiscussionPanelComponent>,
  label: string,
): HTMLButtonElement => {
  const match = [...root(fixture).querySelectorAll<HTMLButtonElement>('button')].find((candidate) =>
    candidate.textContent.includes(label),
  );
  if (!match) throw new Error(`Button not found: ${label}`);
  return match;
};

const fill = async (
  fixture: ComponentFixture<LessonDiscussionPanelComponent>,
  selector: string,
  value: string,
): Promise<void> => {
  const control = root(fixture).querySelector<HTMLInputElement | HTMLTextAreaElement>(selector);
  if (!control) throw new Error(`Form control not found: ${selector}`);
  control.value = value;
  fixture.debugElement.query(By.css(selector)).injector.get(NgModel).viewToModelUpdate(value);
  await fixture.whenStable();
  fixture.detectChanges();
};

const fillThreadForm = async (
  fixture: ComponentFixture<LessonDiscussionPanelComponent>,
  title: string,
  body: string,
): Promise<void> => {
  await fill(fixture, '#discussion-title', title);
  await fill(fixture, '#discussion-body', body);
};
