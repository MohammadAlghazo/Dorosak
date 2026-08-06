import { Injectable, signal } from '@angular/core';

export interface ToastMessage {
  id: number;
  message: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private readonly activeMessages = signal<readonly ToastMessage[]>([]);
  private nextId = 1;

  readonly messages = this.activeMessages.asReadonly();

  announce(message: string): void {
    const toast = { id: this.nextId++, message };
    this.activeMessages.update((messages) => [...messages, toast]);
    setTimeout(() => this.dismiss(toast.id), 5000);
  }

  dismiss(id: number): void {
    this.activeMessages.update((messages) => messages.filter((message) => message.id !== id));
  }
}
