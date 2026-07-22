import { TestBed } from '@angular/core/testing';
import { createMockUnityInstance } from 'ngx-unity';
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest';
import { UnityClient } from '../generated/unity-client';
import { UnityBridgeService } from './unity-bridge.service';

/* eslint-disable @typescript-eslint/no-explicit-any */
const win = window as any;

describe('UnityBridgeService', () => {
  let service: UnityBridgeService;
  let sendMessage: Mock<(gameObjectName: string, methodName: string, data?: unknown) => void>;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [UnityClient] });
    service = TestBed.inject(UnityBridgeService);
    sendMessage = vi.fn();
  });

  function connect(): void {
    service.setUnityInstance(
      createMockUnityInstance({
        onSendMessage: (gameObjectName, methodName, data) => sendMessage(gameObjectName, methodName, data),
      }),
    );
  }

  it('reports isConnected only after an instance is set', () => {
    expect(service.isConnected()).toBe(false);
    connect();
    expect(service.isConnected()).toBe(true);
  });

  it('does not throw when methods are called without an instance', () => {
    expect(() => service.loadObject('Cube-001')).not.toThrow();
    expect(() => service.resetScene()).not.toThrow();
    expect(sendMessage).not.toHaveBeenCalled();
  });

  it('forwards commands to Unity via SendMessage with exact arguments', () => {
    connect();

    service.loadObject('Cube-001');
    expect(sendMessage).toHaveBeenLastCalledWith('SceneManager', 'LoadObject', 'Cube-001');

    service.setColor('#ff6600');
    expect(sendMessage).toHaveBeenLastCalledWith('SceneManager', 'SetColor', '#ff6600');

    service.toggleVisibility();
    expect(sendMessage).toHaveBeenLastCalledWith('SceneManager', 'ToggleVisibility', undefined);

    service.resetScene();
    expect(sendMessage).toHaveBeenLastCalledWith('SceneManager', 'ResetScene', undefined);
  });

  it('serializes spawn requests to JSON', () => {
    connect();

    service.spawnObject({ objectId: 'Spawned-001', x: 1, y: 2, z: 0, colorHex: '#00ff00' });

    expect(sendMessage).toHaveBeenLastCalledWith(
      'SceneManager',
      'SpawnFromJson',
      '{"objectId":"Spawned-001","x":1,"y":2,"z":0,"colorHex":"#00ff00"}',
    );
  });

  it('updates computed signals when Unity events arrive', () => {
    win['sendObjectsListFromUnity']('Cube-001|Sphere-002');
    expect(service.objectCount()).toBe(2);

    win['sendObjectsListFromUnity']('');
    expect(service.objectCount()).toBe(0);

    win['sendSelectedObjectFromUnity']('Cube-001');
    expect(service.hasSelection()).toBe(true);
  });

  it('answers Unity data requests via the constructor-registered handler', () => {
    const respond = vi.fn();

    win['requestDataFromWebFromUnity']('telemetry', respond);

    expect(respond).toHaveBeenCalledExactlyOnceWith('Result for: telemetry');
  });

  it('forwards navigation changes to the Unity-registered callback', () => {
    const unityCallback = vi.fn();
    win['registerOnNavigationChangedFromUnity'](unityCallback);

    service.sendNavigationChange('/dashboard');

    expect(unityCallback).toHaveBeenCalledWith('/dashboard');
  });
});
