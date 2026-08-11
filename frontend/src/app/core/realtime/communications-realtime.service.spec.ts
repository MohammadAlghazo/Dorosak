import { parseCommunicationEvent } from './communications-realtime.service';

describe('parseCommunicationEvent', () => {
  it('accepts a metadata-only message event', () => {
    const event = parseCommunicationEvent({
      eventId: 'event-1',
      eventType: 'communication.message-created',
      schemaVersion: 1,
      occurredAt: '2030-01-01T00:00:00Z',
      payload: {
        messageId: 'message-1',
        conversationId: 'conversation-1',
        senderUserId: 'user-1',
        sequence: 4,
        body: 'This field must be discarded.',
      },
    });

    expect(event?.eventType).toBe('communication.message-created');
    expect(event && 'body' in event.payload).toBe(false);
  });

  it('rejects unsupported schemas and malformed payloads', () => {
    expect(
      parseCommunicationEvent({
        eventId: 'event-1',
        eventType: 'communication.message-created',
        schemaVersion: 2,
        occurredAt: '2030-01-01T00:00:00Z',
        payload: {},
      }),
    ).toBeNull();
    expect(
      parseCommunicationEvent({
        eventId: 'event-2',
        eventType: 'communication.message-created',
        schemaVersion: 1,
        occurredAt: '2030-01-01T00:00:00Z',
        payload: { body: 'A body must never be treated as event metadata.' },
      }),
    ).toBeNull();
  });
});
