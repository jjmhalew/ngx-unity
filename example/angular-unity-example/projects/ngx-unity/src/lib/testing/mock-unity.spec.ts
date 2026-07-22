import { describe, expect, it, vi } from 'vitest';
import { createMockUnityInstance } from './mock-unity';

describe('createMockUnityInstance', () => {
  it('returns an object implementing IUnityInstance', () => {
    const mock = createMockUnityInstance();

    expect(typeof mock.SendMessage).toBe('function');
    expect(typeof mock.SetFullscreen).toBe('function');
    expect(typeof mock.Quit).toBe('function');
  });

  it('forwards SendMessage calls to onSendMessage', () => {
    const onSendMessage = vi.fn();
    const mock = createMockUnityInstance({ onSendMessage });

    mock.SendMessage('SceneManager', 'LoadObject', 'Cube-001');

    expect(onSendMessage).toHaveBeenCalledExactlyOnceWith('SceneManager', 'LoadObject', 'Cube-001');
  });

  it('passes undefined data through to onSendMessage when omitted', () => {
    const onSendMessage = vi.fn();
    const mock = createMockUnityInstance({ onSendMessage });

    mock.SendMessage('SceneManager', 'ResetScene');

    expect(onSendMessage).toHaveBeenCalledExactlyOnceWith('SceneManager', 'ResetScene', undefined);
  });

  it('does not throw when created without options', () => {
    const mock = createMockUnityInstance();

    expect(() => mock.SendMessage('SceneManager', 'LoadObject', 'x')).not.toThrow();
    expect(() => mock.SetFullscreen(1)).not.toThrow();
  });

  it('Quit resolves', async () => {
    const mock = createMockUnityInstance();

    await expect(mock.Quit()).resolves.toBeUndefined();
  });
});
