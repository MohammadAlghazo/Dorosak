import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { debounceTime, Subject } from 'rxjs';
import type { LessonType, SectionInput } from '../../core/api/phase6-api.types';
import { LocaleService } from '../../core/i18n/locale.service';
import { CourseEditorStore } from './course-editor.store';

interface EditableLesson {
  id: string | null;
  key: string;
  title: string;
  lessonType: LessonType;
  content: string;
  mediaAssetId: string | null;
  quizVersionId: string | null;
  assignmentVersionId: string | null;
}

interface EditableSection {
  id: string | null;
  key: string;
  title: string;
  lessons: EditableLesson[];
}

@Component({
  selector: 'drs-curriculum-page',
  imports: [RouterLink],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="curriculum-title">
      <a class="back-link" [routerLink]="['../']">
        {{ locale.locale() === 'ar' ? 'العودة إلى بيانات الدورة' : 'Back to course metadata' }}
      </a>
      <header class="workflow-heading workflow-heading-row">
        <div>
          <p class="identity-kicker">
            {{ locale.locale() === 'ar' ? 'بنية الدورة' : 'Course structure' }}
          </p>
          <h1 id="curriculum-title">
            {{ locale.locale() === 'ar' ? 'تحرير المنهج' : 'Edit curriculum' }}
          </h1>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'استخدم أزرار التحريك أو Alt مع الأسهم لإعادة ترتيب الأقسام والدروس.'
                : 'Use the move buttons, or Alt plus arrows, to reorder sections and lessons.'
            }}
          </p>
        </div>
        <nav
          class="section-tabs"
          [attr.aria-label]="locale.locale() === 'ar' ? 'أقسام المسودة' : 'Draft sections'"
        >
          <a [routerLink]="['../']">{{ locale.locale() === 'ar' ? 'البيانات' : 'Metadata' }}</a>
          <a [routerLink]="['../curriculum']" aria-current="page">{{
            locale.locale() === 'ar' ? 'المنهج' : 'Curriculum'
          }}</a>
          <a [routerLink]="['../assessments']">{{
            locale.locale() === 'ar' ? 'التقييمات' : 'Assessments'
          }}</a>
          <a [routerLink]="['../media']">{{ locale.locale() === 'ar' ? 'الوسائط' : 'Media' }}</a>
          <a [routerLink]="['../publication']">{{
            locale.locale() === 'ar' ? 'النشر' : 'Publication'
          }}</a>
        </nav>
      </header>

      @switch (store.curriculum().status) {
        @case ('loading') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ تحميل المنهج…' : 'Loading curriculum…' }}
          </div>
        }
        @case ('saving') {
          <div class="workflow-state" role="status">
            {{ locale.locale() === 'ar' ? 'جارٍ حفظ المنهج…' : 'Saving curriculum…' }}
          </div>
        }
        @case ('offline') {
          <div class="form-alert" role="alert">
            {{ locale.locale() === 'ar' ? 'أنت غير متصل.' : 'You are offline.' }}
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('error') {
          <div class="form-alert" role="alert">
            {{
              locale.locale() === 'ar' ? 'تعذر تحميل المنهج.' : 'Curriculum could not be loaded.'
            }}
            @if (store.curriculum().errorCode) {
              <code>{{ store.curriculum().errorCode }}</code>
            }
            <button class="text-button" type="button" (click)="reload()">
              {{ locale.locale() === 'ar' ? 'إعادة المحاولة' : 'Retry' }}
            </button>
          </div>
        }
        @case ('conflict') {
          <div class="conflict-panel" role="alert" aria-labelledby="curriculum-conflict-title">
            <h2 id="curriculum-conflict-title">
              {{
                locale.locale() === 'ar' ? 'تعارض في نسخة المنهج' : 'Curriculum version conflict'
              }}
            </h2>
            <p>
              {{
                locale.locale() === 'ar'
                  ? 'لم تُستبدل تغييراتك تلقائيًا. أعد تحميل نسخة الخادم للمقارنة.'
                  : 'Your changes were not replaced automatically. Reload the server version to compare.'
              }}
            </p>
            <button class="danger-button" type="button" (click)="reload()">
              {{
                locale.locale() === 'ar'
                  ? 'تجاهل تعديلاتي وإعادة التحميل'
                  : 'Discard my edits and reload'
              }}
            </button>
          </div>
        }
      }
      @if (validationError()) {
        <div class="form-alert" role="alert">{{ validationError() }}</div>
      }

      <div class="curriculum-toolbar">
        <span class="save-indicator" role="status">{{ saveLabel() }}</span>
        <button class="primary-button" type="button" (click)="addSection()">
          {{ locale.locale() === 'ar' ? 'إضافة قسم' : 'Add section' }}
        </button>
      </div>

      @if (sections().length === 0 && store.curriculum().status === 'success') {
        <div class="empty-state">
          <h2>{{ locale.locale() === 'ar' ? 'المنهج فارغ' : 'The curriculum is empty' }}</h2>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'أضف قسمًا ودرسًا لبدء المسودة.'
                : 'Add a section and lesson to start the draft.'
            }}
          </p>
        </div>
      }

      <div class="curriculum-list" aria-live="polite">
        @for (section of sections(); track section.key; let sectionIndex = $index) {
          <article
            class="curriculum-section"
            [attr.aria-labelledby]="'section-title-' + section.id"
          >
            <header class="curriculum-section-heading">
              <div class="position-badge">{{ sectionIndex + 1 }}</div>
              <label class="sr-only" [for]="'section-title-' + section.id">
                {{ locale.locale() === 'ar' ? 'عنوان القسم' : 'Section title' }}
              </label>
              <input
                class="section-title-input"
                [id]="'section-title-' + section.id"
                [value]="section.title"
                maxlength="200"
                (input)="updateSectionTitle(sectionIndex, $event)"
              />
              <div class="reorder-actions">
                <button
                  class="icon-button"
                  type="button"
                  [disabled]="sectionIndex === 0"
                  [attr.aria-label]="moveLabel('section', section.title, 'up')"
                  aria-keyshortcuts="Alt+ArrowUp"
                  (click)="moveSection(sectionIndex, -1)"
                  (keydown)="reorderKey($event, 'section', sectionIndex, -1)"
                >
                  ↑
                </button>
                <button
                  class="icon-button"
                  type="button"
                  [disabled]="sectionIndex === sections().length - 1"
                  [attr.aria-label]="moveLabel('section', section.title, 'down')"
                  aria-keyshortcuts="Alt+ArrowDown"
                  (click)="moveSection(sectionIndex, 1)"
                  (keydown)="reorderKey($event, 'section', sectionIndex, 1)"
                >
                  ↓
                </button>
                <button
                  class="icon-button danger-icon"
                  type="button"
                  [disabled]="sections().length <= 1"
                  [attr.aria-label]="removeLabel('section', section.title)"
                  (click)="removeSection(sectionIndex)"
                >
                  ×
                </button>
              </div>
            </header>
            <div class="lesson-list">
              @for (lesson of section.lessons; track lesson.key; let lessonIndex = $index) {
                <div class="lesson-row">
                  <div class="position-badge lesson-position">{{ lessonIndex + 1 }}</div>
                  <div class="lesson-fields">
                    <label class="sr-only" [for]="'lesson-title-' + lesson.id">
                      {{ locale.locale() === 'ar' ? 'عنوان الدرس' : 'Lesson title' }}
                    </label>
                    <input
                      [id]="'lesson-title-' + lesson.id"
                      [value]="lesson.title"
                      maxlength="200"
                      (input)="updateLesson(sectionIndex, lessonIndex, 'title', $event)"
                    />
                    <label class="sr-only" [for]="'lesson-type-' + lesson.id">
                      {{ locale.locale() === 'ar' ? 'نوع الدرس' : 'Lesson type' }}
                    </label>
                    <select
                      [id]="'lesson-type-' + lesson.id"
                      [value]="lesson.lessonType"
                      (change)="updateLesson(sectionIndex, lessonIndex, 'lessonType', $event)"
                    >
                      @for (type of lessonTypes; track type) {
                        <option [value]="type">{{ type }}</option>
                      }
                    </select>
                    <label class="sr-only" [for]="'lesson-content-' + lesson.id">
                      {{ locale.locale() === 'ar' ? 'محتوى الدرس' : 'Lesson content' }}
                    </label>
                    <textarea
                      [id]="'lesson-content-' + lesson.id"
                      [value]="lesson.content"
                      rows="3"
                      maxlength="100000"
                      (input)="updateLesson(sectionIndex, lessonIndex, 'content', $event)"
                    ></textarea>
                    @if (lesson.lessonType === 'Document') {
                      <label [for]="'lesson-media-' + lesson.key">{{
                        locale.locale() === 'ar' ? 'معرّف ملف PDF الجاهز' : 'Ready PDF media ID'
                      }}</label>
                      <input
                        [id]="'lesson-media-' + lesson.key"
                        [value]="lesson.mediaAssetId ?? ''"
                        (input)="
                          updateLessonReference(sectionIndex, lessonIndex, 'mediaAssetId', $event)
                        "
                      />
                    }
                    @if (lesson.lessonType === 'Quiz') {
                      <label [for]="'lesson-quiz-' + lesson.key">{{
                        locale.locale() === 'ar'
                          ? 'معرّف نسخة الكويز الجاهزة'
                          : 'Ready quiz version ID'
                      }}</label>
                      <input
                        [id]="'lesson-quiz-' + lesson.key"
                        [value]="lesson.quizVersionId ?? ''"
                        (input)="
                          updateLessonReference(sectionIndex, lessonIndex, 'quizVersionId', $event)
                        "
                      />
                    }
                    @if (lesson.lessonType === 'Assignment') {
                      <label [for]="'lesson-assignment-' + lesson.key">{{
                        locale.locale() === 'ar'
                          ? 'معرّف نسخة الواجب الجاهزة'
                          : 'Ready assignment version ID'
                      }}</label>
                      <input
                        [id]="'lesson-assignment-' + lesson.key"
                        [value]="lesson.assignmentVersionId ?? ''"
                        (input)="
                          updateLessonReference(
                            sectionIndex,
                            lessonIndex,
                            'assignmentVersionId',
                            $event
                          )
                        "
                      />
                    }
                  </div>
                  <div class="reorder-actions lesson-actions">
                    <button
                      class="icon-button"
                      type="button"
                      [disabled]="lessonIndex === 0"
                      [attr.aria-label]="moveLabel('lesson', lesson.title, 'up')"
                      aria-keyshortcuts="Alt+ArrowUp"
                      (click)="moveLesson(sectionIndex, lessonIndex, -1)"
                      (keydown)="reorderKey($event, 'lesson', sectionIndex, lessonIndex)"
                    >
                      ↑
                    </button>
                    <button
                      class="icon-button"
                      type="button"
                      [disabled]="lessonIndex === section.lessons.length - 1"
                      [attr.aria-label]="moveLabel('lesson', lesson.title, 'down')"
                      aria-keyshortcuts="Alt+ArrowDown"
                      (click)="moveLesson(sectionIndex, lessonIndex, 1)"
                      (keydown)="reorderKey($event, 'lesson', sectionIndex, lessonIndex)"
                    >
                      ↓
                    </button>
                    <button
                      class="icon-button danger-icon"
                      type="button"
                      [disabled]="section.lessons.length <= 1"
                      [attr.aria-label]="removeLabel('lesson', lesson.title)"
                      (click)="removeLesson(sectionIndex, lessonIndex)"
                    >
                      ×
                    </button>
                  </div>
                </div>
              }
            </div>
            <button class="secondary-button" type="button" (click)="addLesson(sectionIndex)">
              {{ locale.locale() === 'ar' ? 'إضافة درس' : 'Add lesson' }}
            </button>
          </article>
        }
      </div>
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CurriculumPageComponent {
  protected readonly locale = inject(LocaleService);
  protected readonly store = inject(CourseEditorStore);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly changes = new Subject<void>();
  private readonly courseId = routeCourseId(this.route);
  private syncedVersion = -1;
  protected readonly sections = signal<EditableSection[]>([]);
  protected readonly validationError = signal<string | null>(null);
  protected readonly lessonTypes: readonly LessonType[] = [
    'Video',
    'Article',
    'Document',
    'Quiz',
    'Assignment',
  ];

  constructor() {
    this.store.loadCurriculum(this.courseId);
    effect(() => {
      const curriculum = this.store.curriculum().value;
      if (curriculum === null || curriculum.draftVersion === this.syncedVersion) return;
      this.syncedVersion = curriculum.draftVersion;
      this.sections.set(
        curriculum.sections.map((section) => ({
          id: section.id,
          key: section.id,
          title: section.title,
          lessons: section.lessons.map((lesson) => ({
            id: lesson.id,
            key: lesson.id,
            title: lesson.title,
            lessonType: lesson.lessonType,
            content: lesson.content,
            mediaAssetId: lesson.mediaAssetId,
            quizVersionId: lesson.quizVersionId,
            assignmentVersionId: lesson.assignmentVersionId,
          })),
        })),
      );
    });
    this.changes.pipe(debounceTime(900), takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.save();
    });
  }

  protected addSection(): void {
    this.sections.update((sections) => [
      ...sections,
      { id: null, key: clientKey(), title: '', lessons: [newLesson()] },
    ]);
    this.changes.next();
  }

  protected removeSection(index: number): void {
    this.sections.update((sections) => sections.filter((_, itemIndex) => itemIndex !== index));
    this.changes.next();
  }

  protected addLesson(sectionIndex: number): void {
    this.sections.update((sections) =>
      sections.map((section, index) =>
        index === sectionIndex
          ? { ...section, lessons: [...section.lessons, newLesson()] }
          : section,
      ),
    );
    this.changes.next();
  }

  protected removeLesson(sectionIndex: number, lessonIndex: number): void {
    this.sections.update((sections) =>
      sections.map((section, index) =>
        index === sectionIndex
          ? {
              ...section,
              lessons: section.lessons.filter((_, itemIndex) => itemIndex !== lessonIndex),
            }
          : section,
      ),
    );
    this.changes.next();
  }

  protected moveSection(index: number, offset: -1 | 1): void {
    this.sections.update((sections) => moveItem(sections, index, offset));
    this.changes.next();
  }

  protected moveLesson(sectionIndex: number, lessonIndex: number, offset: -1 | 1): void {
    this.sections.update((sections) =>
      sections.map((section, index) =>
        index === sectionIndex
          ? { ...section, lessons: moveItem(section.lessons, lessonIndex, offset) }
          : section,
      ),
    );
    this.changes.next();
  }

  protected updateSectionTitle(index: number, event: Event): void {
    const value = inputValue(event);
    this.sections.update((sections) =>
      sections.map((section, itemIndex) =>
        itemIndex === index ? { ...section, title: value } : section,
      ),
    );
    this.changes.next();
  }

  protected updateLesson(
    sectionIndex: number,
    lessonIndex: number,
    field: 'title' | 'lessonType' | 'content',
    event: Event,
  ): void {
    const value = inputValue(event);
    this.sections.update((sections) =>
      sections.map((section, currentSectionIndex) =>
        currentSectionIndex === sectionIndex
          ? {
              ...section,
              lessons: section.lessons.map((lesson, currentLessonIndex) =>
                currentLessonIndex === lessonIndex
                  ? updateLessonField(lesson, field, value)
                  : lesson,
              ),
            }
          : section,
      ),
    );
    this.changes.next();
  }

  protected updateLessonReference(
    sectionIndex: number,
    lessonIndex: number,
    field: 'mediaAssetId' | 'quizVersionId' | 'assignmentVersionId',
    event: Event,
  ): void {
    const value = inputValue(event).trim() || null;
    this.sections.update((sections) =>
      sections.map((section, currentSectionIndex) =>
        currentSectionIndex === sectionIndex
          ? {
              ...section,
              lessons: section.lessons.map((lesson, currentLessonIndex) =>
                currentLessonIndex === lessonIndex ? { ...lesson, [field]: value } : lesson,
              ),
            }
          : section,
      ),
    );
    this.changes.next();
  }

  protected reorderKey(
    event: KeyboardEvent,
    kind: 'section' | 'lesson',
    firstIndex: number,
    secondIndexOrOffset: number,
  ): void {
    if (!event.altKey || (event.key !== 'ArrowUp' && event.key !== 'ArrowDown')) return;
    event.preventDefault();
    const direction: -1 | 1 = event.key === 'ArrowUp' ? -1 : 1;
    if (kind === 'section') this.moveSection(firstIndex, direction);
    else this.moveLesson(firstIndex, secondIndexOrOffset, direction);
  }

  protected reload(): void {
    this.store.loadCurriculum(this.courseId);
  }

  protected saveLabel(): string {
    const status = this.store.curriculum().status;
    if (status === 'saving') return this.locale.locale() === 'ar' ? 'جارٍ الحفظ…' : 'Saving…';
    if (status === 'conflict') return this.locale.locale() === 'ar' ? 'تعارض' : 'Conflict';
    return this.locale.locale() === 'ar' ? 'تم الحفظ' : 'Saved';
  }

  protected moveLabel(kind: 'section' | 'lesson', title: string, direction: 'up' | 'down'): string {
    const labels = {
      section: { up: 'Move section up', down: 'Move section down' },
      lesson: { up: 'Move lesson up', down: 'Move lesson down' },
    };
    return this.locale.locale() === 'ar'
      ? `${direction === 'up' ? 'تحريك' : 'تحريك'} ${kind === 'section' ? 'القسم' : 'الدرس'} ${direction === 'up' ? 'لأعلى' : 'لأسفل'}: ${title || 'بدون عنوان'}`
      : `${labels[kind][direction]}: ${title || 'Untitled'}`;
  }

  protected removeLabel(kind: 'section' | 'lesson', title: string): string {
    return this.locale.locale() === 'ar'
      ? `حذف ${kind === 'section' ? 'القسم' : 'الدرس'}: ${title || 'بدون عنوان'}`
      : `Remove ${kind}: ${title || 'Untitled'}`;
  }

  private save(): void {
    const sections: readonly SectionInput[] = this.sections().map((section, position) => ({
      id: section.id,
      position,
      title: section.title.trim(),
      lessons: section.lessons.map((lesson, lessonPosition) => ({
        id: lesson.id,
        position: lessonPosition,
        title: lesson.title.trim(),
        lessonType: lesson.lessonType,
        content: lesson.content,
        mediaAssetId: lesson.lessonType === 'Document' ? lesson.mediaAssetId : null,
        quizVersionId: lesson.lessonType === 'Quiz' ? lesson.quizVersionId : null,
        assignmentVersionId: lesson.lessonType === 'Assignment' ? lesson.assignmentVersionId : null,
      })),
    }));
    if (
      sections.length === 0 ||
      sections.some(
        (section) =>
          section.title.length === 0 ||
          section.lessons.length === 0 ||
          section.lessons.some((lesson) => lesson.title.length === 0),
      )
    ) {
      this.validationError.set(
        this.locale.locale() === 'ar'
          ? 'يجب أن يحتوي كل قسم على عنوان ودرس واحد بعنوان على الأقل.'
          : 'Every section needs a title and at least one titled lesson.',
      );
      return;
    }
    this.validationError.set(null);
    this.store.saveCurriculum(this.courseId, sections);
  }
}

