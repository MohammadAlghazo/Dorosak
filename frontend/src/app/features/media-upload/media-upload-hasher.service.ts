import { Injectable } from '@angular/core';
import { createSHA256 } from 'hash-wasm';

export interface MediaFilePartHash {
  partNumber: number;
  size: number;
  sha256: string;
}

export interface MediaFileHashes {
  sha256: string;
  parts: readonly MediaFilePartHash[];
}

@Injectable({ providedIn: 'root' })
export class MediaUploadHasher {
  async hash(
    file: File,
    partSize: number,
    signal: AbortSignal,
    onProgress: (loaded: number) => void,
  ): Promise<MediaFileHashes> {
    const whole = (await createSHA256()).init();
    const parts: MediaFilePartHash[] = [];
    const bytesPerRead = 2 * 1024 * 1024;
    let loaded = 0;
    let partNumber = 1;

    for (let start = 0; start < file.size; start += partSize || file.size) {
      throwIfAborted(signal);
      const end = Math.min(start + (partSize || file.size), file.size);
      const part = (await createSHA256()).init();
      for (let offset = start; offset < end; offset += bytesPerRead) {
        throwIfAborted(signal);
        const bytes = new Uint8Array(
          await file.slice(offset, Math.min(offset + bytesPerRead, end)).arrayBuffer(),
        );
        whole.update(bytes);
        part.update(bytes);
        loaded += bytes.byteLength;
        onProgress(loaded);
        await yieldToBrowser();
      }
      if (partSize > 0) {
        parts.push({ partNumber, size: end - start, sha256: part.digest('hex') });
        partNumber++;
      }
    }

    return { sha256: whole.digest('hex'), parts };
  }
}

const throwIfAborted = (signal: AbortSignal): void => {
  if (signal.aborted) throw new DOMException('Upload was aborted.', 'AbortError');
};

const yieldToBrowser = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0));
