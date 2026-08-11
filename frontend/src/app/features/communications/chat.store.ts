import { DestroyRef, effect, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { Subscription } from 'rxjs';
import { ApiProblem } from '../../core/api/api-problem';
import { CommunicationsApiClient } from '../../core/api/communications-api.client';
import type { Conversation, Message, MessagePage } from '../../core/api/communications-api.types';
import { SessionStore } from '../../core/auth/session.store';
import { ConnectivityStore } from '../../core/pwa/connectivity.store';
import { CommunicationsRealtimeService } from '../../core/realtime/communications-realtime.service';

export type ChatListStatus =
  'idle' | 'loading' | 'loadingMore' | 'success' | 'empty' | 'offline' | 'error';

export interface ChatListState {
  readonly status: ChatListStatus;
  readonly items: readonly Conversation[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
  readonly errorCode: string | null;
}

export type ChatMessageItem = SentChatMessage | PendingChatMessage;

export interface SentChatMessage extends Message {
  readonly delivery: 'sent';
}

export interface PendingChatMessage {
  readonly id: null;
  readonly conversationId: string;
  readonly senderUserId: string;
  readonly senderName: string;
  readonly clientMessageId: string;
  readonly sequence: null;
  readonly body: string;
  readonly createdAt: string;
  readonly delivery: 'pending' | 'failed';
  readonly idempotencyKey: string;
  readonly errorCode: string | null;
}

export type ChatThreadStatus =
  'idle' | 'loading' | 'loadingOlder' | 'resyncing' | 'success' | 'empty' | 'offline' | 'error';

export interface ChatThreadState {
  readonly status: ChatThreadStatus;
  readonly conversationId: string | null;
  readonly messages: readonly ChatMessageItem[];
  readonly nextCursor: string | null;
  readonly hasMore: boolean;
  readonly latestSequence: number;
  readonly errorCode: string | null;
}

export interface ChatLeaveState {
  readonly status: 'idle' | 'leaving' | 'offline' | 'error';
  readonly errorCode: string | null;
}

const emptyList = (): ChatListState => ({
  status: 'idle',
  items: [],
  nextCursor: null,
  hasMore: false,
  errorCode: null,
});

const emptyThread = (): ChatThreadState => ({
  status: 'idle',
  conversationId: null,
  messages: [],
  nextCursor: null,
  hasMore: false,
  latestSequence: 0,
  errorCode: null,
});

@Injectable()
export class ChatStore {
  private readonly api = inject(CommunicationsApiClient);
  private readonly connectivity = inject(ConnectivityStore);
  private readonly destroyRef = inject(DestroyRef);
  private readonly realtime = inject(CommunicationsRealtimeService);
  private readonly session = inject(SessionStore);
  private readonly listState = signal<ChatListState>(emptyList());
  private readonly threadState = signal<ChatThreadState>(emptyThread());
  private readonly leaveState = signal<ChatLeaveState>({ status: 'idle', errorCode: null });
  private readonly leftConversationIdState = signal<string | null>(null);
  private listRequest: Subscription | null = null;
  private threadRequest: Subscription | null = null;
  private listVersion = 0;
  private threadVersion = 0;
  private accountUserId = this.session.identity()?.userId ?? null;
  private resyncing = false;
  private resyncQueued = false;

  readonly conversations = this.listState.asReadonly();
  readonly thread = this.threadState.asReadonly();
  readonly leave = this.leaveState.asReadonly();
  readonly leftConversationId = this.leftConversationIdState.asReadonly();

  constructor() {
    effect(() => {
      const userId = this.session.isAuthenticated()
        ? (this.session.identity()?.userId ?? null)
        : null;
      if (userId === this.accountUserId) return;
      this.accountUserId = userId;
      this.clear();
    });
    this.realtime.events$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      if (event.eventType === 'communication.message-created') {
        this.loadConversations();
        if (event.payload.conversationId === this.threadState().conversationId) this.resyncThread();
        return;
      }
      if (event.eventType === 'communication.conversation-left') {
        if (event.payload.userId === this.session.identity()?.userId) {
          this.removeConversation(event.payload.conversationId);
        } else {
          this.loadConversations();
        }
        return;
      }
      if (event.eventType === 'communication.conversation-created') this.loadConversations();
    });
    this.realtime.resync$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.loadConversations();
      this.resyncThread();
    });
  }

  loadConversations(cursor: string | null = null): void {
    const current = this.listState();
    const continuing = cursor !== null;
    if (continuing && (!current.hasMore || current.status === 'loadingMore')) return;
    if (!this.connectivity.isOnline()) {
      this.listState.update((state) => ({ ...state, status: 'offline', errorCode: null }));
      return;
    }
    this.listRequest?.unsubscribe();
    const version = ++this.listVersion;
    this.listState.set({
      ...current,
      status: continuing ? 'loadingMore' : 'loading',
      items: continuing ? current.items : [],
      nextCursor: continuing ? current.nextCursor : null,
      hasMore: continuing && current.hasMore,
      errorCode: null,
    });
    this.listRequest = this.api
      .getConversations(20, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (version !== this.listVersion) return;
          const items = mergeConversations(continuing ? this.listState().items : [], page.items);
          this.listState.set({
            status: items.length === 0 ? 'empty' : 'success',
            items,
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
            errorCode: null,
          });
        },
        error: (error: unknown) => {
          if (version !== this.listVersion) return;
          if (problemCode(error) === 'CURSOR.INVALID' && cursor !== null) {
            this.loadConversations();
            return;
          }
          this.listState.update((state) => ({
            ...state,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
        },
      });
  }

  loadMoreConversations(): void {
    const state = this.listState();
    if (state.nextCursor !== null) this.loadConversations(state.nextCursor);
  }

  openThread(conversationId: string): void {
    this.threadRequest?.unsubscribe();
    const version = ++this.threadVersion;
    this.resyncing = false;
    this.resyncQueued = false;
    this.leftConversationIdState.set(null);
    this.leaveState.set({ status: 'idle', errorCode: null });
    this.threadState.set({ ...emptyThread(), status: 'loading', conversationId });
    if (this.listState().status === 'idle') this.loadConversations();
    if (!this.connectivity.isOnline()) {
      this.threadState.update((state) => ({ ...state, status: 'offline' }));
      return;
    }
    this.threadRequest = this.api
      .getMessages(conversationId, 50)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (
            version !== this.threadVersion ||
            this.threadState().conversationId !== conversationId
          )
            return;
          const messages = mergeChatMessages(
            this.threadState().messages,
            page.items.map(toSentMessage),
          );
          this.threadState.set({
            status: messages.length === 0 ? 'empty' : 'success',
            conversationId,
            messages,
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
            latestSequence: page.latestSequence,
            errorCode: null,
          });
          if (this.resyncQueued) this.resyncThread();
        },
        error: (error: unknown) => {
          if (version !== this.threadVersion) return;
          this.threadState.update((state) => ({
            ...state,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
        },
      });
  }

  loadOlderMessages(): void {
    const state = this.threadState();
    if (
      state.conversationId === null ||
      state.nextCursor === null ||
      !state.hasMore ||
      state.status === 'loadingOlder'
    ) {
      return;
    }
    if (!this.connectivity.isOnline()) {
      this.threadState.update((current) => ({ ...current, status: 'offline' }));
      return;
    }
    const conversationId = state.conversationId;
    const cursor = state.nextCursor;
    const version = ++this.threadVersion;
    this.threadState.update((current) => ({ ...current, status: 'loadingOlder' }));
    this.threadRequest = this.api
      .getMessages(conversationId, 50, cursor)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page) => {
          if (version !== this.threadVersion) return;
          const messages = mergeChatMessages(
            this.threadState().messages,
            page.items.map(toSentMessage),
          );
          this.threadState.update((current) => ({
            ...current,
            status: messages.length === 0 ? 'empty' : 'success',
            messages,
            nextCursor: page.nextCursor,
            hasMore: page.hasMore,
            latestSequence: Math.max(current.latestSequence, page.latestSequence),
            errorCode: null,
          }));
        },
        error: (error: unknown) => {
          if (version !== this.threadVersion) return;
          if (problemCode(error) === 'CURSOR.INVALID') {
            this.openThread(conversationId);
            return;
          }
          this.threadState.update((current) => ({
            ...current,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
        },
      });
  }

  sendMessage(body: string): void {
    const normalized = body.trim();
    const thread = this.threadState();
    const identity = this.session.identity();
    if (
      thread.conversationId === null ||
      identity === null ||
      normalized.length === 0 ||
      normalized.length > 5000
    ) {
      return;
    }
    const pending: PendingChatMessage = {
      id: null,
      conversationId: thread.conversationId,
      senderUserId: identity.userId,
      senderName: identity.displayName,
      clientMessageId: globalThis.crypto.randomUUID(),
      sequence: null,
      body: normalized,
      createdAt: new Date().toISOString(),
      delivery: this.connectivity.isOnline() ? 'pending' : 'failed',
      idempotencyKey: globalThis.crypto.randomUUID(),
      errorCode: this.connectivity.isOnline() ? null : 'HTTP.0',
    };
    this.threadState.update((current) => ({
      ...current,
      status: 'success',
      messages: mergeChatMessages(current.messages, [pending]),
    }));
    if (pending.delivery === 'pending') this.dispatchMessage(pending);
  }

  retryMessage(clientMessageId: string): void {
    const pending = this.threadState().messages.find(
      (message): message is PendingChatMessage =>
        message.delivery === 'failed' && message.clientMessageId === clientMessageId,
    );
    if (!pending || !this.connectivity.isOnline()) return;
    const retry = { ...pending, delivery: 'pending' as const, errorCode: null };
    this.threadState.update((state) => ({
      ...state,
      messages: state.messages.map((message) =>
        message.clientMessageId === clientMessageId ? retry : message,
      ),
    }));
    this.dispatchMessage(retry);
  }

  leaveConversation(conversationId: string): void {
    if (this.leaveState().status === 'leaving') return;
    if (!this.connectivity.isOnline()) {
      this.leaveState.set({ status: 'offline', errorCode: 'HTTP.0' });
      return;
    }
    this.leaveState.set({ status: 'leaving', errorCode: null });
    this.api
      .leaveConversation(conversationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.removeConversation(conversationId);
          this.leftConversationIdState.set(conversationId);
          this.leaveState.set({ status: 'idle', errorCode: null });
        },
        error: (error: unknown) => {
          this.leaveState.set({
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          });
        },
      });
  }

  resyncThread(): void {
    const state = this.threadState();
    if (state.conversationId === null) return;
    if (state.status === 'loading') {
      this.resyncQueued = true;
      return;
    }
    if (this.resyncing) {
      this.resyncQueued = true;
      return;
    }
    if (!this.connectivity.isOnline()) {
      this.threadState.update((current) => ({ ...current, status: 'offline' }));
      return;
    }
    this.resyncing = true;
    this.resyncQueued = false;
    const version = ++this.threadVersion;
    const conversationId = state.conversationId;
    const afterSequence = state.latestSequence;
    this.threadState.update((current) => ({ ...current, status: 'resyncing', errorCode: null }));
    this.loadThreadResyncPage(conversationId, afterSequence, null, [], version);
  }

  private loadThreadResyncPage(
    conversationId: string,
    afterSequence: number,
    cursor: string | null,
    incoming: readonly SentChatMessage[],
    version: number,
  ): void {
    this.threadRequest = this.api
      .getMessages(conversationId, 100, cursor, afterSequence)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (page: MessagePage) => {
          if (version !== this.threadVersion) return;
          const accumulated = mergeSentMessages(incoming, page.items.map(toSentMessage));
          if (page.hasMore && page.nextCursor !== null) {
            this.loadThreadResyncPage(
              conversationId,
              afterSequence,
              page.nextCursor,
              accumulated,
              version,
            );
            return;
          }
          this.threadState.update((current) => {
            const messages = mergeChatMessages(current.messages, accumulated);
            return {
              ...current,
              status: messages.length === 0 ? 'empty' : 'success',
              messages,
              latestSequence: Math.max(current.latestSequence, page.latestSequence),
              errorCode: null,
            };
          });
          this.completeThreadResync();
        },
        error: (error: unknown) => {
          if (version !== this.threadVersion) return;
          if (problemCode(error) === 'CURSOR.INVALID' && cursor !== null) {
            this.loadThreadResyncPage(conversationId, afterSequence, null, [], version);
            return;
          }
          this.threadState.update((current) => ({
            ...current,
            status: isOffline(error) ? 'offline' : 'error',
            errorCode: problemCode(error),
          }));
          this.completeThreadResync();
        },
      });
  }

  private dispatchMessage(pending: PendingChatMessage): void {
    this.api
      .createMessage(
        pending.conversationId,
        { clientMessageId: pending.clientMessageId, body: pending.body },
        pending.idempotencyKey,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (message) => {
          if (this.threadState().conversationId !== pending.conversationId) return;
          this.threadState.update((state) => ({
            ...state,
            messages: mergeChatMessages(state.messages, [toSentMessage(message)]),
            latestSequence: Math.max(state.latestSequence, message.sequence),
          }));
          this.loadConversations();
        },
        error: (error: unknown) => {
          this.threadState.update((state) => ({
            ...state,
            messages: state.messages.map((message) =>
              message.delivery === 'pending' && message.clientMessageId === pending.clientMessageId
                ? { ...message, delivery: 'failed', errorCode: problemCode(error) }
                : message,
            ),
          }));
        },
      });
  }

  private completeThreadResync(): void {
    this.resyncing = false;
    if (this.resyncQueued) this.resyncThread();
  }

  private removeConversation(conversationId: string): void {
    this.listState.update((state) => {
      const items = state.items.filter((item) => item.id !== conversationId);
      return { ...state, items, status: items.length === 0 ? 'empty' : state.status };
    });
    if (this.threadState().conversationId === conversationId) this.threadState.set(emptyThread());
  }

  private clear(): void {
    this.listVersion++;
    this.threadVersion++;
    this.listRequest?.unsubscribe();
    this.threadRequest?.unsubscribe();
    this.listState.set(emptyList());
    this.threadState.set(emptyThread());
    this.leaveState.set({ status: 'idle', errorCode: null });
    this.leftConversationIdState.set(null);
  }
}

