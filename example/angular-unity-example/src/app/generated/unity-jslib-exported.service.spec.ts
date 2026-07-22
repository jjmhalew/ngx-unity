import { describe, expect, it, vi } from 'vitest';
import { UnityJSLibExportedService } from './unity-jslib-exported.service';

/* eslint-disable @typescript-eslint/no-explicit-any */
const win = window as any;

/**
 * These specs pin the output contract of the JSLibExport.cs generator by testing
 * the committed generated file. The module-scope signals are singletons shared
 * across the spec file, so assertions are sequential within each test.
 */
describe('UnityJSLibExportedService (generated)', () => {
  const service = new UnityJSLibExportedService();

  it('registers the window callbacks used by the jslib', () => {
    expect(typeof win['sendSelectedObjectFromUnity']).toBe('function');
    expect(typeof win['sendSceneReadyFromUnity']).toBe('function');
    expect(typeof win['sendObjectsListFromUnity']).toBe('function');
    expect(typeof win['sendSceneStateFromUnity']).toBe('function');
    expect(typeof win['requestDataFromWebFromUnity']).toBe('function');
    expect(typeof win['registerOnNavigationChangedFromUnity']).toBe('function');
  });

  it('updates the string signal when Unity sends a selected object', () => {
    win['sendSelectedObjectFromUnity']('Cube-001');
    expect(service.sendSelectedObject()).toBe('Cube-001');

    win['sendSelectedObjectFromUnity']('Sphere-002');
    expect(service.sendSelectedObject()).toBe('Sphere-002');
  });

  it('splits pipe-delimited lists and maps the empty string to an empty array', () => {
    win['sendObjectsListFromUnity']('a|b|c');
    expect(service.sendObjectsList()).toEqual(['a', 'b', 'c']);

    win['sendObjectsListFromUnity']('single');
    expect(service.sendObjectsList()).toEqual(['single']);

    win['sendObjectsListFromUnity']('');
    expect(service.sendObjectsList()).toEqual([]);
  });

  it('increments the counter signal on each void event', () => {
    const before = service.sendSceneReady();
    win['sendSceneReadyFromUnity']();
    win['sendSceneReadyFromUnity']();
    expect(service.sendSceneReady()).toBe(before + 2);
  });

  it('parses JSON payloads into the typed signal and ignores malformed JSON', () => {
    win['sendSceneStateFromUnity']('{"selectedObjectId":"Cube-001","objectCount":4,"visible":true}');
    expect(service.sendSceneState()).toEqual({
      selectedObjectId: 'Cube-001',
      objectCount: 4,
      visible: true,
    });

    const errorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
    win['sendSceneStateFromUnity']('not-json');
    expect(service.sendSceneState()).toEqual({
      selectedObjectId: 'Cube-001',
      objectCount: 4,
      visible: true,
    });
    expect(errorSpy).toHaveBeenCalledOnce();
    errorSpy.mockRestore();
  });

  it('routes events to per-instance channels via forInstance', () => {
    win['sendSelectedObjectFromUnity']('From-A', 'canvas-a');
    win['sendSelectedObjectFromUnity']('From-B', 'canvas-b');

    expect(service.forInstance('canvas-a').sendSelectedObject()).toBe('From-A');
    expect(service.forInstance('canvas-b').sendSelectedObject()).toBe('From-B');
    // Flat signal keeps last-event-from-any-instance semantics.
    expect(service.sendSelectedObject()).toBe('From-B');
  });

  it('routes calls without an instanceId to the default channel', () => {
    win['sendSelectedObjectFromUnity']('legacy-call');
    expect(service.forInstance('default').sendSelectedObject()).toBe('legacy-call');
  });

  it('invokes the registered request-response handler and forwards the response', () => {
    const respond = vi.fn();
    service.registerRequestDataFromWebHandler((query, respondFn) => {
      respondFn(`Result for: ${query}`);
    });

    win['requestDataFromWebFromUnity']('scene-reset', respond);

    expect(respond).toHaveBeenCalledExactlyOnceWith('Result for: scene-reset');
  });

  it('prefers a per-instance request handler over the flat handler', () => {
    const flatRespond = vi.fn();
    const channelRespond = vi.fn();
    service.registerRequestDataFromWebHandler((_query, respondFn) => respondFn('flat'));
    service.forInstance('canvas-rr').registerRequestDataFromWebHandler((_query, respondFn) => respondFn('channel'));

    win['requestDataFromWebFromUnity']('q', channelRespond, 'canvas-rr');
    win['requestDataFromWebFromUnity']('q', flatRespond, 'canvas-other');

    expect(channelRespond).toHaveBeenCalledExactlyOnceWith('channel');
    expect(flatRespond).toHaveBeenCalledExactlyOnceWith('flat');
  });

  it('broadcasts notifications to registered Unity callbacks', () => {
    // Safe no-op before registration.
    expect(() => service.notifyOnNavigationChanged('/nowhere')).not.toThrow();

    const unityCallback = vi.fn();
    win['registerOnNavigationChangedFromUnity'](unityCallback, 'canvas-nav');

    service.notifyOnNavigationChanged('/home');
    expect(unityCallback).toHaveBeenCalledWith('/home');

    service.forInstance('canvas-nav').notifyOnNavigationChanged('/direct');
    expect(unityCallback).toHaveBeenCalledWith('/direct');
  });
});
