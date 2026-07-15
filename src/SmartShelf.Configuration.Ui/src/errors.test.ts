import { describe, expect, it } from 'vitest';
import { configurationSaveErrorMessage } from './errors';

describe('configuration conflict handling', () => {
  it('requires a reload after an optimistic-concurrency conflict', () => {
    const error = Object.assign(new Error('Conflict'), { status: 409 });

    expect(configurationSaveErrorMessage(error)).toContain('Reload before saving again');
  });
});
