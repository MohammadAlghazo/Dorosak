import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { InstructorApiClient } from '../../core/api/instructor-api.client';
import type { CourseMetadataRequest } from '../../core/api/phase6-api.types';
import { CourseEditorStore, isVersionConflict } from './course-editor.store';

describe('CourseEditorStore', () => {
  it('holds local metadata after 412 until the user explicitly reloads', () => {
    const getCourse = vi
      .fn()
      .mockReturnValueOnce(of({ value: course(1, 'Original'), etag: '"v1"' }))
      .mockReturnValueOnce(of({ value: course(2, 'Server edit'), etag: '"v2"' }));
    const updateCourseMetadata = vi.fn(() =>
      throwError(
        () =>
          new ApiProblem(
            412,
            'COURSE.VERSION_CONFLICT',
            null,
            null,
            null,
            {},
            'Stale draft',
            '"v2"',
          ),
      ),
    );
    TestBed.configureTestingModule({
      providers: [
        CourseEditorStore,
        { provide: InstructorApiClient, useValue: { getCourse, updateCourseMetadata } },
      ],
    });
    const store = TestBed.inject(CourseEditorStore);

    store.loadCourse('course-1');
    store.saveMetadata('course-1', metadata);
    expect(updateCourseMetadata).toHaveBeenCalledWith('course-1', metadata, '"v1"');
    expect(store.course()).toMatchObject({
      status: 'conflict',
      etag: '"v1"',
      conflictEtag: '"v2"',
    });
    expect(store.course().value?.localizations[0]?.title).toBe('Original');

    store.loadCourse('course-1');
    expect(store.course()).toMatchObject({ status: 'success', etag: '"v2"' });
    expect(store.course().value?.localizations[0]?.title).toBe('Server edit');
  });

  it('recognizes only the stable backend version conflict contract', () => {
    expect(
      isVersionConflict(
        new ApiProblem(412, 'COURSE.VERSION_CONFLICT', null, null, null, {}, 'Stale', '"v3"'),
      ),
    ).toBe(true);
    expect(isVersionConflict(new Error('conflict'))).toBe(false);
  });

  it('queues the latest metadata while an autosave is in flight', () => {
    const firstSave = new Subject<{
      value: { courseId: string; status: 'Draft'; draftVersion: number };
      etag: string;
    }>();
    const secondSave = new Subject<{
      value: { courseId: string; status: 'Draft'; draftVersion: number };
      etag: string;
    }>();
    const getCourse = vi
      .fn()
      .mockReturnValueOnce(of({ value: course(1, 'Original'), etag: '"v1"' }))
      .mockReturnValueOnce(of({ value: course(3, 'Latest'), etag: '"v3"' }));
    const updateCourseMetadata = vi
      .fn()
      .mockReturnValueOnce(firstSave.asObservable())
      .mockReturnValueOnce(secondSave.asObservable());
    TestBed.configureTestingModule({
      providers: [
        CourseEditorStore,
        { provide: InstructorApiClient, useValue: { getCourse, updateCourseMetadata } },
      ],
    });
    const store = TestBed.inject(CourseEditorStore);
    const baseLocalization = metadata.localizations[0];
    if (baseLocalization === undefined)
      throw new Error('Test metadata must include a localization.');
    const laterMetadata: CourseMetadataRequest = {
      ...metadata,
      localizations: [{ ...baseLocalization, title: 'Queued edit' }],
    };

    store.loadCourse('course-1');
    store.saveMetadata('course-1', metadata);
    store.saveMetadata('course-1', laterMetadata);
    firstSave.next({
      value: { courseId: 'course-1', status: 'Draft', draftVersion: 2 },
      etag: '"v2"',
    });
    firstSave.complete();

    expect(updateCourseMetadata).toHaveBeenNthCalledWith(2, 'course-1', laterMetadata, '"v2"');
    secondSave.next({
      value: { courseId: 'course-1', status: 'Draft', draftVersion: 3 },
      etag: '"v3"',
    });
    secondSave.complete();

    expect(store.course()).toMatchObject({ status: 'success', etag: '"v3"' });
    expect(store.course().value?.localizations[0]?.title).toBe('Latest');
  });
});

const metadata: CourseMetadataRequest = {
  defaultLocale: 'en',
  level: 'Beginner',
  localizations: [
    {
      locale: 'en',
      title: 'Local edit',
      subtitle: '',
      description: 'Description',
      slug: 'local-edit',
    },
  ],
  categoryCodes: ['technology'],
  tagCodes: [],
};

const course = (draftVersion: number, title: string) => ({
  id: 'course-1',
  ownerUserId: 'user-1',
  defaultLocale: 'en' as const,
  status: 'Draft' as const,
  draftVersion,
  level: 'Beginner' as const,
  categoryCodes: ['technology'],
  tagCodes: [],
  localizations: [
    {
      locale: 'en' as const,
      title,
      subtitle: '',
      description: 'Description',
      slug: title.toLowerCase().replace(' ', '-'),
    },
  ],
  collaborators: [],
  createdAt: '2030-01-01T00:00:00Z',
  updatedAt: '2030-01-01T00:00:00Z',
});
