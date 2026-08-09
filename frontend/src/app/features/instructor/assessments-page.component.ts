import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/api/api-problem';
import { LearningApiClient } from '../../core/api/learning-api.client';
import type {
  AssignmentVersion,
  AssessmentAudienceType,
  CourseLearner,
  QuizQuestionInput,
  QuizVersion,
} from '../../core/api/learning-api.types';
import type {
  Curriculum,
  Lesson,
  SectionInput,
  VersionedResult,
} from '../../core/api/phase6-api.types';
import { InstructorApiClient } from '../../core/api/instructor-api.client';
import { LocaleService } from '../../core/i18n/locale.service';

type AssessmentKind = 'Quiz' | 'Assignment';

@Component({
  selector: 'drs-assessments-page',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="workflow-page workflow-page-wide" aria-labelledby="assessment-editor-title">
      <a class="back-link" [routerLink]="['../curriculum']">{{
        locale.locale() === 'ar' ? 'العودة إلى المنهج' : 'Back to curriculum'
      }}</a>
      <header class="workflow-heading workflow-heading-row">
        <div>
          <p class="identity-kicker">ASSESSMENT DESK</p>
          <h1 id="assessment-editor-title">
            {{ locale.locale() === 'ar' ? 'إنشاء اختبار أو واجب' : 'Create a quiz or assignment' }}
          </h1>
          <p>
            {{
              locale.locale() === 'ar'
                ? 'اختر كل المسجلين أو طلاباً محددين. لا يستطيع غير المستهدف فتح التقييم حتى لو عرف رابطه.'
                : 'Choose all enrolled learners or a selected audience. Hidden assessments remain inaccessible by URL.'
            }}
          </p>
        </div>
        <nav class="section-tabs">
          <a [routerLink]="['../']">{{ locale.locale() === 'ar' ? 'البيانات' : 'Metadata' }}</a>
          <a [routerLink]="['../curriculum']">{{
            locale.locale() === 'ar' ? 'المنهج' : 'Curriculum'
          }}</a>
          <a [routerLink]="['../assessments']" aria-current="page">{{
            locale.locale() === 'ar' ? 'التقييمات' : 'Assessments'
          }}</a>
          <a [routerLink]="['../media']">{{ locale.locale() === 'ar' ? 'الملفات' : 'Files' }}</a>
        </nav>
      </header>

      @if (loading()) {
        <div class="workflow-state" role="status">
          {{
            locale.locale() === 'ar'
              ? 'جارٍ تحميل الدروس والطلاب…'
              : 'Loading lessons and learners…'
          }}
        </div>
      } @else {
        <form class="workflow-form" (ngSubmit)="create()">
          <article class="workflow-card form-grid two-columns">
            <div>
              <label for="assessment-kind">{{ locale.locale() === 'ar' ? 'النوع' : 'Type' }}</label>
              <select id="assessment-kind" [(ngModel)]="kind" name="kind">
                <option value="Quiz">
                  {{ locale.locale() === 'ar' ? 'كويز / امتحان' : 'Quiz / exam' }}
                </option>
                <option value="Assignment">
                  {{ locale.locale() === 'ar' ? 'واجب' : 'Assignment' }}
                </option>
              </select>
            </div>
            <div>
              <label for="assessment-lesson">{{
                locale.locale() === 'ar' ? 'الدرس' : 'Lesson'
              }}</label>
              <select id="assessment-lesson" [(ngModel)]="lessonId" name="lessonId" required>
                <option value="" disabled>
                  {{ locale.locale() === 'ar' ? 'اختر درساً' : 'Choose a lesson' }}
                </option>
                @for (lesson of compatibleLessons(); track lesson.id) {
                  <option [value]="lesson.id">{{ lesson.title }}</option>
                }
              </select>
            </div>
            <div class="wide">
              <label for="assessment-title">{{
                locale.locale() === 'ar' ? 'العنوان' : 'Title'
              }}</label>
              <input
                id="assessment-title"
                [(ngModel)]="title"
                name="title"
                maxlength="200"
                required
              />
            </div>
            <div>
              <label for="assessment-deadline">{{
                locale.locale() === 'ar' ? 'الموعد النهائي' : 'Deadline'
              }}</label>
              <input
                id="assessment-deadline"
                type="datetime-local"
                [(ngModel)]="deadline"
                name="deadline"
              />
            </div>
            <div>
              <label for="assessment-audience">{{
                locale.locale() === 'ar' ? 'الجمهور' : 'Audience'
              }}</label>
              <select id="assessment-audience" [(ngModel)]="audienceType" name="audienceType">
                <option value="AllEnrolled">
                  {{ locale.locale() === 'ar' ? 'كل المسجلين' : 'All enrolled learners' }}
                </option>
                <option value="SelectedLearners">
                  {{ locale.locale() === 'ar' ? 'طلاب محددون' : 'Selected learners' }}
                </option>
              </select>
            </div>
          </article>

          @if (audienceType === 'SelectedLearners') {
            <article class="workflow-card learner-list">
              <h2>{{ locale.locale() === 'ar' ? 'حدد الطلاب' : 'Select learners' }}</h2>
              @if (learners().length === 0) {
                <p>
                  {{
                    locale.locale() === 'ar'
                      ? 'لا يوجد طلاب مسجلون بعد.'
                      : 'No learners are enrolled yet.'
                  }}
                </p>
              }
              @for (learner of learners(); track learner.userId) {
                <label>
                  <input
                    type="checkbox"
                    [checked]="selectedLearnerIds().has(learner.userId)"
                    (change)="toggleLearner(learner.userId, $event)"
                  />
                  <span
                    ><strong>{{ learner.displayName }}</strong
                    ><small>{{ learnerStatus(learner) }}</small></span
                  >
                </label>
              }
            </article>
          }

          @if (kind === 'Quiz') {
            <article class="workflow-card form-grid two-columns">
              <div>
                <label for="attempt-limit">{{
                  locale.locale() === 'ar' ? 'عدد المحاولات' : 'Attempt limit'
                }}</label>
                <input
                  id="attempt-limit"
                  type="number"
                  min="1"
                  max="100"
                  [(ngModel)]="attemptLimit"
                  name="attemptLimit"
                />
              </div>
              <div>
                <label for="duration">{{
                  locale.locale() === 'ar' ? 'المدة بالدقائق' : 'Duration in minutes'
                }}</label>
                <input
                  id="duration"
                  type="number"
                  min="1"
                  max="1440"
                  [(ngModel)]="durationMinutes"
                  name="duration"
                />
              </div>
              <div>
                <label for="pass-score">{{
                  locale.locale() === 'ar' ? 'علامة النجاح %' : 'Pass score %'
                }}</label>
                <input
                  id="pass-score"
                  type="number"
                  min="0"
                  max="100"
                  [(ngModel)]="passScore"
                  name="passScore"
                />
              </div>
            </article>
            @for (question of questions(); track $index; let index = $index) {
              <article class="workflow-card question-card">
                <header>
                  <h2>{{ locale.locale() === 'ar' ? 'السؤال' : 'Question' }} {{ index + 1 }}</h2>
                </header>
                <label [for]="'question-' + index">{{
                  locale.locale() === 'ar' ? 'النص' : 'Prompt'
                }}</label>
                <textarea
                  [id]="'question-' + index"
                  rows="3"
                  [value]="question.prompt"
                  (input)="updateQuestion(index, 'prompt', $event)"
                ></textarea>
                <label [for]="'question-type-' + index">{{
                  locale.locale() === 'ar' ? 'نوع السؤال' : 'Question type'
                }}</label>
                <select
                  [id]="'question-type-' + index"
                  [value]="question.type"
                  (change)="updateQuestion(index, 'type', $event)"
                >
                  <option value="SingleChoice">
                    {{ locale.locale() === 'ar' ? 'اختيار واحد' : 'Single choice' }}
                  </option>
                  <option value="TrueFalse">
                    {{ locale.locale() === 'ar' ? 'صح / خطأ' : 'True / false' }}
                  </option>
                  <option value="ShortAnswer">
                    {{ locale.locale() === 'ar' ? 'إجابة قصيرة' : 'Short answer' }}
                  </option>
                </select>
                @if (question.type === 'ShortAnswer') {
                  <label [for]="'accepted-' + index">{{
                    locale.locale() === 'ar'
                      ? 'الإجابة المعتمدة، أو اتركها فارغة للتصحيح اليدوي'
                      : 'Accepted answer, or leave blank for manual grading'
                  }}</label>
                  <input
                    [id]="'accepted-' + index"
                    [value]="question.acceptedAnswer ?? ''"
                    (input)="updateQuestion(index, 'acceptedAnswer', $event)"
                  />
                } @else {
                  @for (option of question.options; track $index; let optionIndex = $index) {
                    <div class="option-row">
                      <input
                        type="radio"
                        [name]="'correct-' + index"
                        [checked]="option.isCorrect"
                        (change)="setCorrectOption(index, optionIndex)"
                      />
                      <input
                        [value]="option.text"
                        (input)="updateOption(index, optionIndex, $event)"
                      />
                    </div>
                  }
                }
              </article>
            }
            <button class="secondary-button" type="button" (click)="addQuestion()">
              {{ locale.locale() === 'ar' ? 'إضافة سؤال' : 'Add question' }}
            </button>
          } @else {
            <article class="workflow-card">
              <label for="assignment-instructions">{{
                locale.locale() === 'ar' ? 'تعليمات الواجب' : 'Assignment instructions'
              }}</label>
              <textarea
                id="assignment-instructions"
                rows="8"
                [(ngModel)]="instructions"
                name="instructions"
                maxlength="100000"
              ></textarea>
              <label class="check-line"
                ><input
                  type="checkbox"
                  [(ngModel)]="allowMultipleSubmissions"
                  name="allowMultipleSubmissions"
                />{{
                  locale.locale() === 'ar' ? 'السماح بأكثر من تسليم' : 'Allow multiple submissions'
                }}</label
              >
            </article>
          }

          @if (errorCode()) {
            <div class="form-alert" role="alert">
              <code>{{ errorCode() }}</code>
            </div>
          }
          @if (createdVersionId()) {
            <div class="success-panel" role="status">
              <strong>{{
                locale.locale() === 'ar' ? 'نسخة جاهزة للنشر' : 'Version ready for publication'
              }}</strong>
              <code>{{ createdVersionId() }}</code>
              <p>
                {{
                  locale.locale() === 'ar'
                    ? 'تم ربط النسخة تلقائياً بالدرس المحدد.'
                    : 'The version was linked to the selected lesson automatically.'
                }}
              </p>
            </div>
          }
          <button class="primary-button" type="submit" [disabled]="saving()">
            {{
              saving()
                ? locale.locale() === 'ar'
                  ? 'جارٍ الإنشاء…'
                  : 'Creating…'
                : locale.locale() === 'ar'
                  ? 'إنشاء واعتماد النسخة'
                  : 'Create and mark ready'
            }}
          </button>
        </form>
      }
    </section>
  `,
  styles: `
    .wide {
      grid-column: 1 / -1;
    }
    .learner-list {
      display: grid;
      gap: var(--space-3);
    }
    .learner-list > label {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      min-block-size: 48px;
      padding: var(--space-3);
      border: 1px solid var(--color-border, #334155);
    }
    .learner-list span {
      display: grid;
    }
    .learner-list small {
      color: var(--color-muted);
    }
    .question-card {
      display: grid;
      gap: var(--space-3);
    }
    .question-card textarea,
    .question-card input,
    .question-card select {
      inline-size: 100%;
    }
    .option-row {
      display: grid;
      grid-template-columns: auto 1fr;
      align-items: center;
      gap: var(--space-3);
    }
    .check-line {
      display: flex;
      align-items: center;
      gap: var(--space-3);
      margin-block-start: var(--space-4);
    }
    .success-panel {
      display: grid;
      gap: var(--space-2);
      padding: var(--space-5);
      border-inline-start: 4px solid #2dd4bf;
      background: #0d2927;
    }
    @media (max-width: 640px) {
      .wide {
        grid-column: auto;
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AssessmentsPageComponent {
  protected readonly locale = inject(LocaleService);
  private readonly api = inject(LearningApiClient);
  private readonly instructorApi = inject(InstructorApiClient);
  private readonly destroyRef = inject(DestroyRef);
  private readonly courseId = routeCourseId(inject(ActivatedRoute));
  private curriculum: VersionedResult<Curriculum> | null = null;
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly errorCode = signal<string | null>(null);
  protected readonly learners = signal<readonly CourseLearner[]>([]);
  protected readonly lessons = signal<readonly Lesson[]>([]);
  protected readonly selectedLearnerIds = signal<ReadonlySet<string>>(new Set());
  protected readonly questions = signal<readonly QuizQuestionInput[]>([newQuestion()]);
  protected readonly createdVersionId = signal<string | null>(null);
  protected kind: AssessmentKind = 'Quiz';
  protected lessonId = '';
  protected title = '';
  protected deadline = '';
  protected audienceType: AssessmentAudienceType = 'AllEnrolled';
  protected attemptLimit = 1;
  protected durationMinutes: number | null = 30;
  protected passScore = 60;
  protected instructions = '';
  protected allowMultipleSubmissions = false;

  constructor() {
    this.api
      .getCourseLearners(this.courseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (learners) => {
          this.learners.set(learners);
        },
        error: (error: unknown) => {
          this.fail(error);
        },
      });
    this.instructorApi
      .getCurriculum(this.courseId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.curriculum = result;
          this.lessons.set(result.value.sections.flatMap((section) => section.lessons));
          this.loading.set(false);
        },
        error: (error: unknown) => {
          this.fail(error);
        },
      });
  }

  protected compatibleLessons(): readonly Lesson[] {
    return this.lessons().filter((lesson) => lesson.lessonType === this.kind);
  }

  protected learnerStatus(learner: CourseLearner): string {
    return learner.enrollments.map((enrollment) => enrollment.status).join(', ');
  }

  protected toggleLearner(userId: string, event: Event): void {
    const selected = new Set(this.selectedLearnerIds());
    if ((event.target as HTMLInputElement).checked) selected.add(userId);
    else selected.delete(userId);
    this.selectedLearnerIds.set(selected);
  }

  protected addQuestion(): void {
    this.questions.update((questions) => [...questions, newQuestion(questions.length)]);
  }

  protected updateQuestion(
    index: number,
    field: 'prompt' | 'type' | 'acceptedAnswer',
    event: Event,
  ): void {
    const value = (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement)
      .value;
    this.questions.update((questions) =>
      questions.map((question, questionIndex) => {
        if (questionIndex !== index) return question;
        if (field === 'type') return questionForType(question, value as QuizQuestionInput['type']);
        if (field === 'prompt') return { ...question, prompt: value };
        return { ...question, acceptedAnswer: value || null };
      }),
    );
  }

  protected updateOption(questionIndex: number, optionIndex: number, event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.questions.update((questions) =>
      questions.map((question, currentQuestion) =>
        currentQuestion === questionIndex
          ? {
              ...question,
              options: question.options.map((option, currentOption) =>
                currentOption === optionIndex ? { ...option, text } : option,
              ),
            }
          : question,
      ),
    );
  }

  protected setCorrectOption(questionIndex: number, optionIndex: number): void {
    this.questions.update((questions) =>
      questions.map((question, currentQuestion) =>
        currentQuestion === questionIndex
          ? {
              ...question,
              options: question.options.map((option, currentOption) => ({
                ...option,
                isCorrect: currentOption === optionIndex,
              })),
            }
          : question,
      ),
    );
  }

  protected create(): void {
    const selected = [...this.selectedLearnerIds()];
    if (
      !this.lessonId ||
      !this.title.trim() ||
      (this.audienceType === 'SelectedLearners' && selected.length === 0)
    ) {
      this.errorCode.set('ASSESSMENT.FORM_INCOMPLETE');
      return;
    }
    this.saving.set(true);
    this.errorCode.set(null);
    this.createdVersionId.set(null);
    const deadline = this.deadline ? new Date(this.deadline).toISOString() : null;
    if (this.kind === 'Quiz') {
      this.api
        .createQuizVersion(this.courseId, this.lessonId, {
          title: this.title.trim(),
          attemptLimit: this.attemptLimit,
          durationMinutes: this.durationMinutes,
          deadline,
          passScore: this.passScore,
          questions: this.questions(),
          audienceType: this.audienceType,
          selectedLearnerUserIds: selected,
        })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe({
          next: (version: QuizVersion) => {
            this.markQuizReady(version);
          },
          error: (error: unknown) => {
            this.fail(error);
          },
        });
      return;
    }
    this.api
      .createAssignmentVersion(this.courseId, this.lessonId, {
        title: this.title.trim(),
        instructions: this.instructions.trim(),
        deadline,
        allowMultipleSubmissions: this.allowMultipleSubmissions,
        audienceType: this.audienceType,
        selectedLearnerUserIds: selected,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (version: AssignmentVersion) => {
          this.markAssignmentReady(version);
        },
        error: (error: unknown) => {
          this.fail(error);
        },
      });
  }

  private markQuizReady(version: QuizVersion): void {
    this.api
      .markQuizReady(this.courseId, version.versionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.linkVersion('Quiz', result.versionId);
        },
        error: (error: unknown) => {
          this.fail(error);
        },
      });
  }

  private markAssignmentReady(version: AssignmentVersion): void {
    this.api
      .markAssignmentReady(this.courseId, version.versionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.linkVersion('Assignment', result.versionId);
        },
        error: (error: unknown) => {
          this.fail(error);
        },
      });
  }

  private linkVersion(kind: AssessmentKind, versionId: string): void {
    const curriculum = this.curriculum;
    if (!curriculum) {
      this.createdVersionId.set(versionId);
      this.saving.set(false);
      return;
    }
    const sections: readonly SectionInput[] = curriculum.value.sections.map((section) => ({
      id: section.id,
      position: section.position,
      title: section.title,
      lessons: section.lessons.map((lesson) => ({
        id: lesson.id,
        position: lesson.position,
        title: lesson.title,
        lessonType: lesson.lessonType,
        content: lesson.content,
        mediaAssetId: lesson.mediaAssetId,
        quizVersionId:
          lesson.id === this.lessonId && kind === 'Quiz' ? versionId : lesson.quizVersionId,
        assignmentVersionId:
          lesson.id === this.lessonId && kind === 'Assignment'
            ? versionId
            : lesson.assignmentVersionId,
      })),
    }));
    this.instructorApi
      .updateCurriculum(this.courseId, sections, curriculum.etag)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.curriculum = {
            value: { ...curriculum.value, draftVersion: updated.value.draftVersion },
            etag: updated.etag,
          };
          this.createdVersionId.set(versionId);
          this.saving.set(false);
        },
        error: (error: unknown) => {
          this.createdVersionId.set(versionId);
          this.fail(error);
        },
      });
  }

  private fail(error: unknown): void {
    this.errorCode.set(error instanceof ApiProblem ? error.code : 'ASSESSMENT.REQUEST_FAILED');
    this.loading.set(false);
    this.saving.set(false);
  }
}

const routeCourseId = (route: ActivatedRoute): string => {
  const value =
    route.snapshot.paramMap.get('courseId') ?? route.parent?.snapshot.paramMap.get('courseId');
  if (!value) throw new Error('The assessment route requires a courseId parameter.');
  return value;
};

const choiceOptions = () => [
  { position: 0, text: '', isCorrect: true },
  { position: 1, text: '', isCorrect: false },
];

const newQuestion = (position = 0): QuizQuestionInput => ({
  position,
  type: 'SingleChoice',
  prompt: '',
  points: 1,
  acceptedAnswer: null,
  options: choiceOptions(),
});

const questionForType = (
  question: QuizQuestionInput,
  type: QuizQuestionInput['type'],
): QuizQuestionInput => {
  if (type === 'ShortAnswer') return { ...question, type, options: [], acceptedAnswer: null };
  if (type === 'TrueFalse')
    return {
      ...question,
      type,
      acceptedAnswer: null,
      options: [
        { position: 0, text: 'True', isCorrect: true },
        { position: 1, text: 'False', isCorrect: false },
      ],
    };
  return { ...question, type, acceptedAnswer: null, options: choiceOptions() };
};
