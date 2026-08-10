import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { EngagementApiClient } from './engagement-api.client';
import { IdentityApiClient } from './identity-api.client';

describe('EngagementApiClient', () => {
  let client: EngagementApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: IdentityApiClient, useValue: { bootstrapCsrf: vi.fn(() => of(undefined)) } },
      ],
    });
    client = TestBed.inject(EngagementApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('creates one idempotent course review', async () => {
    const promise = firstValueFrom(client.createCourseReview('course-1', 5, 'Useful course'));
    const request = http.expectOne('courses/course-1/reviews');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBeTruthy();
    expect(request.request.body).toEqual({ rating: 5, text: 'Useful course' });
    request.flush({ data: review });
    await expect(promise).resolves.toEqual(review);
  });

  it('loads release-scoped lesson discussions with an authenticated read', async () => {
    const promise = firstValueFrom(client.getDiscussionThreads('enrollment-1', 'lesson-1'));
    const request = http.expectOne(
      (candidate) =>
        candidate.url === 'learning/enrollments/enrollment-1/lessons/lesson-1/discussions' &&
        candidate.params.get('limit') === '20',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ data: discussionPage });
    await expect(promise).resolves.toEqual(discussionPage);
  });

  it('uses the enrollment discussion path for course-level discussions', async () => {
    const promise = firstValueFrom(
      client.getDiscussionThread('enrollment/1', null, 'thread/1', 'cursor-1', 25),
    );
    const request = http.expectOne(
      (candidate) =>
        candidate.url === 'learning/enrollments/enrollment%2F1/discussions/thread%2F1' &&
        candidate.params.get('commentCursor') === 'cursor-1' &&
        candidate.params.get('commentLimit') === '25',
    );
    expect(request.request.method).toBe('GET');
    request.flush({ data: discussionThread });
    await expect(promise).resolves.toEqual(discussionThread);
  });

  it('creates an idempotent discussion comment with a concrete parent', async () => {
    const promise = firstValueFrom(
      client.createDiscussionComment(
        'enrollment-1',
        'lesson-1',
        'thread-1',
        'A useful reply',
        'comment-1',
        'idempotency-1',
      ),
    );
    const request = http.expectOne(
      'learning/enrollments/enrollment-1/lessons/lesson-1/discussions/thread-1/comments',
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('Idempotency-Key')).toBe('idempotency-1');
    expect(request.request.body).toEqual({
      body: 'A useful reply',
      parentCommentId: 'comment-1',
    });
    request.flush({ data: discussionComment });
    await expect(promise).resolves.toEqual(discussionComment);
  });

  it('uses an idempotent put when liking a comment', async () => {
    const promise = firstValueFrom(
      client.setDiscussionCommentLike('enrollment-1', 'lesson-1', 'thread-1', 'comment-1', true),
    );
    const request = http.expectOne(
      'learning/enrollments/enrollment-1/lessons/lesson-1/discussions/thread-1/comments/comment-1/like',
    );
    expect(request.request.method).toBe('PUT');
    request.flush({ data: { commentId: 'comment-1', liked: true, likeCount: 1 } });
    await expect(promise).resolves.toEqual({
      commentId: 'comment-1',
      liked: true,
      likeCount: 1,
    });
  });
});

const review = {
  id: 'review-1',
  courseId: 'course-1',
  userId: 'user-1',
  authorName: 'Learner',
  rating: 5,
  text: 'Useful course',
  status: 'Published' as const,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
};

const discussionPage = {
  items: [
    {
      id: 'thread-1',
      lessonId: 'lesson-1',
      authorUserId: 'user-1',
      authorName: 'Learner',
      title: 'Release question',
      body: 'How does release pinning work?',
      status: 'Published' as const,
      isEdited: false,
      createdAt: '2030-01-01T00:00:00Z',
      updatedAt: '2030-01-01T00:00:00Z',
      commentCount: 0,
      canEdit: true,
      canDelete: true,
    },
  ],
  nextCursor: null,
  hasMore: false,
};

const discussionComment = {
  id: 'comment-2',
  threadId: 'thread-1',
  parentCommentId: 'comment-1',
  authorUserId: 'user-1',
  authorName: 'Learner',
  body: 'A useful reply',
  depth: 1 as const,
  status: 'Published' as const,
  isEdited: false,
  likeCount: 0,
  likedByViewer: false,
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
  canEdit: true,
  canDelete: true,
};

const discussionThread = {
  ...discussionPage.items[0],
  courseId: 'course-1',
  releaseId: 'release-1',
  lessonId: null,
  commentCount: 1,
  comments: {
    items: [discussionComment],
    nextCursor: null,
    hasMore: false,
  },
};
