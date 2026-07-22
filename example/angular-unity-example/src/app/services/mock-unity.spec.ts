import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createProjectMockUnityInstance } from './mock-unity';

/* eslint-disable @typescript-eslint/no-explicit-any */
const win = window as any;

describe('createProjectMockUnityInstance', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('simulates a selection event after LoadObject', () => {
    const selectedCb = vi.fn();
    win['sendSelectedObjectFromUnity'] = selectedCb;
    const mock = createProjectMockUnityInstance();

    mock.SendMessage('SceneManager', 'LoadObject', 'Cube-001');
    vi.advanceTimersByTime(300);

    expect(selectedCb).toHaveBeenCalledWith('Cube-001');
  });

  it('simulates the ResetScene event sequence', () => {
    const listCb = vi.fn();
    const readyCb = vi.fn();
    const requestCb = vi.fn();
    win['sendObjectsListFromUnity'] = listCb;
    win['sendSceneReadyFromUnity'] = readyCb;
    win['requestDataFromWebFromUnity'] = requestCb;
    const mock = createProjectMockUnityInstance();

    mock.SendMessage('SceneManager', 'ResetScene');

    vi.advanceTimersByTime(300);
    expect(listCb).toHaveBeenCalledWith('Cube-001|Sphere-002|Cylinder-003|Plane-004');
    expect(readyCb).not.toHaveBeenCalled();

    vi.advanceTimersByTime(300);
    expect(readyCb).toHaveBeenCalledOnce();
    expect(requestCb).toHaveBeenCalledOnce();
    expect(requestCb.mock.calls[0][0]).toBe('scene-reset');
  });

  it('adds a spawned object and publishes the JSON scene state', () => {
    const listCb = vi.fn();
    const stateCb = vi.fn();
    win['sendObjectsListFromUnity'] = listCb;
    win['sendSceneStateFromUnity'] = stateCb;
    const mock = createProjectMockUnityInstance();

    mock.SendMessage(
      'SceneManager',
      'SpawnFromJson',
      JSON.stringify({ objectId: 'Spawned-001', x: 0, y: 2, z: 0, colorHex: '#00ff00' }),
    );
    vi.advanceTimersByTime(300);

    expect(listCb).toHaveBeenCalledWith('Cube-001|Sphere-002|Cylinder-003|Plane-004|Spawned-001');
    expect(stateCb).toHaveBeenCalledOnce();
    expect(JSON.parse(stateCb.mock.calls[0][0])).toEqual({
      selectedObjectId: '',
      objectCount: 5,
      visible: true,
    });
  });
});
