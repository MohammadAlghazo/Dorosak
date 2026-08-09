import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  catchError,
  distinctUntilChanged,
  forkJoin,
  map,
  of,
  switchMap,
  type Observable,
} from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import {
  DirectStorageHttpClient,
  type DirectStorageUploadEvent,
} from '../../core/api/direct-storage-http.client';
import { LearningApiClient } from '../../core/api/learning-api.client';
import type {
  AssignmentSubmission,
  AssignmentSubmissionFile,
  LearningLesson,
  LearningManifest,
  LearningMediaVariant,
  LearningNote,
  QuizAnswerInput,
  QuizAttempt,
  WatchedInterval,
} from '../../core/api/learning-api.types';
import { MediaApiClient } from '../../core/api/media-api.client';
import type {
  CompletedUploadPart,
  MediaUploadEvent,
  UploadSession,
} from '../../core/api/media-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import {
  MediaUploadHasher,
  type MediaFileHashes,
} from '../media-upload/media-upload-hasher.service';

type WorkspaceState =
  | { status: 'loading' | 'offline'; manifest: null; lesson: null; errorCode: null }
  | {
      status: 'success';
      manifest: LearningManifest;
      lesson: LearningLesson | null;
      errorCode: null;
    }
  | { status: 'error'; manifest: null; lesson: null; errorCode: string | null };