const toSentMessage = (message: Message): SentChatMessage => ({ ...message, delivery: 'sent' });

const mergeConversations = (
  current: readonly Conversation[],
  incoming: readonly Conversation[],
): readonly Conversation[] => {
  const byId = new Map(current.map((item) => [item.id, item]));
  for (const item of incoming) byId.set(item.id, item);
  return [...byId.values()].sort(
    (left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt),
  );
};

const mergeSentMessages = (
  current: readonly SentChatMessage[],
  incoming: readonly SentChatMessage[],
): readonly SentChatMessage[] =>
  mergeChatMessages(current, incoming).filter(
    (message): message is SentChatMessage => message.delivery === 'sent',
  );

const mergeChatMessages = (
  current: readonly ChatMessageItem[],
  incoming: readonly ChatMessageItem[],
): readonly ChatMessageItem[] => {
  const messages = [...current];
  for (const item of incoming) {
    const existingIndex = messages.findIndex(
      (candidate) =>
        (item.id !== null && candidate.id === item.id) ||
        candidate.clientMessageId === item.clientMessageId,
    );
    if (existingIndex >= 0) messages[existingIndex] = item;
    else messages.push(item);
  }
  return messages.sort((left, right) => {
    if (left.sequence !== null && right.sequence !== null) return left.sequence - right.sequence;
    if (left.sequence !== null) return -1;
    if (right.sequence !== null) return 1;
    return Date.parse(left.createdAt) - Date.parse(right.createdAt);
  });
};

const isOffline = (error: unknown): boolean => error instanceof ApiProblem && error.status === 0;
const problemCode = (error: unknown): string | null =>
  error instanceof ApiProblem ? error.code : null;