const routeCourseId = (route: ActivatedRoute): string => {
  const value =
    route.snapshot.paramMap.get('courseId') ?? route.parent?.snapshot.paramMap.get('courseId');
  if (value === null || value === undefined) {
    throw new Error('The course route requires a courseId parameter.');
  }
  return value;
};

const inputValue = (event: Event): string =>
  (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value;

const newLesson = (): EditableLesson => ({
  id: null,
  key: clientKey(),
  title: '',
  lessonType: 'Article',
  content: '',
  mediaAssetId: null,
  quizVersionId: null,
  assignmentVersionId: null,
});

const updateLessonField = (
  lesson: EditableLesson,
  field: 'title' | 'lessonType' | 'content',
  value: string,
): EditableLesson => {
  if (field === 'title') return { ...lesson, title: value };
  if (field === 'content') return { ...lesson, content: value };
  return { ...lesson, lessonType: value as LessonType };
};

const moveItem = <T>(items: readonly T[], index: number, offset: -1 | 1): T[] => {
  const target = index + offset;
  if (index < 0 || index >= items.length || target < 0 || target >= items.length) return [...items];
  const copy = [...items];
  const [item] = copy.splice(index, 1);
  if (item !== undefined) copy.splice(target, 0, item);
  return copy;
};

const clientKey = (): string => globalThis.crypto.randomUUID();
