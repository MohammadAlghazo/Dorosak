import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { Subscription } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { EngagementApiClient } from '../../core/api/engagement-api.client';
import type {
  DiscussionComment,
  DiscussionCommentPage,
  DiscussionStatus,
  DiscussionThread,
  DiscussionThreadPage,
  DiscussionThreadSummary,
} from '../../core/api/engagement-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';

type DiscussionState =
  | { status: 'idle' | 'loading' | 'offline'; page: null; errorCode: null }
  | { status: 'success' | 'empty'; page: DiscussionThreadPage; errorCode: null }
  | { status: 'error'; page: null; errorCode: string };

interface DiscussionScope {
  readonly version: number;
  readonly enrollmentId: string;
  readonly lessonId: string | null;
}

interface ThreadSelectionScope extends DiscussionScope {
  readonly selectionVersion: number;
  readonly threadId: string;
}

@Component({
  selector: 'drs-lesson-discussion-panel',
  imports: [FormsModule],
  template: `
    <div
      class="discussion-panel"
      role="region"
      aria-labelledby="discussion-heading"
      [attr.aria-busy]="state().status === 'loading' || loadingThread()"
    >
      <header class="discussion-heading">
        <div>
          <p class="discussion-kicker">{{ scopeKicker() }}</p>
          <h3 id="discussion-heading">{{ discussionHeading() }}</h3>
          <p class="discussion-description">{{ scopeDescription() }}</p>
        </div>
        <span class="discussion-count" [attr.aria-label]="threadCountLabel()">
          {{ threadCount() }}
        </span>
      </header>

      @switch (state().status) {
        @case ('loading') {
          <div class="discussion-state" role="status">
            <span class="discussion-loader" aria-hidden="true"></span>
            {{ locale.locale() === 'ar' ? 'جار تحميل النقاش…' : 'Loading discussions…' }}
          </div>
        }
        @case ('offline') {
          <div class="discussion-state" role="status">
            <strong>
              {{
                locale.locale() === 'ar'
                  ? 'النقاش غير متاح دون اتصال'
                  : 'Discussions are unavailable offline'
              }}
            </strong>
            <button type="button" class="discussion-secondary" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="discussion-state" role="alert">
            <strong>{{ problemLabel(state().errorCode) }}</strong>
            <button type="button" class="discussion-secondary" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @default {
          <div class="discussion-layout">
            <div class="thread-column">
              <form class="discussion-form" (submit)="$event.preventDefault(); createThread()">
                <label for="discussion-title">
                  {{ locale.locale() === 'ar' ? 'عنوان النقاش' : 'Discussion title' }}
                </label>
                <input
                  id="discussion-title"
                  name="discussion-title"
                  type="text"
                  maxlength="200"
                  autocomplete="off"
                  [(ngModel)]="newThreadTitle"
                  [placeholder]="
                    locale.locale() === 'ar'
                      ? 'مثلاً: كيف أطبق هذا المفهوم؟'
                      : 'For example: How do I apply this concept?'
                  "
                />
                <label for="discussion-body">
                  {{ locale.locale() === 'ar' ? 'السؤال أو الفكرة' : 'Question or thought' }}
                </label>
                <textarea
                  id="discussion-body"
                  name="discussion-body"
                  rows="4"
                  maxlength="10000"
                  [(ngModel)]="newThreadBody"
                  [placeholder]="
                    locale.locale() === 'ar'
                      ? 'اكتب بوضوح ومن دون بيانات شخصية…'
                      : 'Write clearly and avoid personal data…'
                  "
                ></textarea>
                <div class="discussion-form-footer">
                  <small>
                    {{
                      locale.locale() === 'ar'
                        ? newThreadBody.length + ' من 10000 حرف'
                        : newThreadBody.length + ' of 10,000 characters'
                    }}
                  </small>
                  <button
                    type="submit"
                    class="discussion-primary"
                    [disabled]="busy() || !canCreateThread()"
                  >
                    {{
                      busy()
                        ? locale.locale() === 'ar'
                          ? 'جار النشر…'
                          : 'Publishing…'
                        : locale.locale() === 'ar'
                          ? 'ابدأ نقاشاً'
                          : 'Start discussion'
                    }}
                  </button>
                </div>
              </form>

              @if (state().status === 'empty') {
                <div class="discussion-empty">
                  <span aria-hidden="true">01</span>
                  <p>
                    {{
                      locale.locale() === 'ar'
                        ? 'لا توجد نقاشات بعد. كن أول من يبدأ.'
                        : 'No discussions yet. Start the first one.'
                    }}
                  </p>
                </div>
              } @else if (state().page; as page) {
                <div
                  class="thread-list"
                  [attr.aria-label]="
                    locale.locale() === 'ar' ? 'قائمة النقاشات' : 'Discussion threads'
                  "
                >
                  @for (thread of page.items; track thread.id) {
                    <button
                      type="button"
                      class="thread-card"
                      [class.selected]="selectedThreadId() === thread.id"
                      [class.tombstone]="thread.status !== 'Published'"
                      [attr.aria-current]="selectedThreadId() === thread.id ? 'true' : null"
                      [attr.aria-label]="threadAriaLabel(thread)"
                      (click)="openThread(thread)"
                    >
                      <span class="thread-card-top">
                        @if (thread.status === 'Published') {
                          <strong>{{ thread.title }}</strong>
                        } @else {
                          <strong>{{ tombstoneLabel('thread', thread.status) }}</strong>
                        }
                        <small>{{ replyCountLabel(thread.commentCount) }}</small>
                      </span>
                      @if (thread.status === 'Published') {
                        <span class="thread-preview">{{ thread.body }}</span>
                        <span class="thread-meta">
                          {{ thread.authorName }} ·
                          <time [attr.datetime]="thread.createdAt">
                            {{ formattedDate(thread.createdAt) }}
                          </time>
                          @if (thread.isEdited) {
                            · {{ locale.locale() === 'ar' ? 'معدّل' : 'edited' }}
                          }
                        </span>
                      } @else {
                        <span class="thread-status">{{ statusLabel(thread.status) }}</span>
                      }
                    </button>
                  }
                </div>
                @if (page.hasMore) {
                  <button
                    type="button"
                    class="discussion-load-more"
                    [disabled]="loadingMore()"
                    (click)="loadMoreThreads()"
                  >
                    {{
                      loadingMore()
                        ? locale.locale() === 'ar'
                          ? 'جار التحميل…'
                          : 'Loading…'
                        : locale.locale() === 'ar'
                          ? 'تحميل نقاشات إضافية'
                          : 'Load more discussions'
                    }}
                  </button>
                }
              }
            </div>

            <div class="thread-detail" aria-live="polite">
              @if (loadingThread()) {
                <div class="thread-placeholder" role="status">
                  <span class="discussion-loader" aria-hidden="true"></span>
                  <p>
                    {{
                      locale.locale() === 'ar'
                        ? 'جار تحميل النقاش والردود…'
                        : 'Loading the discussion and replies…'
                    }}
                  </p>
                </div>
              } @else if (selectedThread(); as thread) {
                <article>
                  <header class="thread-detail-heading">
                    <div>
                      @if (thread.status === 'Published') {
                        <p class="discussion-kicker">
                          {{ thread.authorName }} ·
                          <time [attr.datetime]="thread.createdAt">
                            {{ formattedDate(thread.createdAt) }}
                          </time>
                          @if (thread.isEdited) {
                            · {{ locale.locale() === 'ar' ? 'معدّل' : 'edited' }}
                          }
                        </p>
                        <h4>{{ thread.title }}</h4>
                      } @else {
                        <p class="discussion-kicker">{{ statusLabel(thread.status) }}</p>
                        <h4>{{ tombstoneLabel('thread', thread.status) }}</h4>
                      }
                    </div>
                    @if (thread.status === 'Published' && (thread.canEdit || thread.canDelete)) {
                      <div class="thread-actions">
                        @if (confirmDeleteThread() && thread.canDelete) {
                          <button
                            type="button"
                            class="danger-action"
                            (click)="deleteThread(thread)"
                          >
                            {{ locale.locale() === 'ar' ? 'تأكيد حذف النقاش' : 'Confirm deletion' }}
                          </button>
                          <button
                            type="button"
                            class="discussion-link"
                            (click)="confirmDeleteThread.set(false)"
                          >
                            {{ locale.locale() === 'ar' ? 'إلغاء الحذف' : 'Cancel deletion' }}
                          </button>
                        } @else {
                          @if (thread.canEdit) {
                            <button
                              type="button"
                              class="discussion-link"
                              (click)="beginEditThread(thread)"
                            >
                              {{ locale.locale() === 'ar' ? 'تعديل النقاش' : 'Edit discussion' }}
                            </button>
                          }
                          @if (thread.canDelete) {
                            <button
                              type="button"
                              class="danger-link"
                              (click)="confirmDeleteThread.set(true)"
                            >
                              {{ locale.locale() === 'ar' ? 'حذف النقاش' : 'Delete discussion' }}
                            </button>
                          }
                        }
                      </div>
                    }
                  </header>

                  @if (thread.status !== 'Published') {
                    <p class="thread-tombstone">{{ tombstoneLabel('thread', thread.status) }}</p>
                  } @else if (editingThread()) {
                    <form class="edit-form" (submit)="$event.preventDefault(); saveThread(thread)">
                      <label for="edit-thread-title">
                        {{ locale.locale() === 'ar' ? 'عنوان النقاش' : 'Discussion title' }}
                      </label>
                      <input
                        id="edit-thread-title"
                        name="edit-thread-title"
                        type="text"
                        maxlength="200"
                        [(ngModel)]="editThreadTitle"
                      />
                      <label for="edit-thread-body">
                        {{ locale.locale() === 'ar' ? 'محتوى النقاش' : 'Discussion content' }}
                      </label>
                      <textarea
                        id="edit-thread-body"
                        name="edit-thread-body"
                        rows="4"
                        maxlength="10000"
                        [(ngModel)]="editThreadBody"
                      ></textarea>
                      <div class="thread-actions">
                        <button
                          type="submit"
                          class="discussion-primary"
                          [disabled]="busy() || !canSaveThread()"
                        >
                          {{ locale.locale() === 'ar' ? 'حفظ التعديلات' : 'Save changes' }}
                        </button>
                        <button type="button" class="discussion-link" (click)="cancelEditThread()">
                          {{ locale.locale() === 'ar' ? 'إلغاء التعديل' : 'Cancel editing' }}
                        </button>
                      </div>
                    </form>
                  } @else {
                    <p class="thread-body">{{ thread.body }}</p>
                  }

                  <section
                    class="comments"
                    [attr.aria-label]="
                      locale.locale() === 'ar' ? 'ردود النقاش' : 'Discussion replies'
                    "
                  >
                    <div class="comments-heading">
                      <h5>{{ locale.locale() === 'ar' ? 'الردود' : 'Replies' }}</h5>
                      <span>{{ replyCountLabel(thread.commentCount) }}</span>
                    </div>
                    @if (!thread.comments.items.length) {
                      <p class="muted-comment">
                        {{
                          thread.commentCount === 0
                            ? locale.locale() === 'ar'
                              ? 'كن أول من يرد.'
                              : 'Be the first to reply.'
                            : locale.locale() === 'ar'
                              ? 'لا توجد ردود متاحة للعرض.'
                              : 'No replies are available to display.'
                        }}
                      </p>
                    }
                    @for (comment of thread.comments.items; track comment.id) {
                      <article
                        class="comment"
                        [class.comment-depth-1]="comment.depth === 1"
                        [class.comment-depth-2]="comment.depth === 2"
                        [class.tombstone]="comment.status !== 'Published'"
                      >
                        @if (comment.status !== 'Published') {
                          <p class="comment-tombstone">
                            {{ tombstoneLabel('comment', comment.status) }}
                          </p>
                        } @else {
                          <header>
                            <strong>{{ comment.authorName }}</strong>
                            <small>
                              <time [attr.datetime]="comment.createdAt">
                                {{ formattedDate(comment.createdAt) }}
                              </time>
                              @if (comment.isEdited) {
                                · {{ locale.locale() === 'ar' ? 'معدّل' : 'edited' }}
                              }
                            </small>
                          </header>
                          @if (editingCommentId() === comment.id) {
                            <form (submit)="$event.preventDefault(); saveComment(comment)">
                              <label [for]="'edit-comment-body-' + comment.id">
                                {{ locale.locale() === 'ar' ? 'نص الرد' : 'Reply text' }}
                              </label>
                              <textarea
                                [id]="'edit-comment-body-' + comment.id"
                                name="edit-comment-body"
                                rows="3"
                                maxlength="5000"
                                [(ngModel)]="editCommentBody"
                              ></textarea>
                              <div class="thread-actions">
                                <button
                                  type="submit"
                                  class="discussion-primary"
                                  [disabled]="busy() || !editCommentBody.trim()"
                                >
                                  {{ locale.locale() === 'ar' ? 'حفظ الرد' : 'Save reply' }}
                                </button>
                                <button
                                  type="button"
                                  class="discussion-link"
                                  (click)="cancelEditComment()"
                                >
                                  {{
                                    locale.locale() === 'ar' ? 'إلغاء التعديل' : 'Cancel editing'
                                  }}
                                </button>
                              </div>
                            </form>
                          } @else {
                            <p>{{ comment.body }}</p>
                          }
                          @if (thread.status === 'Published') {
                            <footer>
                              <button
                                type="button"
                                class="comment-action"
                                [disabled]="isLikePending(comment.id)"
                                [attr.aria-pressed]="comment.likedByViewer"
                                [attr.aria-label]="likeLabel(comment)"
                                (click)="toggleLike(comment)"
                              >
                                <span aria-hidden="true">
                                  {{ comment.likedByViewer ? '♥' : '♡' }}
                                </span>
                                {{ comment.likeCount }}
                              </button>
                              @if (comment.depth < 2) {
                                <button
                                  type="button"
                                  class="comment-action"
                                  (click)="replyTo(comment)"
                                >
                                  {{ locale.locale() === 'ar' ? 'الرد على التعليق' : 'Reply' }}
                                </button>
                              }
                              @if (comment.canEdit) {
                                <button
                                  type="button"
                                  class="comment-action"
                                  (click)="beginEditComment(comment)"
                                >
                                  {{ locale.locale() === 'ar' ? 'تعديل الرد' : 'Edit reply' }}
                                </button>
                              }
                              @if (comment.canDelete) {
                                @if (confirmDeleteCommentId() === comment.id) {
                                  <button
                                    type="button"
                                    class="danger-action"
                                    (click)="deleteComment(comment)"
                                  >
                                    {{
                                      locale.locale() === 'ar'
                                        ? 'تأكيد حذف الرد'
                                        : 'Confirm reply deletion'
                                    }}
                                  </button>
                                  <button
                                    type="button"
                                    class="comment-action"
                                    (click)="confirmDeleteCommentId.set(null)"
                                  >
                                    {{
                                      locale.locale() === 'ar' ? 'إلغاء الحذف' : 'Cancel deletion'
                                    }}
                                  </button>
                                } @else {
                                  <button
                                    type="button"
                                    class="danger-link"
                                    (click)="confirmDeleteCommentId.set(comment.id)"
                                  >
                                    {{ locale.locale() === 'ar' ? 'حذف الرد' : 'Delete reply' }}
                                  </button>
                                }
                              }
                            </footer>
                          }
                        }
                      </article>
                    }
                    @if (thread.comments.hasMore) {
                      <button
                        type="button"
                        class="discussion-load-more"
                        [disabled]="loadingComments()"
                        (click)="loadMoreComments(thread)"
                      >
                        {{
                          loadingComments()
                            ? locale.locale() === 'ar'
                              ? 'جار تحميل الردود…'
                              : 'Loading replies…'
                            : locale.locale() === 'ar'
                              ? 'تحميل ردود أقدم'
                              : 'Load older replies'
                        }}
                      </button>
                    }
                  </section>

                  @if (thread.status === 'Published') {
                    <form
                      class="comment-form"
                      (submit)="$event.preventDefault(); createComment(thread)"
                    >
                      @if (replyParent(); as parent) {
                        <div class="reply-context">
                          <p>
                            {{ locale.locale() === 'ar' ? 'الرد على' : 'Replying to' }}
                            <strong>{{ parent.authorName }}</strong>
                          </p>
                          <button type="button" class="discussion-link" (click)="cancelReply()">
                            {{ locale.locale() === 'ar' ? 'إلغاء الرد المقتبس' : 'Cancel reply' }}
                          </button>
                        </div>
                      }
                      <label for="comment-body">
                        {{
                          replyParent()
                            ? locale.locale() === 'ar'
                              ? 'اكتب ردك على التعليق'
                              : 'Write your nested reply'
                            : locale.locale() === 'ar'
                              ? 'أضف رداً إلى النقاش'
                              : 'Add a reply to the discussion'
                        }}
                      </label>
                      <textarea
                        id="comment-body"
                        name="comment-body"
                        rows="3"
                        maxlength="5000"
                        [(ngModel)]="newCommentBody"
                        [placeholder]="
                          locale.locale() === 'ar' ? 'اكتب رداً مفيداً…' : 'Write a helpful reply…'
                        "
                      ></textarea>
                      <button
                        type="submit"
                        class="discussion-primary"
                        [disabled]="busy() || !newCommentBody.trim()"
                      >
                        {{
                          busy()
                            ? locale.locale() === 'ar'
                              ? 'جار إرسال الرد…'
                              : 'Posting reply…'
                            : locale.locale() === 'ar'
                              ? 'إرسال الرد'
                              : 'Post reply'
                        }}
                      </button>
                    </form>
                  }
                </article>
              } @else {
                <div class="thread-placeholder">
                  <span aria-hidden="true">→</span>
                  <p>
                    {{
                      locale.locale() === 'ar'
                        ? 'اختر نقاشاً لعرض الردود.'
                        : 'Select a discussion to read its replies.'
                    }}
                  </p>
                </div>
              }
            </div>
          </div>
        }
      }

      @if (actionError()) {
        <p class="discussion-error" role="alert">{{ problemLabel(actionError()) }}</p>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .discussion-panel {
      margin-block-start: var(--space-7);
      padding: clamp(var(--space-4), 4vw, var(--space-7));
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-2);
      box-shadow: var(--shadow-1);
    }
    .discussion-heading,
    .thread-detail-heading,
    .thread-card-top,
    .discussion-form-footer,
    .thread-actions,
    .comment header,
    .comment footer,
    .comments-heading,
    .reply-context {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
    }
    .discussion-heading {
      align-items: start;
      margin-block-end: var(--space-5);
    }
    .discussion-kicker {
      margin: 0;
      color: var(--color-brand);
      font: 700 0.72rem/1.2 monospace;
      letter-spacing: 0.12em;
    }
    .discussion-heading h3 {
      margin: var(--space-2) 0;
      font-size: clamp(1.6rem, 3vw, 2.6rem);
    }
    .discussion-description,
    .thread-meta,
    .comment small,
    .discussion-form small,
    .muted-comment,
    .thread-status {
      color: var(--color-muted);
    }
    .discussion-description {
      max-inline-size: 55ch;
      margin: 0;
      line-height: 1.7;
    }
    .discussion-count {
      color: var(--color-brand);
      font: 700 2rem/1 monospace;
    }
    .discussion-layout {
      display: grid;
      grid-template-columns: minmax(15rem, 0.8fr) minmax(0, 1.4fr);
      gap: var(--space-5);
    }
    .thread-column,
    .thread-detail {
      min-inline-size: 0;
    }
    .discussion-form,
    .comment-form,
    .edit-form,
    .comment form {
      display: grid;
      gap: var(--space-3);
      padding: var(--space-4);
      background: var(--color-subtle);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-1);
    }
    .discussion-form {
      margin-block-end: var(--space-4);
    }
    .discussion-form label,
    .comment-form label,
    .edit-form label,
    .comment form label {
      font-weight: 700;
    }
    input,
    textarea {
      inline-size: 100%;
      min-block-size: 44px;
      padding: var(--space-3);
      color: var(--color-text);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-1);
      resize: vertical;
    }
    .discussion-primary,
    .discussion-secondary,
    .discussion-load-more,
    .discussion-link,
    .danger-link,
    .danger-action,
    .comment-action {
      min-block-size: 44px;
      padding-inline: var(--space-3);
      font-weight: 700;
      border-radius: var(--radius-1);
    }
    .discussion-primary,
    .discussion-secondary,
    .discussion-load-more {
      border: 1px solid var(--color-brand);
    }
    .discussion-primary {
      color: var(--color-on-brand);
      background: var(--color-brand);
    }
    .discussion-secondary,
    .discussion-load-more {
      color: var(--color-brand);
      background: transparent;
    }
    button:disabled {
      cursor: not-allowed;
      opacity: 0.55;
    }
    .thread-list {
      display: grid;
      gap: var(--space-2);
    }
    .thread-card {
      display: grid;
      gap: var(--space-2);
      inline-size: 100%;
      min-block-size: 44px;
      padding: var(--space-4);
      color: var(--color-text);
      text-align: start;
      background: var(--color-subtle);
      border: 1px solid var(--color-border);
      border-inline-start: 3px solid transparent;
      border-radius: var(--radius-1);
    }
    .thread-card:hover,
    .thread-card.selected {
      background: color-mix(in srgb, var(--color-brand) 10%, var(--color-surface));
      border-inline-start-color: var(--color-brand);
    }
    .thread-card small {
      color: var(--color-brand);
      white-space: nowrap;
    }
    .thread-preview {
      display: -webkit-box;
      overflow: hidden;
      color: var(--color-text);
      -webkit-box-orient: vertical;
      -webkit-line-clamp: 3;
    }
    .thread-meta,
    .thread-status {
      font-size: 0.8rem;
    }
    .thread-card.tombstone,
    .comment.tombstone {
      background: color-mix(in srgb, var(--color-muted) 8%, var(--color-surface));
      border-style: dashed;
    }
    .discussion-load-more {
      inline-size: 100%;
      margin-block-start: var(--space-3);
    }
    .thread-detail > article {
      padding: clamp(var(--space-4), 4vw, var(--space-6));
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-1);
    }
    .thread-detail-heading {
      align-items: start;
    }
    .thread-detail-heading h4 {
      margin: var(--space-2) 0 var(--space-4);
      font-size: clamp(1.5rem, 3vw, 2.4rem);
      overflow-wrap: anywhere;
    }
    .thread-body {
      white-space: pre-wrap;
      line-height: 1.85;
      overflow-wrap: anywhere;
    }
    .thread-actions {
      flex-wrap: wrap;
      justify-content: start;
    }
    .discussion-link,
    .danger-link,
    .danger-action,
    .comment-action {
      color: var(--color-link);
      background: transparent;
      border: 0;
    }
    .danger-link,
    .danger-action {
      color: var(--color-danger);
    }
    .danger-action {
      border: 1px solid var(--color-danger);
    }
    .thread-tombstone,
    .comment-tombstone {
      margin: 0;
      padding: var(--space-4);
      color: var(--color-muted);
      background: var(--color-subtle);
      border-inline-start: 3px solid var(--color-muted);
    }
    .comments {
      margin-block-start: var(--space-6);
      padding-block-start: var(--space-5);
      border-block-start: 1px solid var(--color-border);
    }
    .comments-heading h5 {
      margin: 0;
      color: var(--color-brand);
      font-size: 1rem;
    }
    .comments-heading span {
      color: var(--color-muted);
      font-size: 0.85rem;
    }
    .comment {
      display: grid;
      gap: var(--space-2);
      margin-block-start: var(--space-3);
      padding: var(--space-3);
      background: var(--color-subtle);
      border-inline-start: 2px solid var(--color-border);
      border-radius: var(--radius-1);
    }
    .comment-depth-1 {
      margin-inline-start: clamp(var(--space-3), 5vw, var(--space-7));
      border-inline-start-color: var(--color-brand);
    }
    .comment-depth-2 {
      margin-inline-start: clamp(var(--space-6), 10vw, var(--space-12));
      border-inline-start-color: color-mix(in srgb, var(--color-brand) 60%, var(--color-border));
    }
    .comment p {
      margin: 0;
      white-space: pre-wrap;
      line-height: 1.7;
      overflow-wrap: anywhere;
    }
    .comment footer {
      justify-content: start;
      flex-wrap: wrap;
    }
    .comment-action {
      color: var(--color-text);
    }
    .comment-action[aria-pressed='true'] {
      color: var(--color-danger);
    }
    .comment-form {
      margin-block-start: var(--space-4);
    }
    .reply-context {
      align-items: center;
      padding: var(--space-2);
      color: var(--color-muted);
      background: var(--color-surface);
    }
    .reply-context p {
      margin: 0;
    }
    .discussion-state,
    .discussion-empty,
    .thread-placeholder {
      display: grid;
      justify-items: start;
      gap: var(--space-3);
      padding: var(--space-6);
      color: var(--color-muted);
      border: 1px dashed var(--color-border);
      border-radius: var(--radius-1);
    }
    .discussion-empty span,
    .thread-placeholder > span:not(.discussion-loader) {
      color: var(--color-brand);
      font: 700 2rem/1 monospace;
    }
    .discussion-state strong {
      color: var(--color-text);
    }
    .discussion-loader {
      inline-size: 1.5rem;
      block-size: 1.5rem;
      border: 2px solid var(--color-border);
      border-block-start-color: var(--color-brand);
      border-radius: 50%;
      animation: spin 700ms linear infinite;
    }
    .discussion-error {
      margin-block-start: var(--space-4);
      padding: var(--space-3);
      color: var(--color-danger);
      background: color-mix(in srgb, var(--color-danger) 10%, var(--color-surface));
      border: 1px solid var(--color-danger);
      border-radius: var(--radius-1);
    }
    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .discussion-loader {
        animation: none;
      }
    }
    @media (max-width: 760px) {
      .discussion-layout {
        grid-template-columns: 1fr;
      }
      .thread-detail {
        order: -1;
      }
      .discussion-heading,
      .thread-detail-heading,
      .discussion-form-footer {
        align-items: stretch;
        flex-direction: column;
      }
      .discussion-count {
        align-self: start;
      }
      .comment-depth-1 {
        margin-inline-start: var(--space-3);
      }
      .comment-depth-2 {
        margin-inline-start: var(--space-5);
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LessonDiscussionPanelComponent {
  readonly enrollmentId = input.required<string>();
  readonly lessonId = input.required<string | null>();
  protected readonly locale = inject(LocaleService);
  protected readonly state = signal<DiscussionState>({
    status: 'idle',
    page: null,
    errorCode: null,
  });
  protected readonly selectedThread = signal<DiscussionThread | null>(null);
  protected readonly selectedThreadId = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly loadingThread = signal(false);
  protected readonly loadingMore = signal(false);
  protected readonly loadingComments = signal(false);
  protected readonly actionError = signal<string | null>(null);
  protected readonly replyParent = signal<DiscussionComment | null>(null);
  protected readonly editingThread = signal(false);
  protected readonly editingCommentId = signal<string | null>(null);
  protected readonly confirmDeleteThread = signal(false);
  protected readonly confirmDeleteCommentId = signal<string | null>(null);
  protected readonly pendingLikeIds = signal<ReadonlySet<string>>(new Set<string>());
  protected newThreadTitle = '';
  protected newThreadBody = '';
  protected newCommentBody = '';
  protected editThreadTitle = '';
  protected editThreadBody = '';
  protected editCommentBody = '';
  private readonly api = inject(EngagementApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly likeSubscriptions = new Map<string, Subscription>();
  private scope: DiscussionScope = {
    version: 0,
    enrollmentId: '',
    lessonId: null,
  };
  private scopeVersion = 0;
  private selectionVersion = 0;
  private mutationVersion = 0;
  private threadListSubscription: Subscription | null = null;
  private threadPageSubscription: Subscription | null = null;
  private threadDetailSubscription: Subscription | null = null;
  private commentPageSubscription: Subscription | null = null;
  private readonly pendingThreadKeys = new Map<string, string>();
  private readonly pendingCommentKeys = new Map<string, string>();

  constructor() {
    effect(() => {
      const enrollmentId = this.enrollmentId();
      const lessonId = this.lessonId();
      untracked(() => {
        this.activateScope(enrollmentId, lessonId, true);
      });
    });
  }

  protected threadCount(): number {
    return this.state().page?.items.length ?? 0;
  }

  protected threadCountLabel(): string {
    const count = this.threadCount();
    return this.locale.locale() === 'ar'
      ? `عدد النقاشات المحمّلة: ${String(count)}`
      : `${String(count)} loaded ${count === 1 ? 'discussion' : 'discussions'}`;
  }

  protected scopeKicker(): string {
    if (this.locale.locale() === 'ar') {
      return this.lessonId() === null ? 'نقاش المسار' : 'نقاش الدرس';
    }
    return this.lessonId() === null ? 'COURSE DISCUSSION' : 'LESSON DISCUSSION';
  }

  protected discussionHeading(): string {
    return this.locale.locale() === 'ar' ? 'اسأل وشارك الفكرة' : 'Ask, answer, and compare notes';
  }

  protected scopeDescription(): string {
    if (this.locale.locale() === 'ar') {
      return this.lessonId() === null
        ? 'هذا النقاش خاص بالمسار وإصداره المثبت، ولا يظهر إلا للمسجلين المصرح لهم.'
        : 'هذا النقاش خاص بالدرس وإصداره المثبت، ولا يظهر إلا للمسجلين المصرح لهم.';
    }
    return this.lessonId() === null
      ? 'This course discussion belongs to the pinned release and is visible only to authorized learners.'
      : 'This lesson discussion belongs to the pinned release and is visible only to authorized learners.';
  }

  protected canCreateThread(): boolean {
    return this.newThreadTitle.trim().length > 0 && this.newThreadBody.trim().length > 0;
  }

  protected canSaveThread(): boolean {
    return this.editThreadTitle.trim().length > 0 && this.editThreadBody.trim().length > 0;
  }

  protected reload(): void {
    this.activateScope(this.enrollmentId(), this.lessonId(), false);
  }

  protected openThread(summary: DiscussionThreadSummary): void {
    const scope = this.scope;
    this.cancelThreadReads();
    this.cancelLikeRequests();
    this.selectionVersion += 1;
    const selection = this.captureSelection(scope, summary.id);
    this.selectedThreadId.set(summary.id);
    this.selectedThread.set(null);
    this.loadingThread.set(true);
    this.actionError.set(null);
    this.resetThreadInteraction();

    this.threadDetailSubscription = this.api
      .getDiscussionThread(scope.enrollmentId, scope.lessonId, summary.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (thread) => {
          if (!this.isSelectionCurrent(selection)) return;
          const normalized = normalizeThread(thread);
          this.selectedThread.set(normalized);
          this.replaceThreadSummary(normalized);
          this.loadingThread.set(false);
        },
        error: (error: unknown) => {
          if (!this.isSelectionCurrent(selection)) return;
          this.loadingThread.set(false);
          this.selectedThreadId.set(null);
          this.setActionError(error);
        },
      });
  }

  protected createThread(): void {
    const title = this.newThreadTitle.trim();
    const body = this.newThreadBody.trim();
    if (!title || !body) return;
    const mutation = this.beginMutation();
    if (mutation === null) return;

    const scope = this.scope;
    const selectionAtStart = this.selectionVersion;
    const signature = payloadSignature('thread', scope.enrollmentId, scope.lessonId, title, body);
    const key = this.threadIdempotencyKey(signature);
    this.actionError.set(null);
    this.api
      .createDiscussionThread(scope.enrollmentId, scope.lessonId, title, body, key)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (thread) => {
          this.clearThreadKey(signature, key);
          if (this.isScopeCurrent(scope)) {
            const normalized = normalizeThread(thread);
            if (this.newThreadTitle.trim() === title && this.newThreadBody.trim() === body) {
              this.newThreadTitle = '';
              this.newThreadBody = '';
            }
            this.prependThread(normalized);
            if (this.selectionVersion === selectionAtStart) {
              this.cancelThreadReads();
              this.cancelLikeRequests();
              this.selectionVersion += 1;
              this.selectedThreadId.set(normalized.id);
              this.selectedThread.set(normalized);
              this.resetThreadInteraction();
            }
          }
          this.finishMutation(mutation);
        },
        error: (error: unknown) => {
          if (!isRequestTimeout(error)) this.clearThreadKey(signature, key);
          if (this.isScopeCurrent(scope)) this.setActionError(error);
          this.finishMutation(mutation);
        },
      });
  }

  protected loadMoreThreads(): void {
    const page = this.state().page;
    if (!page?.nextCursor || this.loadingMore()) return;
    const scope = this.scope;
    this.loadingMore.set(true);
    this.threadPageSubscription?.unsubscribe();
    this.threadPageSubscription = this.api
      .getDiscussionThreads(scope.enrollmentId, scope.lessonId, page.nextCursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (nextPage) => {
          if (!this.isScopeCurrent(scope)) return;
          this.state.update((current) => {
            if (!current.page) return current;
            const items = mergeUniqueById(current.page.items, nextPage.items);
            return {
              status: items.length ? 'success' : 'empty',
              page: {
                items,
                nextCursor: nextPage.nextCursor,
                hasMore: nextPage.hasMore,
              },
              errorCode: null,
            };
          });
          this.loadingMore.set(false);
        },
        error: (error: unknown) => {
          if (!this.isScopeCurrent(scope)) return;
          this.loadingMore.set(false);
          this.setActionError(error);
        },
      });
  }

  protected beginEditThread(thread: DiscussionThread): void {
    if (thread.status !== 'Published' || !thread.canEdit) return;
    this.editingThread.set(true);
    this.editThreadTitle = thread.title;
    this.editThreadBody = thread.body;
  }

  protected cancelEditThread(): void {
    this.editingThread.set(false);
    this.editThreadTitle = '';
    this.editThreadBody = '';
  }

  protected saveThread(thread: DiscussionThread): void {
    const title = this.editThreadTitle.trim();
    const body = this.editThreadBody.trim();
    if (thread.status !== 'Published' || !thread.canEdit || !title || !body) return;
    const mutation = this.beginMutation();
    if (mutation === null) return;
    const selection = this.captureSelection(this.scope, thread.id);
    this.actionError.set(null);
    this.api
      .updateDiscussionThread(selection.enrollmentId, selection.lessonId, thread.id, title, body)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          if (this.isSelectionCurrent(selection)) {
            this.selectedThread.update((current) =>
              current?.id === updated.id
                ? normalizeThread({ ...updated, comments: current.comments })
                : current,
            );
            this.replaceThreadSummaryFromSelected();
            this.cancelEditThread();
          }
          this.finishMutation(mutation);
        },
        error: (error: unknown) => {
          if (this.isSelectionCurrent(selection)) this.setActionError(error);
          this.finishMutation(mutation);
        },
      });
  }

  protected deleteThread(thread: DiscussionThread): void {
    if (thread.status !== 'Published' || !thread.canDelete) return;
    const mutation = this.beginMutation();
    if (mutation === null) return;
    const scope = this.scope;
    const selection = this.captureSelection(scope, thread.id);
    this.actionError.set(null);
    this.api
      .deleteDiscussionThread(scope.enrollmentId, scope.lessonId, thread.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (this.isScopeCurrent(scope)) {
            const tombstone = removedThread(thread);
            this.replaceThreadSummary(tombstone);
            if (this.isSelectionCurrent(selection)) {
              this.selectedThread.set(tombstone);
              this.confirmDeleteThread.set(false);
              this.resetThreadInteraction();
            }
          }
          this.finishMutation(mutation);
        },
        error: (error: unknown) => {
          if (this.isSelectionCurrent(selection)) this.setActionError(error);
          this.finishMutation(mutation);
        },
      });
  }

  protected replyTo(comment: DiscussionComment): void {
    if (comment.status !== 'Published' || comment.depth >= 2) return;
    this.replyParent.set(comment);
  }

  protected cancelReply(): void {
    this.replyParent.set(null);
  }

  protected createComment(thread: DiscussionThread): void {
    const body = this.newCommentBody.trim();
    if (thread.status !== 'Published' || !body) return;
    const mutation = this.beginMutation();
    if (mutation === null) return;
    const selection = this.captureSelection(this.scope, thread.id);
    const parent = this.replyParent();
    const parentCommentId = parent?.id ?? null;
    const signature = payloadSignature(
      'comment',
      selection.enrollmentId,
      selection.lessonId,
      thread.id,
      body,
      parentCommentId,
    );
    const key = this.commentIdempotencyKey(signature);
    this.actionError.set(null);
    this.api
      .createDiscussionComment(
        selection.enrollmentId,
        selection.lessonId,
        thread.id,
        body,
        parentCommentId,
        key,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (comment) => {
          this.clearCommentKey(signature, key);
          if (this.isSelectionCurrent(selection)) {
            const draftStillMatches =
              this.newCommentBody.trim() === body &&
              (this.replyParent()?.id ?? null) === parentCommentId;
            if (draftStillMatches) {
              this.newCommentBody = '';
              this.replyParent.set(null);
            }
            this.selectedThread.update((current) => {
              if (current?.id !== thread.id) return current;
              const exists = current.comments.items.some((item) => item.id === comment.id);
              return {
                ...current,
                commentCount: current.commentCount + (exists ? 0 : 1),
                comments: appendComment(current.comments, comment),
              };
            });
            this.replaceThreadSummaryFromSelected();
          }
          this.finishMutation(mutation);
        },
        error: (error: unknown) => {
          if (!isRequestTimeout(error)) this.clearCommentKey(signature, key);
          if (this.isSelectionCurrent(selection)) this.setActionError(error);
          this.finishMutation(mutation);
        },
      });
  }

  protected toggleLike(comment: DiscussionComment): void {
    if (comment.status !== 'Published' || this.isLikePending(comment.id)) return;
    const selection = this.captureSelection(this.scope, comment.threadId);
    this.addPendingLike(comment.id);
    this.actionError.set(null);
    const subscription = this.api
      .setDiscussionCommentLike(
        selection.enrollmentId,
        selection.lessonId,
        comment.threadId,
        comment.id,
        !comment.likedByViewer,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          if (this.isSelectionCurrent(selection)) {
            this.updateComment(comment.id, (current) => ({
              ...current,
              likedByViewer: result.liked,
              likeCount: result.likeCount,
            }));
          }
          this.removePendingLike(comment.id);
        },
        error: (error: unknown) => {
          this.removePendingLike(comment.id);
          if (this.isSelectionCurrent(selection)) this.setActionError(error);
        },
        complete: () => {
          this.removePendingLike(comment.id);
          this.likeSubscriptions.delete(comment.id);
        },
      });
    if (!subscription.closed) this.likeSubscriptions.set(comment.id, subscription);
  }

  protected isLikePending(commentId: string): boolean {
    return this.pendingLikeIds().has(commentId);
  }

  protected beginEditComment(comment: DiscussionComment): void {
    if (comment.status !== 'Published' || !comment.canEdit) return;
    this.editingCommentId.set(comment.id);
    this.editCommentBody = comment.body;
  }

  protected cancelEditComment(): void {
    this.editingCommentId.set(null);
    this.editCommentBody = '';
  }

  protected saveComment(comment: DiscussionComment): void {
    const body = this.editCommentBody.trim();
    if (comment.status !== 'Published' || !comment.canEdit || !body) return;
    const mutation = this.beginMutation();
    if (mutation === null) return;
    const selection = this.captureSelection(this.scope, comment.threadId);
    this.actionError.set(null);
    this.api
      .updateDiscussionComment(
        selection.enrollmentId,
        selection.lessonId,
        comment.threadId,
        comment.id,
        body,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          if (this.isSelectionCurrent(selection)) {
            this.updateComment(updated.id, () => updated);
            this.cancelEditComment();
          }
          this.finishMutation(mutation);
        },
        error: (error: unknown) => {
          if (this.isSelectionCurrent(selection)) this.setActionError(error);
          this.finishMutation(mutation);
        },
      });
  }

  protected deleteComment(comment: DiscussionComment): void {
    if (comment.status !== 'Published' || !comment.canDelete) return;
    const mutation = this.beginMutation();
    if (mutation === null) return;
    const selection = this.captureSelection(this.scope, comment.threadId);
    this.actionError.set(null);
    this.api
      .deleteDiscussionComment(
        selection.enrollmentId,
        selection.lessonId,
        comment.threadId,
        comment.id,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          if (this.isSelectionCurrent(selection)) {
            this.selectedThread.update((current) =>
              current?.id === comment.threadId
                ? {
                    ...current,
                    commentCount: Math.max(0, current.commentCount - 1),
                    comments: {
                      ...current.comments,
                      items: current.comments.items.map((item) =>
                        item.id === comment.id ? removedComment(item) : item,
                      ),
                    },
                  }
                : current,
            );
            this.replaceThreadSummaryFromSelected();
            this.confirmDeleteCommentId.set(null);
          }
          this.finishMutation(mutation);
        },
        error: (error: unknown) => {
          if (this.isSelectionCurrent(selection)) this.setActionError(error);
          this.finishMutation(mutation);
        },
      });
  }

  protected loadMoreComments(thread: DiscussionThread): void {
    const cursor = thread.comments.nextCursor;
    if (!cursor || this.loadingComments()) return;
    const selection = this.captureSelection(this.scope, thread.id);
    this.loadingComments.set(true);
    this.commentPageSubscription?.unsubscribe();
    this.commentPageSubscription = this.api
      .getDiscussionThread(selection.enrollmentId, selection.lessonId, thread.id, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          if (!this.isSelectionCurrent(selection)) return;
          this.selectedThread.update((current) =>
            current?.id === thread.id
              ? {
                  ...current,
                  commentCount: updated.commentCount,
                  comments: {
                    items: mergeUniqueById(current.comments.items, updated.comments.items),
                    nextCursor: updated.comments.nextCursor,
                    hasMore: updated.comments.hasMore,
                  },
                }
              : current,
          );
          this.replaceThreadSummaryFromSelected();
          this.loadingComments.set(false);
        },
        error: (error: unknown) => {
          if (!this.isSelectionCurrent(selection)) return;
          this.loadingComments.set(false);
          this.setActionError(error);
        },
      });
  }

  protected formattedDate(value: string): string {
    const date = new Date(value);
    if (Number.isNaN(date.valueOf())) return '';
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium' }).format(date);
  }

  protected replyCountLabel(count: number): string {
    if (this.locale.locale() === 'ar') {
      if (count === 0) return 'لا ردود';
      if (count === 1) return 'رد واحد';
      if (count === 2) return 'ردّان';
      return `${String(count)} ردود`;
    }
    return `${String(count)} ${count === 1 ? 'reply' : 'replies'}`;
  }

  protected threadAriaLabel(thread: DiscussionThreadSummary): string {
    const title =
      thread.status === 'Published' ? thread.title : this.tombstoneLabel('thread', thread.status);
    return `${title}. ${this.replyCountLabel(thread.commentCount)}`;
  }

  protected likeLabel(comment: DiscussionComment): string {
    if (this.locale.locale() === 'ar') {
      return comment.likedByViewer
        ? `إلغاء الإعجاب بالرد. ${String(comment.likeCount)} إعجاب`
        : `الإعجاب بالرد. ${String(comment.likeCount)} إعجاب`;
    }
    return comment.likedByViewer
      ? `Unlike reply. ${String(comment.likeCount)} likes`
      : `Like reply. ${String(comment.likeCount)} likes`;
  }

  protected statusLabel(status: DiscussionStatus): string {
    if (this.locale.locale() === 'ar') return status === 'Hidden' ? 'مخفي' : 'محذوف';
    return status === 'Hidden' ? 'Hidden' : 'Removed';
  }

  protected tombstoneLabel(
    entity: 'thread' | 'comment',
    status: Exclude<DiscussionStatus, 'Published'>,
  ): string {
    if (this.locale.locale() === 'ar') {
      if (entity === 'thread') {
        return status === 'Hidden' ? 'أُخفي هذا النقاش.' : 'حُذف هذا النقاش.';
      }
      return status === 'Hidden' ? 'أُخفي هذا الرد.' : 'حُذف هذا الرد.';
    }
    if (entity === 'thread') {
      return status === 'Hidden' ? 'This discussion was hidden.' : 'This discussion was removed.';
    }
    return status === 'Hidden' ? 'This reply was hidden.' : 'This reply was removed.';
  }

  protected problemLabel(code: string | null): string {
    if (this.locale.locale() === 'ar') {
      return code === 'DISCUSSION.NOT_FOUND'
        ? 'لم يعد هذا النقاش متاحاً.'
        : code === 'COMMENT.DEPTH_LIMIT'
          ? 'يمكن أن يصل الرد إلى مستويين متداخلين فقط.'
          : code === 'HTTP.408'
            ? 'انتهت مهلة الطلب. أعد المحاولة بأمان.'
            : 'تعذر تنفيذ العملية. حاول مرة أخرى.';
    }
    return code === 'DISCUSSION.NOT_FOUND'
      ? 'This discussion is no longer available.'
      : code === 'COMMENT.DEPTH_LIMIT'
        ? 'Replies can be nested at most two levels deep.'
        : code === 'HTTP.408'
          ? 'The request timed out. It is safe to retry.'
          : 'The action could not be completed. Try again.';
  }

  private activateScope(enrollmentId: string, lessonId: string | null, resetDrafts: boolean): void {
    this.scopeVersion += 1;
    this.scope = { version: this.scopeVersion, enrollmentId, lessonId };
    this.selectionVersion += 1;
    this.mutationVersion += 1;
    this.cancelAllReads();
    this.cancelLikeRequests();
    this.state.set({ status: 'loading', page: null, errorCode: null });
    this.selectedThread.set(null);
    this.selectedThreadId.set(null);
    this.loadingThread.set(false);
    this.busy.set(false);
    this.actionError.set(null);
    this.resetThreadInteraction();
    if (resetDrafts) {
      this.newThreadTitle = '';
      this.newThreadBody = '';
    }

    if (!this.connectivity.isOnline()) {
      this.state.set({ status: 'offline', page: null, errorCode: null });
      return;
    }
    this.loadThreads(this.scope);
  }

  private loadThreads(scope: DiscussionScope): void {
    this.threadListSubscription = this.api
      .getDiscussionThreads(scope.enrollmentId, scope.lessonId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (!this.isScopeCurrent(scope)) return;
          const normalizedPage = {
            ...page,
            items: mergeUniqueById([], page.items),
          };
          this.state.set({
            status: normalizedPage.items.length ? 'success' : 'empty',
            page: normalizedPage,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          if (!this.isScopeCurrent(scope)) return;
          if (error instanceof ApiProblem && error.status === 0) {
            this.state.set({ status: 'offline', page: null, errorCode: null });
            return;
          }
          this.state.set({
            status: 'error',
            page: null,
            errorCode: error instanceof ApiProblem ? error.code : 'DISCUSSION.LOAD_FAILED',
          });
        },
      });
  }

  private prependThread(thread: DiscussionThread): void {
    const summary = toSummary(thread);
    this.state.update((current) => {
      if (!current.page) {
        return {
          status: 'success',
          page: { items: [summary], nextCursor: null, hasMore: false },
          errorCode: null,
        };
      }
      return {
        status: 'success',
        page: {
          ...current.page,
          items: [summary, ...current.page.items.filter((item) => item.id !== summary.id)],
        },
        errorCode: null,
      };
    });
  }

  private replaceThreadSummary(thread: DiscussionThread): void {
    const summary = toSummary(thread);
    this.state.update((current) => {
      if (!current.page) return current;
      return {
        status: 'success',
        page: {
          ...current.page,
          items: current.page.items.map((item) => (item.id === summary.id ? summary : item)),
        },
        errorCode: null,
      };
    });
  }

  private replaceThreadSummaryFromSelected(): void {
    const thread = this.selectedThread();
    if (thread) this.replaceThreadSummary(thread);
  }

  private updateComment(
    commentId: string,
    update: (comment: DiscussionComment) => DiscussionComment,
  ): void {
    this.selectedThread.update((thread) =>
      thread
        ? {
            ...thread,
            comments: {
              ...thread.comments,
              items: thread.comments.items.map((comment) =>
                comment.id === commentId ? update(comment) : comment,
              ),
            },
          }
        : thread,
    );
  }

  private resetThreadInteraction(): void {
    this.replyParent.set(null);
    this.newCommentBody = '';
    this.editingThread.set(false);
    this.editingCommentId.set(null);
    this.confirmDeleteThread.set(false);
    this.confirmDeleteCommentId.set(null);
    this.editThreadTitle = '';
    this.editThreadBody = '';
    this.editCommentBody = '';
  }

  private beginMutation(): number | null {
    if (this.busy()) return null;
    this.mutationVersion += 1;
    this.busy.set(true);
    return this.mutationVersion;
  }

  private finishMutation(version: number): void {
    if (this.mutationVersion === version) this.busy.set(false);
  }

  private captureSelection(scope: DiscussionScope, threadId: string): ThreadSelectionScope {
    return { ...scope, selectionVersion: this.selectionVersion, threadId };
  }

  private isScopeCurrent(scope: DiscussionScope): boolean {
    return this.scope.version === scope.version;
  }

  private isSelectionCurrent(selection: ThreadSelectionScope): boolean {
    return (
      this.isScopeCurrent(selection) &&
      this.selectionVersion === selection.selectionVersion &&
      this.selectedThreadId() === selection.threadId
    );
  }

  private threadIdempotencyKey(signature: string): string {
    const pending = this.pendingThreadKeys.get(signature);
    if (pending) return pending;
    const key = globalThis.crypto.randomUUID();
    this.pendingThreadKeys.set(signature, key);
    return key;
  }

  private commentIdempotencyKey(signature: string): string {
    const pending = this.pendingCommentKeys.get(signature);
    if (pending) return pending;
    const key = globalThis.crypto.randomUUID();
    this.pendingCommentKeys.set(signature, key);
    return key;
  }

  private clearThreadKey(signature: string, key: string): void {
    if (this.pendingThreadKeys.get(signature) === key) this.pendingThreadKeys.delete(signature);
  }

  private clearCommentKey(signature: string, key: string): void {
    if (this.pendingCommentKeys.get(signature) === key) this.pendingCommentKeys.delete(signature);
  }

  private addPendingLike(commentId: string): void {
    this.pendingLikeIds.update((current) => new Set([...current, commentId]));
  }

  private removePendingLike(commentId: string): void {
    this.pendingLikeIds.update((current) => {
      if (!current.has(commentId)) return current;
      const next = new Set(current);
      next.delete(commentId);
      return next;
    });
  }

  private cancelLikeRequests(): void {
    for (const subscription of this.likeSubscriptions.values()) subscription.unsubscribe();
    this.likeSubscriptions.clear();
    this.pendingLikeIds.set(new Set<string>());
  }

  private cancelThreadReads(): void {
    this.threadDetailSubscription?.unsubscribe();
    this.commentPageSubscription?.unsubscribe();
    this.threadDetailSubscription = null;
    this.commentPageSubscription = null;
    this.loadingThread.set(false);
    this.loadingComments.set(false);
  }

  private cancelAllReads(): void {
    this.threadListSubscription?.unsubscribe();
    this.threadPageSubscription?.unsubscribe();
    this.threadListSubscription = null;
    this.threadPageSubscription = null;
    this.loadingMore.set(false);
    this.cancelThreadReads();
  }

  private setActionError(error: unknown): void {
    this.actionError.set(error instanceof ApiProblem ? error.code : 'DISCUSSION.ACTION_FAILED');
  }
}

const toSummary = (thread: DiscussionThread): DiscussionThreadSummary => ({
  id: thread.id,
  lessonId: thread.lessonId,
  authorUserId: thread.authorUserId,
  authorName: thread.authorName,
  title: thread.title,
  body: thread.body,
  status: thread.status,
  isEdited: thread.isEdited,
  createdAt: thread.createdAt,
  updatedAt: thread.updatedAt,
  commentCount: thread.commentCount,
  canEdit: thread.canEdit,
  canDelete: thread.canDelete,
});

const normalizeThread = (thread: DiscussionThread): DiscussionThread => ({
  ...thread,
  comments: {
    ...thread.comments,
    items: mergeUniqueById([], thread.comments.items),
  },
});

const appendComment = (
  page: DiscussionCommentPage,
  comment: DiscussionComment,
): DiscussionCommentPage => ({
  ...page,
  items: mergeUniqueById(page.items, [comment]),
});

const mergeUniqueById = <T extends { readonly id: string }>(
  current: readonly T[],
  incoming: readonly T[],
): readonly T[] => {
  const ids = new Set<string>();
  const items: T[] = [];
  for (const item of [...current, ...incoming]) {
    if (ids.has(item.id)) continue;
    ids.add(item.id);
    items.push(item);
  }
  return items;
};

const removedThread = (thread: DiscussionThread): DiscussionThread => ({
  ...thread,
  authorUserId: '',
  authorName: '',
  title: '',
  body: '',
  status: 'Removed',
  isEdited: false,
  canEdit: false,
  canDelete: false,
});

const removedComment = (comment: DiscussionComment): DiscussionComment => ({
  ...comment,
  authorUserId: '',
  authorName: '',
  body: '',
  status: 'Removed',
  isEdited: false,
  likeCount: 0,
  likedByViewer: false,
  canEdit: false,
  canDelete: false,
});

const payloadSignature = (...parts: readonly unknown[]): string => JSON.stringify(parts);

const isRequestTimeout = (error: unknown): boolean =>
  error instanceof ApiProblem && error.status === 408;
