import { createMockUnityInstance, type IUnityInstance } from 'ngx-unity';
import type { SceneState } from '../generated/unity-jslib-exported.service';

const MOCK_OBJECTS = ['Cube-001', 'Sphere-002', 'Cylinder-003', 'Plane-004'];

/**
 * Creates a project-specific mock Unity instance.
 *
 * Extends the library's base mock with simulated window callbacks that
 * mimic what the Unity WebGL/WebGPU build would do via BrowserInteractions.jslib.
 */
export function createProjectMockUnityInstance(): IUnityInstance {
  const objects = [...MOCK_OBJECTS];
  let selectedObjectId = '';
  let visible = true;

  /* eslint-disable @typescript-eslint/no-explicit-any */
  const win = window as any;

  const sendSceneState = (): void => {
    const state: SceneState = { selectedObjectId, objectCount: objects.length, visible };
    const stateCb = win['sendSceneStateFromUnity'] as ((json: string) => void) | undefined;
    stateCb?.(JSON.stringify(state));
  };

  return createMockUnityInstance({
    onSendMessage(_gameObjectName, methodName, data) {
      if (methodName === 'LoadObject') {
        selectedObjectId = data as string;
        setTimeout(() => {
          const cb = win['sendSelectedObjectFromUnity'] as ((id: string) => void) | undefined;
          cb?.(data as string);
          sendSceneState();
        }, 300);
      }

      if (methodName === 'ToggleVisibility') {
        visible = !visible;
        setTimeout(() => sendSceneState(), 100);
      }

      if (methodName === 'SpawnFromJson') {
        const request = JSON.parse(data as string) as { objectId: string };
        objects.push(request.objectId);
        setTimeout(() => {
          const listCb = win['sendObjectsListFromUnity'] as ((ids: string) => void) | undefined;
          listCb?.(objects.join('|'));
          sendSceneState();
        }, 300);
      }

      if (methodName === 'ResetScene') {
        objects.length = 0;
        objects.push(...MOCK_OBJECTS);
        selectedObjectId = '';
        visible = true;
        setTimeout(() => {
          const listCb = win['sendObjectsListFromUnity'] as ((ids: string) => void) | undefined;
          listCb?.(objects.join('|'));
        }, 300);
        setTimeout(() => {
          const readyCb = win['sendSceneReadyFromUnity'] as (() => void) | undefined;
          readyCb?.();
          sendSceneState();
        }, 500);
        setTimeout(() => {
          const reqCb = win['requestDataFromWebFromUnity'] as
            | ((query: string, respond: (result: string) => void) => void)
            | undefined;
          reqCb?.('scene-reset', (result: string) => {
            console.log(`[Unity Mock] Received callback response: ${result}`);
          });
        }, 600);
      }
    },
  });
}