@Component({
  selector: 'drs-learning-page',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="workspace" aria-live="polite" [attr.aria-busy]="state().status === 'loading'">
      @switch (state().status) {
        @case ('loading') {
          <div class="workspace-state" role="status">
            <span>08</span>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'جار تجهيز الإصدار المثبت…'
                  : 'Preparing your pinned release…'
              }}
            </p>
          </div>
        }
        @case ('offline') {
          <div class="workspace-state" role="alert">
            <span>OFFLINE</span>
            <h1>
              {{
                locale.locale() === 'ar'
                  ? 'مساحة الدرس غير متاحة دون اتصال'
                  : 'Lesson workspace is unavailable offline'
              }}
            </h1>
            <button type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="workspace-state" role="alert">
            <span>{{ state().errorCode ?? 'LEARNING.LOAD_FAILED' }}</span>
            <h1>
              {{
                locale.locale() === 'ar' ? 'تعذر فتح هذا الدرس' : 'This lesson could not be opened'
              }}
            </h1>
            <button type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('success') {
          @if (state().manifest; as manifest) {
            <header class="course-header">
              <div>
                <p>
                  {{ locale.locale() === 'ar' ? 'إصدار مثبت' : 'Pinned release' }} ·
                  {{ manifest.releaseId.slice(0, 8) }}
                </p>
                <h1>{{ manifest.title }}</h1>
              </div>
              <a [routerLink]="['/', locale.locale(), 'my-learning']">{{
                locale.locale() === 'ar' ? 'كل مساراتي' : 'All pathways'
              }}</a>
            </header>

            <div class="learning-grid">
              <aside>
                <div class="progress-meter" [style.--progress]="completionPercent(manifest) + '%'">
                  <strong>{{ completionPercent(manifest) }}%</strong>
                  <span>{{ locale.locale() === 'ar' ? 'مكتمل' : 'complete' }}</span>
                </div>
                <nav
                  [attr.aria-label]="locale.locale() === 'ar' ? 'منهج المسار' : 'Course curriculum'"
                >
                  @for (section of manifest.sections; track section.id; let sectionIndex = $index) {
                    <section>
                      <h2>
                        <span>{{ String(sectionIndex + 1).padStart(2, '0') }}</span
                        >{{ section.title }}
                      </h2>
                      @for (item of section.lessons; track item.id) {
                        <a
                          [routerLink]="[
                            '/',
                            locale.locale(),
                            'learn',
                            manifest.enrollmentId,
                            'lessons',
                            item.id,
                          ]"
                          [class.active]="item.id === state().lesson?.id"
                          [class.complete]="item.isCompleted"
                          [attr.aria-current]="item.id === state().lesson?.id ? 'page' : null"
                        >
                          <span>{{ item.isCompleted ? '✓' : lessonGlyph(item.lessonType) }}</span>
                          {{ item.title }}
                        </a>
                      }
                    </section>
                  }
                </nav>
              </aside>

              <main>
                @if (state().lesson; as lesson) {
                  <header class="lesson-heading">
                    <div>
                      <p>{{ lesson.lessonType }}</p>
                      <h2>{{ lesson.title }}</h2>
                    </div>
                    <button
                      type="button"
                      class="bookmark"
                      [class.active]="bookmarked()"
                      (click)="toggleBookmark()"
                      [disabled]="savingBookmark()"
                    >
                      {{ bookmarked() ? '★' : '☆' }}
                      <span>{{ locale.locale() === 'ar' ? 'حفظ' : 'Bookmark' }}</span>
                    </button>
                  </header>

                  @if (lesson.lessonType === 'Video' && lesson.mediaVariants.length) {
                    <section class="media-stage" aria-labelledby="media-heading">
                      <h3 id="media-heading" class="visually-hidden">
                        {{ locale.locale() === 'ar' ? 'مشغل الفيديو' : 'Video player' }}
                      </h3>
                      @if (mediaUrl()) {
                        <video
                          controls
                          playsinline
                          preload="metadata"
                          [src]="mediaUrl()"
                          [attr.poster]="null"
                          (timeupdate)="trackVideo($event)"
                          (pause)="saveVideoProgress($event, false)"
                          (ended)="saveVideoProgress($event, true)"
                        ></video>
                      } @else {
                        <div class="media-gate">
                          <span>PRIVATE MEDIA</span>
                          <p>
                            {{
                              locale.locale() === 'ar'
                                ? 'أنشئ رابط مشاهدة قصير العمر لهذا الجهاز.'
                                : 'Create a short-lived playback grant for this device.'
                            }}
                          </p>
                          <button
                            type="button"
                            (click)="
                              openVariant(selectedVariant() ?? firstVariant(lesson.mediaVariants))
                            "
                            [disabled]="grantingMedia()"
                          >
                            {{
                              grantingMedia()
                                ? locale.locale() === 'ar'
                                  ? 'جار المنح…'
                                  : 'Granting…'
                                : locale.locale() === 'ar'
                                  ? 'بدء المشاهدة'
                                  : 'Start watching'
                            }}
                          </button>
                        </div>
                      }
                      <footer>
                        <label
                          >{{ locale.locale() === 'ar' ? 'الجودة' : 'Quality' }}
                          <select
                            [ngModel]="
                              selectedVariant()?.variantId ??
                              firstVariant(lesson.mediaVariants).variantId
                            "
                            (ngModelChange)="selectVariant($event, lesson.mediaVariants)"
                          >
                            @for (variant of lesson.mediaVariants; track variant.variantId) {
                              <option [value]="variant.variantId">
                                {{ variantLabel(variant) }}
                              </option>
                            }
                          </select>
                        </label>
                        @if (lesson.captions.length) {
                          <span
                            >{{ lesson.captions.length }}
                            {{ locale.locale() === 'ar' ? 'مسار ترجمة' : 'caption tracks' }}</span
                          >
                        }
                      </footer>
                    </section>
                  } @else {
                    <article class="lesson-content">
                      <p>{{ lesson.content }}</p>
                      @if (lesson.lessonType === 'Article' || lesson.lessonType === 'Document') {
                        <button
                          type="button"
                          (click)="completeReading()"
                          [disabled]="savingProgress() || lesson.isCompleted"
                        >
                          {{
                            lesson.isCompleted
                              ? locale.locale() === 'ar'
                                ? 'اكتمل الدرس'
                                : 'Lesson complete'
                              : locale.locale() === 'ar'
                                ? 'أنهيت القراءة'
                                : 'Mark as read'
                          }}
                        </button>
                      }
                    </article>
                  }

                  @if (lesson.quizVersionId) {
                    <section class="assessment" aria-labelledby="quiz-title">
                      <p class="section-label">CHECKPOINT / QUIZ</p>
                      <h3 id="quiz-title">
                        {{ locale.locale() === 'ar' ? 'اختبر فهمك' : 'Check your understanding' }}
                      </h3>
                      @if (!quizAttempt()) {
                        <button
                          type="button"
                          (click)="startQuiz(lesson.quizVersionId)"
                          [disabled]="assessmentBusy()"
                        >
                          {{ locale.locale() === 'ar' ? 'ابدأ المحاولة' : 'Start attempt' }}
                        </button>
                      } @else if (quizAttempt(); as attempt) {
                        @if (attempt.status === 'InProgress') {
                          <form (ngSubmit)="submitQuiz(attempt)">
                            @for (
                              question of attempt.questions;
                              track question.id;
                              let questionIndex = $index
                            ) {
                              <fieldset>
                                <legend>
                                  <span>{{ questionIndex + 1 }}</span
                                  >{{ question.prompt }}
                                </legend>
                                @if (question.type === 'ShortAnswer') {
                                  <textarea
                                    rows="4"
                                    [value]="textAnswer(question.id)"
                                    (input)="setTextAnswer(question.id, $event)"
                                    maxlength="10000"
                                  ></textarea>
                                } @else {
                                  @for (option of question.options; track option.id) {
                                    <label class="answer-option"
                                      ><input
                                        [type]="
                                          question.type === 'MultipleChoice' ? 'checkbox' : 'radio'
                                        "
                                        [name]="question.id"
                                        [checked]="optionSelected(question.id, option.id)"
                                        (change)="
                                          setOptionAnswer(
                                            question.id,
                                            option.id,
                                            question.type === 'MultipleChoice',
                                            $event
                                          )
                                        "
                                      />
                                      <span>{{ option.text }}</span></label
                                    >
                                  }
                                }
                              </fieldset>
                            }
                            <button type="submit" [disabled]="assessmentBusy()">
                              {{ locale.locale() === 'ar' ? 'سلّم الإجابات' : 'Submit answers' }}
                            </button>
                          </form>
                        } @else {
                          <div class="result" [class.passed]="attempt.passed">
                            <strong>{{ attempt.score ?? '—' }}%</strong
                            ><span>{{
                              attempt.status === 'PendingManualGrade'
                                ? locale.locale() === 'ar'
                                  ? 'بانتظار التصحيح اليدوي'
                                  : 'Awaiting manual grade'
                                : attempt.passed
                                  ? locale.locale() === 'ar'
                                    ? 'اجتزت الاختبار'
                                    : 'Quiz passed'
                                  : locale.locale() === 'ar'
                                    ? 'يمكنك المحاولة مجدداً'
                                    : 'Another attempt may be available'
                            }}</span>
                          </div>
                        }
                      }
                    </section>
                  }

                  @if (lesson.assignmentVersionId) {
                    <section class="assessment" aria-labelledby="assignment-title">
                      <p class="section-label">CHECKPOINT / ASSIGNMENT</p>
                      <h3 id="assignment-title">
                        {{ locale.locale() === 'ar' ? 'تسليم الواجب' : 'Assignment submission' }}
                      </h3>
                      <p>
                        {{
                          locale.locale() === 'ar'
                            ? 'أرسل الإجابة أولاً، ثم أرفق حتى خمسة ملفات PDF. لا يصبح الملف متاحاً قبل الفحص.'
                            : 'Submit the response first, then attach up to five PDFs. Files remain private until scanned.'
                        }}
                      </p>
                      <textarea
                        [(ngModel)]="assignmentText"
                        rows="8"
                        maxlength="100000"
                        [placeholder]="
                          locale.locale() === 'ar' ? 'اكتب إجابتك…' : 'Write your response…'
                        "
                      ></textarea>
                      <button
                        type="button"
                        (click)="submitAssignment(lesson.assignmentVersionId)"
                        [disabled]="assessmentBusy() || assignmentText.trim().length === 0"
                      >
                        {{ locale.locale() === 'ar' ? 'سلّم المهمة' : 'Submit assignment' }}
                      </button>
                      @if (assignmentStatus()) {
                        <p class="success-note" role="status">{{ assignmentStatus() }}</p>
                      }
                      @if (assignmentSubmission(); as submission) {
                        <div class="assignment-files">
                          <label for="assignment-pdf">{{
                            locale.locale() === 'ar' ? 'إرفاق ملف PDF' : 'Attach a PDF'
                          }}</label>
                          <input
                            id="assignment-pdf"
                            type="file"
                            accept="application/pdf,.pdf"
                            (change)="chooseAssignmentFile($event, submission)"
                            [disabled]="assignmentUploadBusy() || submission.files.length >= 5"
                          />
                          @if (assignmentUploadStatus()) {
                            <p role="status">{{ assignmentUploadStatus() }}</p>
                          }
                          @if (submission.files.length) {
                            <ul>
                              @for (file of submission.files; track file.id) {
                                <li>
                                  <span
                                    ><strong>{{ file.fileName }}</strong
                                    ><small>{{ file.state }}</small></span
                                  >
                                  @if (file.state === 'Ready') {
                                    <button type="button" (click)="downloadAssignmentFile(file)">
                                      {{ locale.locale() === 'ar' ? 'تنزيل' : 'Download' }}
                                    </button>
                                  }
                                </li>
                              }
                            </ul>
                          }
                        </div>
                      }
                    </section>
                  }

                  <section class="notes" aria-labelledby="notes-title">
                    <header>
                      <div>
                        <p class="section-label">PRIVATE NOTES</p>
                        <h3 id="notes-title">
                          {{ locale.locale() === 'ar' ? 'ملاحظاتي' : 'My notes' }}
                        </h3>
                      </div>
                      <span>{{ notes().length }}</span>
                    </header>
                    <div class="note-composer">
                      <textarea
                        [(ngModel)]="noteText"
                        rows="3"
                        maxlength="5000"
                        [placeholder]="
                          locale.locale() === 'ar'
                            ? 'ملاحظة خاصة بك فقط…'
                            : 'A private note only you can see…'
                        "
                      ></textarea
                      ><button
                        type="button"
                        (click)="saveNote()"
                        [disabled]="savingNote() || noteText.trim().length === 0"
                      >
                        {{ locale.locale() === 'ar' ? 'أضف' : 'Add' }}
                      </button>
                    </div>
                    @for (note of notes(); track note.id) {
                      <article>
                        <p>{{ note.text }}</p>
                        <button type="button" (click)="deleteNote(note)">
                          {{ locale.locale() === 'ar' ? 'حذف' : 'Delete' }}
                        </button>
                      </article>
                    }
                  </section>

                  <nav
                    class="lesson-navigation"
                    [attr.aria-label]="
                      locale.locale() === 'ar' ? 'التنقل بين الدروس' : 'Lesson navigation'
                    "
                  >
                    @if (adjacentLesson(manifest, lesson.id, -1); as previous) {
                      <a
                        [routerLink]="[
                          '/',
                          locale.locale(),
                          'learn',
                          manifest.enrollmentId,
                          'lessons',
                          previous.id,
                        ]"
                        >← {{ previous.title }}</a
                      >
                    }
                    @if (adjacentLesson(manifest, lesson.id, 1); as next) {
                      <a
                        [routerLink]="[
                          '/',
                          locale.locale(),
                          'learn',
                          manifest.enrollmentId,
                          'lessons',
                          next.id,
                        ]"
                        >{{ next.title }} →</a
                      >
                    }
                  </nav>
                } @else {
                  <div class="workspace-state">
                    <span>00</span>
                    <h2>
                      {{
                        locale.locale() === 'ar'
                          ? 'لا توجد دروس في هذا الإصدار'
                          : 'This release has no lessons'
                      }}
                    </h2>
                  </div>
                }
              </main>
            </div>
          }
        }
      }
      @if (actionError()) {
        <div class="action-error" role="alert">
          <code>{{ actionError() }}</code
          ><button type="button" (click)="actionError.set(null)">×</button>
        </div>
      }
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .workspace {
      max-inline-size: 100rem;
      margin-inline: auto;
    }
    .course-header {
      display: flex;
      justify-content: space-between;
      align-items: end;
      gap: var(--space-5);
      padding: var(--space-4) 0 var(--space-6);
      border-block-end: 1px solid #253247;
    }
    .course-header p,
    .lesson-heading p,
    .section-label {
      margin: 0;
      color: #5eead4;
      font-size: 0.72rem;
      font-weight: 750;
      letter-spacing: 0.12em;
    }
    .course-header h1 {
      margin: var(--space-2) 0 0;
      font-size: clamp(2rem, 5vw, 4.5rem);
      line-height: 0.95;
    }
    .course-header a {
      color: #99f6e4;
    }
    .learning-grid {
      display: grid;
      grid-template-columns: minmax(17rem, 22rem) minmax(0, 1fr);
      min-block-size: calc(100dvh - 10rem);
    }
    aside {
      padding: var(--space-5) var(--space-5) var(--space-8) 0;
      border-inline-end: 1px solid #253247;
    }
    .progress-meter {
      display: grid;
      grid-template-columns: auto 1fr;
      align-items: baseline;
      gap: var(--space-3);
      padding-block-end: var(--space-5);
      border-block-end: 4px solid #1e293b;
      background: linear-gradient(90deg, #2dd4bf var(--progress), transparent 0) bottom left / 100%
        4px no-repeat;
    }
    .progress-meter strong {
      font: 700 2rem/1 monospace;
    }
    .progress-meter span {
      color: #94a3b8;
    }
    aside section {
      margin-block-start: var(--space-6);
    }
    aside h2 {
      display: flex;
      gap: var(--space-3);
      font-size: 0.9rem;
      text-transform: uppercase;
    }
    aside h2 span {
      color: #5eead4;
      font-family: monospace;
    }
    aside nav a {
      display: grid;
      grid-template-columns: 1.5rem 1fr;
      gap: var(--space-2);
      padding: 0.7rem;
      color: #cbd5e1;
      border-inline-start: 2px solid transparent;
      text-decoration: none;
    }
    aside nav a:hover,
    aside nav a.active {
      color: #fff;
      background: #111f32;
      border-inline-start-color: #5eead4;
    }
    aside nav a.complete > span {
      color: #5eead4;
    }
    main {
      min-inline-size: 0;
      padding: var(--space-6) 0 var(--space-10) clamp(var(--space-5), 5vw, var(--space-9));
    }
    .lesson-heading {
      display: flex;
      justify-content: space-between;
      align-items: start;
      gap: var(--space-5);
      margin-block-end: var(--space-5);
    }
    .lesson-heading h2 {
      margin: var(--space-2) 0;
      font-size: clamp(2rem, 5vw, 4rem);
    }
    .bookmark {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.7rem 1rem;
      color: #cbd5e1;
      background: transparent;
      border: 1px solid #334155;
    }
    .bookmark.active {
      color: #facc15;
      border-color: #facc15;
    }
    .media-stage {
      border: 1px solid #334155;
      background: #020617;
    }
    video {
      display: block;
      inline-size: 100%;
      max-block-size: 68dvh;
      background: #000;
    }
    .media-gate {
      display: grid;
      place-content: center;
      justify-items: center;
      min-block-size: 52dvh;
      padding: var(--space-6);
      text-align: center;
      background: radial-gradient(circle at 50% 35%, #17304a, #020617 65%);
    }
    .media-gate span {
      color: #5eead4;
      font: 700 0.75rem/1 monospace;
      letter-spacing: 0.15em;
    }
    .media-gate p {
      max-inline-size: 34rem;
      color: #94a3b8;
    }
    .media-gate button,
    .lesson-content button,
    .assessment > button,
    .assessment form > button,
    .assessment + button {
      min-block-size: 46px;
      padding-inline: var(--space-5);
      border: 0;
      color: #042f2e;
      background: #5eead4;
      font-weight: 750;
    }
    .media-stage footer {
      display: flex;
      justify-content: space-between;
      gap: var(--space-4);
      padding: var(--space-3) var(--space-4);
      color: #94a3b8;
    }
    .media-stage select {
      margin-inline-start: 0.5rem;
      color: #fff;
      background: #0f172a;
      border: 1px solid #475569;
    }
    .lesson-content {
      min-block-size: 24rem;
      padding: clamp(var(--space-5), 6vw, var(--space-9));
      color: #dbeafe;
      background: #0f1b2e;
      border: 1px solid #334155;
    }
    .lesson-content p {
      max-inline-size: 70ch;
      white-space: pre-wrap;
      font-size: 1.1rem;
      line-height: 1.9;
    }
    .assessment,
    .notes {
      margin-block-start: var(--space-7);
      padding: clamp(var(--space-5), 4vw, var(--space-7));
      border: 1px solid #334155;
      background: #0b1728;
    }
    .assessment h3,
    .notes h3 {
      margin: var(--space-2) 0 var(--space-5);
      font-size: clamp(1.6rem, 3vw, 2.6rem);
    }
    .assessment textarea,
    .note-composer textarea {
      inline-size: 100%;
      padding: var(--space-4);
      color: #f8fafc;
      background: #07101d;
      border: 1px solid #475569;
      resize: vertical;
    }
    fieldset {
      margin-block: var(--space-5);
      padding: var(--space-5);
      border: 1px solid #334155;
    }
    legend {
      display: flex;
      gap: var(--space-3);
      padding-inline: var(--space-2);
      font-weight: 700;
    }
    legend span {
      color: #5eead4;
      font-family: monospace;
    }
    .answer-option {
      display: flex;
      align-items: start;
      gap: var(--space-3);
      margin-block: var(--space-3);
      padding: var(--space-3);
      background: #111f32;
    }
    .answer-option input {
      margin-block-start: 0.2rem;
      accent-color: #2dd4bf;
    }
    .result {
      display: flex;
      align-items: center;
      gap: var(--space-5);
      padding: var(--space-5);
      border-inline-start: 4px solid #fb7185;
      background: #191827;
    }
    .result.passed {
      border-color: #5eead4;
      background: #0d2927;
    }
    .result strong {
      font: 700 2.5rem/1 monospace;
    }
    .notes header {
      display: flex;
      justify-content: space-between;
      align-items: start;
    }
    .notes header > span {
      color: #5eead4;
      font: 700 2rem/1 monospace;
    }
    .note-composer {
      display: grid;
      grid-template-columns: 1fr auto;
      align-items: end;
      gap: var(--space-3);
    }
    .note-composer button {
      min-block-size: 46px;
      padding-inline: var(--space-4);
      border: 0;
      background: #5eead4;
      color: #042f2e;
    }
    .notes article {
      display: flex;
      justify-content: space-between;
      gap: var(--space-4);
      margin-block-start: var(--space-3);
      padding: var(--space-4);
      background: #111f32;
    }
    .notes article p {
      margin: 0;
      white-space: pre-wrap;
    }
    .notes article button {
      color: #fda4af;
      background: transparent;
      border: 0;
    }
    .lesson-navigation {
      display: flex;
      justify-content: space-between;
      gap: var(--space-4);
      margin-block-start: var(--space-7);
    }
    .lesson-navigation a {
      max-inline-size: 45%;
      color: #99f6e4;
    }
    .workspace-state {
      display: grid;
      place-content: center;
      justify-items: start;
      min-block-size: 65dvh;
      gap: var(--space-4);
    }
    .workspace-state > span {
      color: #5eead4;
      font: 700 3rem/1 monospace;
    }
    .workspace-state h1 {
      max-inline-size: 20ch;
      font-size: clamp(2rem, 5vw, 4.5rem);
    }
    .workspace-state button {
      min-block-size: 46px;
      padding-inline: var(--space-5);
      color: #042f2e;
      background: #5eead4;
      border: 0;
    }
    .action-error {
      position: fixed;
      inset-inline-end: var(--space-5);
      inset-block-end: var(--space-5);
      display: flex;
      gap: var(--space-4);
      align-items: center;
      max-inline-size: min(90vw, 34rem);
      padding: var(--space-4);
      background: #4c0519;
      border: 1px solid #fb7185;
      z-index: 20;
    }
    .action-error button {
      color: #fff;
      background: transparent;
      border: 0;
      font-size: 1.4rem;
    }
    .success-note {
      color: #99f6e4;
    }
    .assignment-files {
      display: grid;
      gap: var(--space-3);
      margin-block-start: var(--space-5);
      padding-block-start: var(--space-5);
      border-block-start: 1px solid #334155;
    }
    .assignment-files ul {
      display: grid;
      gap: var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .assignment-files li {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: var(--space-3);
      padding: var(--space-3);
      background: #111f32;
    }
    .assignment-files li span {
      display: grid;
      min-inline-size: 0;
    }
    .assignment-files li strong {
      overflow-wrap: anywhere;
    }
    .assignment-files li small {
      color: #94a3b8;
    }
    .assignment-files li button {
      min-block-size: 44px;
      color: #99f6e4;
      background: transparent;
      border: 1px solid #2dd4bf;
    }
    @media (max-width: 900px) {
      .learning-grid {
        grid-template-columns: 1fr;
      }
      aside {
        border-inline-end: 0;
        border-block-end: 1px solid #253247;
        padding-inline-end: 0;
      }
      aside nav {
        display: grid;
        grid-template-columns: repeat(2, 1fr);
        gap: var(--space-3);
      }
      main {
        padding-inline-start: 0;
      }
    }
    @media (max-width: 620px) {
      .course-header,
      .lesson-heading,
      .media-stage footer {
        align-items: start;
        flex-direction: column;
      }
      aside nav {
        grid-template-columns: 1fr;
      }
      .note-composer {
        grid-template-columns: 1fr;
      }
      .lesson-navigation {
        flex-direction: column;
      }
      .lesson-navigation a {
        max-inline-size: 100%;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LearningPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly String = String;
  protected readonly actionError = signal<string | null>(null);
  protected readonly notes = signal<readonly LearningNote[]>([]);
  protected readonly quizAttempt = signal<QuizAttempt | null>(null);
  protected readonly selectedVariant = signal<LearningMediaVariant | null>(null);
  protected readonly mediaUrl = signal<string | null>(null);
  protected readonly bookmarked = signal(false);
  protected readonly savingBookmark = signal(false);
  protected readonly savingProgress = signal(false);
  protected readonly savingNote = signal(false);
  protected readonly grantingMedia = signal(false);
  protected readonly assessmentBusy = signal(false);
  protected readonly assignmentStatus = signal<string | null>(null);
  protected readonly assignmentSubmission = signal<AssignmentSubmission | null>(null);
  protected readonly assignmentUploadBusy = signal(false);
  protected readonly assignmentUploadStatus = signal<string | null>(null);
  protected readonly state = signal<WorkspaceState>({
    status: 'loading',
    manifest: null,
    lesson: null,
    errorCode: null,
  });
  protected noteText = '';
  protected assignmentText = '';
  private readonly api = inject(LearningApiClient);
  private readonly mediaApi = inject(MediaApiClient);
  private readonly directStorage = inject(DirectStorageHttpClient);
  private readonly mediaHasher = inject(MediaUploadHasher);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly answers = new Map<string, { text: string | null; options: Set<string> }>();
  private watchedIntervals: WatchedInterval[] = [];
  private sequence = Date.now();

  constructor() {
    this.bindRoute();
  }

  protected reload(): void {
    this.bindRoute();
  }

  protected completionPercent(manifest: LearningManifest): number {
    const lessons = manifest.sections.flatMap((section) => section.lessons);
    return lessons.length === 0
      ? 0
      : Math.round((lessons.filter((lesson) => lesson.isCompleted).length / lessons.length) * 100);
  }

  protected lessonGlyph(type: string): string {
    return (
      (
        { Video: '▶', Article: '¶', Document: '▤', Quiz: '?', Assignment: '✎' } as Record<
          string,
          string
        >
      )[type] ?? '·'
    );
  }

  protected adjacentLesson(manifest: LearningManifest, lessonId: string, offset: number) {
    const lessons = manifest.sections.flatMap((section) => section.lessons);
    const index = lessons.findIndex((lesson) => lesson.id === lessonId);
    return lessons[index + offset] ?? null;
  }

  protected variantLabel(variant: LearningMediaVariant): string {
    if (variant.height) return `${String(variant.height)}p`;
    return variant.kind;
  }

  protected firstVariant(variants: readonly LearningMediaVariant[]): LearningMediaVariant {
    const variant = variants[0];
    if (!variant) throw new Error('A video lesson requires a playback variant.');
    return variant;
  }

  protected selectVariant(variantId: string, variants: readonly LearningMediaVariant[]): void {
    const variant = variants.find((item) => item.variantId === variantId) ?? variants[0];
    if (!variant) return;
    this.selectedVariant.set(variant);
    this.mediaUrl.set(null);
  }

  protected openVariant(variant: LearningMediaVariant): void {
    if (this.grantingMedia()) return;
    this.selectedVariant.set(variant);
    this.grantingMedia.set(true);
    this.mediaApi
      .createDownloadGrant(variant.assetId, { variantId: variant.variantId, fileName: null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (grant) => {
          this.mediaUrl.set(grant.url);
          this.grantingMedia.set(false);
        },
        error: (error: unknown) => {
          this.grantingMedia.set(false);
          this.setActionError(error);
        },
      });
  }

  protected trackVideo(event: Event): void {
    const video = event.target as HTMLVideoElement;
    const end = Math.max(0, video.currentTime);
    if (end <= 0) return;
    this.watchedIntervals = mergeIntervals([
      ...this.watchedIntervals,
      { startSeconds: Math.max(0, end - 5), endSeconds: end },
    ]);
  }

  protected saveVideoProgress(event: Event, completionIntent: boolean): void {
    const video = event.target as HTMLVideoElement;
    this.updateProgress(video.currentTime, completionIntent);
  }

  protected completeReading(): void {
    this.updateProgress(0, true);
  }

  protected toggleBookmark(): void {
    const lesson = this.state().lesson;
    const manifest = this.state().manifest;
    if (!lesson || !manifest || this.savingBookmark()) return;
    this.savingBookmark.set(true);
    const request = this.bookmarked()
      ? this.api.deleteBookmark(manifest.enrollmentId, lesson.id)
      : this.api.addBookmark(manifest.enrollmentId, lesson.id);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.bookmarked.update((value) => !value);
        this.savingBookmark.set(false);
      },
      error: (error: unknown) => {
        this.savingBookmark.set(false);
        this.setActionError(error);
      },
    });
  }

  protected saveNote(): void {
    const lesson = this.state().lesson;
    const manifest = this.state().manifest;
    const text = this.noteText.trim();
    if (!lesson || !manifest || !text || this.savingNote()) return;
    this.savingNote.set(true);
    this.api
      .createNote(manifest.enrollmentId, lesson.id, text)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (note) => {
          this.notes.update((items) => [note, ...items]);
          this.noteText = '';
          this.savingNote.set(false);
        },
        error: (error: unknown) => {
          this.savingNote.set(false);
          this.setActionError(error);
        },
      });
  }

  protected deleteNote(note: LearningNote): void {
    this.api
      .deleteNote(note.enrollmentId, note.lessonId, note.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.notes.update((items) => items.filter((item) => item.id !== note.id));
        },
        error: (error: unknown) => {
          this.setActionError(error);
        },
      });
  }

  protected startQuiz(versionId: string): void {
    const manifest = this.state().manifest;
    if (!manifest || this.assessmentBusy()) return;
    this.assessmentBusy.set(true);
    this.api
      .startQuiz(manifest.enrollmentId, versionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (attempt) => {
          this.answers.clear();
          this.quizAttempt.set(attempt);
          this.assessmentBusy.set(false);
        },
        error: (error: unknown) => {
          this.assessmentBusy.set(false);
          this.setActionError(error);
        },
      });
  }

  protected textAnswer(questionId: string): string {
    return this.answers.get(questionId)?.text ?? '';
  }

  protected setTextAnswer(questionId: string, event: Event): void {
    const answer = this.answers.get(questionId) ?? { text: null, options: new Set<string>() };
    answer.text = (event.target as HTMLTextAreaElement).value;
    this.answers.set(questionId, answer);
  }

  protected optionSelected(questionId: string, optionId: string): boolean {
    return this.answers.get(questionId)?.options.has(optionId) ?? false;
  }

  protected setOptionAnswer(
    questionId: string,
    optionId: string,
    multiple: boolean,
    event: Event,
  ): void {
    const answer = this.answers.get(questionId) ?? { text: null, options: new Set<string>() };
    if (!multiple) answer.options.clear();
    if ((event.target as HTMLInputElement).checked) answer.options.add(optionId);
    else answer.options.delete(optionId);
    this.answers.set(questionId, answer);
  }

  protected submitQuiz(attempt: QuizAttempt): void {
    if (this.assessmentBusy()) return;
    const payload: readonly QuizAnswerInput[] = attempt.questions.map((question) => ({
      questionId: question.id,
      textAnswer: this.answers.get(question.id)?.text ?? null,
      selectedOptionIds: [...(this.answers.get(question.id)?.options ?? [])],
    }));
    this.assessmentBusy.set(true);
    this.api
      .submitQuiz(attempt.enrollmentId, attempt.quizVersionId, attempt.id, payload)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.quizAttempt.set(result);
          this.assessmentBusy.set(false);
          if (result.passed) this.reload();
        },
        error: (error: unknown) => {
          this.assessmentBusy.set(false);
          this.setActionError(error);
        },
      });
  }

  protected submitAssignment(versionId: string): void {
    const manifest = this.state().manifest;
    const text = this.assignmentText.trim();
    if (!manifest || !text || this.assessmentBusy()) return;
    this.assessmentBusy.set(true);
    this.api
      .submitAssignment(manifest.enrollmentId, versionId, text)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (submission) => {
          this.assessmentBusy.set(false);
          this.assignmentText = '';
          this.assignmentSubmission.set(submission);
          this.assignmentStatus.set(
            this.locale.locale() === 'ar'
              ? `تم حفظ التسليم رقم ${String(submission.submissionNumber)}.`
              : `Submission ${String(submission.submissionNumber)} saved.`,
          );
        },
        error: (error: unknown) => {
          this.assessmentBusy.set(false);
          this.setActionError(error);
        },
      });
  }

  protected chooseAssignmentFile(event: Event, submission: AssignmentSubmission): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.item(0);
    input.value = '';
    if (!file || this.assignmentUploadBusy()) return;
    if (
      file.size <= 0 ||
      file.size > 250 * 1024 * 1024 ||
      (file.type && file.type !== 'application/pdf')
    ) {
      this.actionError.set('MEDIA.FILE_TYPE_HINT');
      return;
    }
    this.assignmentUploadBusy.set(true);
    this.assignmentUploadStatus.set(
      this.locale.locale() === 'ar' ? 'جارٍ إعداد الرفع…' : 'Preparing upload…',
    );
    const abort = new AbortController();
    let uploadSession: UploadSession | null = null;
    this.requestValue(
      this.api.createAssignmentFile(
        submission.enrollmentId,
        submission.assignmentVersionId,
        {
          submissionId: submission.id,
          clientFileId: globalThis.crypto.randomUUID(),
          expectedBytes: file.size,
          fileName: file.name,
          contentType: file.type || 'application/pdf',
        },
        globalThis.crypto.randomUUID(),
      ),
    )
      .then((session) => {
        uploadSession = session;
        this.assignmentUploadStatus.set(
          this.locale.locale() === 'ar' ? 'جارٍ حساب بصمة الملف…' : 'Hashing file…',
        );
        return this.mediaHasher.hash(file, session.partSize, abort.signal, () => undefined);
      })
      .then((hashes) => {
        if (!uploadSession) throw new AssignmentUploadError('MEDIA.SESSION_MISSING');
        this.assignmentUploadStatus.set(
          this.locale.locale() === 'ar' ? 'جارٍ رفع الملف…' : 'Uploading file…',
        );
        return this.uploadAssignmentFile(file, uploadSession, hashes);
      })
      .then(() => {
        this.assignmentUploadStatus.set(
          this.locale.locale() === 'ar' ? 'جارٍ فحص الملف…' : 'Scanning file…',
        );
        this.pollAssignmentSubmission(submission);
      })
      .catch((error: unknown) => {
        this.assignmentUploadBusy.set(false);
        this.assignmentUploadStatus.set(null);
        this.setActionError(error);
      });
  }

  protected downloadAssignmentFile(file: AssignmentSubmissionFile): void {
    if (file.state !== 'Ready') return;
    this.mediaApi
      .createDownloadGrant(file.assetId, { variantId: null, fileName: file.fileName })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (grant) => {
          globalThis.location.assign(grant.url);
        },
        error: (error: unknown) => {
          this.setActionError(error);
        },
      });
  }

  private pollAssignmentSubmission(submission: AssignmentSubmission): void {
    this.api
      .getAssignmentSubmission(
        submission.enrollmentId,
        submission.assignmentVersionId,
        submission.id,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.assignmentSubmission.set(updated);
          const pending = updated.files.some(
            (file) => !['Ready', 'Rejected', 'Deleted'].includes(file.state),
          );
          if (pending) {
            setTimeout(() => {
              this.pollAssignmentSubmission(updated);
            }, 3000);
            return;
          }
          this.assignmentUploadBusy.set(false);
          this.assignmentUploadStatus.set(
            updated.files.some((file) => file.state === 'Rejected')
              ? this.locale.locale() === 'ar'
                ? 'رُفض ملف أثناء الفحص.'
                : 'A file was rejected during scanning.'
              : this.locale.locale() === 'ar'
                ? 'الملف جاهز.'
                : 'File ready.',
          );
        },
        error: (error: unknown) => {
          this.assignmentUploadBusy.set(false);
          this.setActionError(error);
        },
      });
  }

  private async uploadAssignmentFile(
    file: File,
    session: UploadSession,
    hashes: MediaFileHashes,
  ): Promise<void> {
    if (session.mode === 'Stream') {
      await this.requestUpload(
        this.mediaApi.uploadAssignmentStream(session.uploadSessionId, file, hashes.sha256),
      );
      return;
    }
    const parts: CompletedUploadPart[] = [];
    for (const part of hashes.parts) {
      const grant = await this.requestValue(
        this.mediaApi.issuePart(session.uploadSessionId, {
          partNumber: part.partNumber,
          expectedBytes: part.size,
          sha256: part.sha256,
        }),
      );
      const offset = (part.partNumber - 1) * session.partSize;
      const etag = await this.requestStorageUpload(
        this.directStorage.putPart(
          grant.uploadUrl,
          file.slice(offset, offset + part.size),
          grant.requiredChecksumSha256,
        ),
      );
      parts.push({ ...part, etag });
    }
    const completed = await this.requestValue(
      this.mediaApi.complete(
        session.uploadSessionId,
        { totalBytes: file.size, sha256: hashes.sha256, parts },
        globalThis.crypto.randomUUID(),
      ),
    );
    if (completed.state !== 'Completed') throw new AssignmentUploadError('MEDIA.SESSION_TERMINAL');
  }

  private requestStorageUpload(source: Observable<DirectStorageUploadEvent>): Promise<string> {
    return new Promise((resolve, reject) => {
      source.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (event) => {
          if (event.kind === 'complete') resolve(event.etag);
        },
        error: reject,
      });
    });
  }

  private requestValue<T>(source: Observable<T>): Promise<T> {
    return new Promise((resolve, reject) => {
      source.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: resolve,
        error: reject,
      });
    });
  }

  private requestUpload(source: Observable<MediaUploadEvent>): Promise<void> {
    return new Promise((resolve, reject) => {
      source.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (event) => {
          if (event.kind === 'complete') resolve();
        },
        error: reject,
      });
    });
  }

  private loadCurrentAssignmentSubmission(enrollmentId: string, assignmentVersionId: string): void {
    this.api
      .getCurrentAssignmentSubmission(enrollmentId, assignmentVersionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (submission) => {
          this.assignmentSubmission.set(submission);
          if (
            submission.files.some((file) => !['Ready', 'Rejected', 'Deleted'].includes(file.state))
          ) {
            this.assignmentUploadBusy.set(true);
            this.assignmentUploadStatus.set(
              this.locale.locale() === 'ar' ? 'جارٍ فحص الملفات…' : 'Scanning files…',
            );
            this.pollAssignmentSubmission(submission);
          }
        },
        error: (error: unknown) => {
          if (!(error instanceof ApiProblem) || error.code !== 'ASSIGNMENT.SUBMISSION_NOT_FOUND') {
            this.setActionError(error);
          }
        },
      });
  }

  private bindRoute(): void {
    if (!this.connectivity.isOnline()) {
      this.state.set({ status: 'offline', manifest: null, lesson: null, errorCode: null });
      return;
    }
    this.route.paramMap
      .pipe(
        map((params) => ({
          enrollmentId: params.get('enrollmentId') ?? '',
          lessonId: params.get('lessonId'),
        })),
        distinctUntilChanged(
          (left, right) =>
            left.enrollmentId === right.enrollmentId && left.lessonId === right.lessonId,
        ),
        switchMap(({ enrollmentId, lessonId }) => {
          this.resetLessonState();
          if (!enrollmentId)
            return of<WorkspaceState>({
              status: 'error',
              manifest: null,
              lesson: null,
              errorCode: 'LEARNING.NOT_FOUND',
            });
          return this.api.getManifest(enrollmentId).pipe(
            switchMap((manifest) => {
              const firstLessonId =
                manifest.sections.flatMap((section) => section.lessons)[0]?.id ?? null;
              const selectedLessonId = lessonId ?? manifest.nextLessonId ?? firstLessonId;
              if (!selectedLessonId)
                return of<WorkspaceState>({
                  status: 'success',
                  manifest,
                  lesson: null,
                  errorCode: null,
                });
              if (!lessonId) {
                void this.router.navigate(
                  ['/', this.locale.locale(), 'learn', enrollmentId, 'lessons', selectedLessonId],
                  { replaceUrl: true },
                );
              }
              return forkJoin({
                lesson: this.api.getLesson(enrollmentId, selectedLessonId),
                notes: this.api.getNotes(enrollmentId, selectedLessonId),
              }).pipe(
                map(({ lesson, notes }): WorkspaceState => {
                  this.notes.set(notes);
                  this.selectedVariant.set(lesson.mediaVariants[0] ?? null);
                  if (lesson.assignmentVersionId) {
                    this.loadCurrentAssignmentSubmission(enrollmentId, lesson.assignmentVersionId);
                  }
                  this.api
                    .markRecentlyViewed(enrollmentId, selectedLessonId)
                    .pipe(takeUntilDestroyed(this.destroyRef))
                    .subscribe({
                      error: () => {
                        // Recently-viewed is best effort and never blocks the lesson.
                      },
                    });
                  return { status: 'success', manifest, lesson, errorCode: null };
                }),
              );
            }),
            catchError((error: unknown) =>
              of<WorkspaceState>({
                status: 'error',
                manifest: null,
                lesson: null,
                errorCode: error instanceof ApiProblem ? error.code : null,
              }),
            ),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((state) => {
        this.state.set(state);
      });
  }

  private updateProgress(positionSeconds: number, completionIntent: boolean): void {
    const lesson = this.state().lesson;
    const manifest = this.state().manifest;
    if (!lesson || !manifest || this.savingProgress()) return;
    this.savingProgress.set(true);
    this.sequence = Math.max(this.sequence + 1, Date.now());
    this.api
      .updateProgress(manifest.enrollmentId, lesson.id, {
        clientCommandId: globalThis.crypto.randomUUID(),
        sequence: this.sequence,
        positionSeconds,
        watchedIntervals: this.watchedIntervals,
        completionIntent,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (progress) => {
          this.savingProgress.set(false);
          if (progress.isCompleted) this.reload();
        },
        error: (error: unknown) => {
          this.savingProgress.set(false);
          this.setActionError(error);
        },
      });
  }

  private resetLessonState(): void {
    this.state.set({ status: 'loading', manifest: null, lesson: null, errorCode: null });
    this.notes.set([]);
    this.quizAttempt.set(null);
    this.mediaUrl.set(null);
    this.assignmentStatus.set(null);
    this.assignmentSubmission.set(null);
    this.assignmentUploadBusy.set(false);
    this.assignmentUploadStatus.set(null);
    this.answers.clear();
    this.watchedIntervals = [];
  }

  private setActionError(error: unknown): void {
    this.actionError.set(
      error instanceof ApiProblem || error instanceof AssignmentUploadError
        ? error.code
        : 'LEARNING.ACTION_FAILED',
    );
  }
}

class AssignmentUploadError extends Error {
  constructor(readonly code: string) {
    super(code);
  }
}

const mergeIntervals = (intervals: readonly WatchedInterval[]): WatchedInterval[] => {
  const ordered = intervals
    .filter((item) => item.endSeconds > item.startSeconds)
    .sort((left, right) => left.startSeconds - right.startSeconds);
  const merged: WatchedInterval[] = [];
  for (const interval of ordered) {
    const previous = merged.at(-1);
    if (previous && interval.startSeconds <= previous.endSeconds + 1)
      previous.endSeconds = Math.max(previous.endSeconds, interval.endSeconds);
    else merged.push({ ...interval });
  }
  return merged;
};
