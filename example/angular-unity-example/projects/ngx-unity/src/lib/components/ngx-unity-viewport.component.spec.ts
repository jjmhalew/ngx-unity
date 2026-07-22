import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { IUnityInstance } from '../models/unity-instance';
import { NgxUnityViewport } from './ngx-unity-viewport.component';

/**
 * The component caches loader promises and canvas ids in module scope, so each
 * test that exercises the real-load path uses a unique buildPath and canvas-id
 * assertions are relative (never exact).
 */
describe('NgxUnityViewport', () => {
  function createSpyInstance(): IUnityInstance {
    return {
      SendMessage: vi.fn(),
      SetFullscreen: vi.fn(),
      Quit: vi.fn().mockResolvedValue(undefined),
    };
  }

  /** Stub fetch so HEAD probes succeed only for URLs matching the predicate. */
  function stubFetch(okFor: (url: string) => boolean): void {
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => ({ ok: okFor(url) })),
    );
  }

  /** Wait for the loader <script> to be appended, then fire the given event on it. */
  async function settleLoaderScript(src: string, event: 'load' | 'error'): Promise<void> {
    const script = await vi.waitFor(() => {
      const el = document.querySelector(`script[src="${src}"]`);
      if (!el) throw new Error(`loader script ${src} not yet appended`);
      return el;
    });
    script.dispatchEvent(new Event(event));
  }

  function createViewport(buildPath: string): ComponentFixture<NgxUnityViewport> {
    const fixture = TestBed.createComponent(NgxUnityViewport);
    fixture.componentRef.setInput('buildPath', buildPath);
    return fixture;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    document.querySelectorAll('script').forEach((s) => s.remove());
  });

  describe('mock fallback (no build)', () => {
    it('creates a default mock and emits instanceReady when no build is found', async () => {
      stubFetch(() => false);
      const fixture = createViewport('missing-build');
      const ready = vi.fn();
      fixture.componentInstance.instanceReady.subscribe(ready);

      fixture.detectChanges();

      await vi.waitFor(() => expect(fixture.componentInstance.isLoaded()).toBe(true));
      expect(fixture.componentInstance.useMock()).toBe(true);
      expect(ready).toHaveBeenCalledOnce();
      expect(typeof ready.mock.calls[0][0].SendMessage).toBe('function');
    });

    it('uses the provided mockFactory instead of the default mock', async () => {
      stubFetch(() => false);
      const customMock = createSpyInstance();
      const fixture = createViewport('missing-build-2');
      fixture.componentRef.setInput('mockFactory', () => customMock);
      const ready = vi.fn();
      fixture.componentInstance.instanceReady.subscribe(ready);

      fixture.detectChanges();

      await vi.waitFor(() => expect(ready).toHaveBeenCalledExactlyOnceWith(customMock));
    });

    it('emits instanceCreated with the resolved canvas id', async () => {
      stubFetch(() => false);
      const fixture = createViewport('missing-build-3');
      const created = vi.fn();
      fixture.componentInstance.instanceCreated.subscribe(created);

      fixture.detectChanges();

      await vi.waitFor(() => expect(created).toHaveBeenCalledOnce());
      expect(created.mock.calls[0][0].canvasId).toBe(fixture.componentInstance.resolvedCanvasId());
    });

    it('shows the no-build overlay message', async () => {
      stubFetch(() => false);
      const fixture = createViewport('missing-build-4');

      fixture.detectChanges();
      await vi.waitFor(() => expect(fixture.componentInstance.useMock()).toBe(true));
      fixture.detectChanges();

      const overlay = fixture.nativeElement.querySelector('.overlay');
      expect(overlay.textContent).toContain('No Unity build found');
    });
  });

  describe('real Unity load', () => {
    it('loads Unity and emits the real instance', async () => {
      stubFetch(() => true);
      const instance = createSpyInstance();
      const createUnityInstance = vi.fn().mockResolvedValue(instance);
      vi.stubGlobal('createUnityInstance', createUnityInstance);

      const fixture = createViewport('build-a');
      const ready = vi.fn();
      fixture.componentInstance.instanceReady.subscribe(ready);
      fixture.detectChanges();

      await settleLoaderScript('build-a/Build/build-a.loader.js', 'load');

      await vi.waitFor(() => expect(ready).toHaveBeenCalledExactlyOnceWith(instance));
      expect(fixture.componentInstance.isLoaded()).toBe(true);
      expect(fixture.componentInstance.isLoading()).toBe(false);
      expect(fixture.componentInstance.useMock()).toBe(false);
    });

    it('detects .br compression and builds config URLs accordingly', async () => {
      stubFetch((url) => url.endsWith('.loader.js') || url.endsWith('.br'));
      const createUnityInstance = vi.fn().mockResolvedValue(createSpyInstance());
      vi.stubGlobal('createUnityInstance', createUnityInstance);

      const fixture = createViewport('build-br');
      fixture.detectChanges();
      await settleLoaderScript('build-br/Build/build-br.loader.js', 'load');

      await vi.waitFor(() => expect(createUnityInstance).toHaveBeenCalledOnce());
      const config = createUnityInstance.mock.calls[0][1];
      expect(config.dataUrl).toBe('build-br/Build/build-br.data.br');
      expect(config.frameworkUrl).toBe('build-br/Build/build-br.framework.js.br');
      expect(config.codeUrl).toBe('build-br/Build/build-br.wasm.br');
    });

    it('prefers uncompressed files when both plain and .gz respond ok', async () => {
      stubFetch((url) => !url.endsWith('.br'));
      const createUnityInstance = vi.fn().mockResolvedValue(createSpyInstance());
      vi.stubGlobal('createUnityInstance', createUnityInstance);

      const fixture = createViewport('build-plain');
      fixture.detectChanges();
      await settleLoaderScript('build-plain/Build/build-plain.loader.js', 'load');

      await vi.waitFor(() => expect(createUnityInstance).toHaveBeenCalledOnce());
      const config = createUnityInstance.mock.calls[0][1];
      expect(config.dataUrl).toBe('build-plain/Build/build-plain.data');
      expect(config.codeUrl).toBe('build-plain/Build/build-plain.wasm');
    });

    it('updates loadingProgress from the progress callback', async () => {
      stubFetch(() => true);
      const instance = createSpyInstance();
      const createUnityInstance = vi.fn(
        async (_canvas: unknown, _config: unknown, onProgress: (p: number) => void) => {
          onProgress(0.42);
          return instance;
        },
      );
      vi.stubGlobal('createUnityInstance', createUnityInstance);

      const fixture = createViewport('build-progress');
      fixture.detectChanges();
      await settleLoaderScript('build-progress/Build/build-progress.loader.js', 'load');

      await vi.waitFor(() =>
        expect(fixture.componentInstance.loadingProgress()).toBe('Loading Unity... 42%'),
      );
    });

    it('loads the loader script only once for two viewports with the same buildPath', async () => {
      stubFetch(() => true);
      vi.stubGlobal('createUnityInstance', vi.fn().mockResolvedValue(createSpyInstance()));

      const first = createViewport('build-dedup');
      first.detectChanges();
      await settleLoaderScript('build-dedup/Build/build-dedup.loader.js', 'load');
      await vi.waitFor(() => expect(first.componentInstance.isLoaded()).toBe(true));

      const second = createViewport('build-dedup');
      second.detectChanges();
      await vi.waitFor(() => expect(second.componentInstance.isLoaded()).toBe(true));

      const scripts = document.querySelectorAll('script[src="build-dedup/Build/build-dedup.loader.js"]');
      expect(scripts.length).toBe(1);
    });
  });

  describe('error handling', () => {
    it('emits loadError and falls back to a mock when createUnityInstance rejects', async () => {
      stubFetch(() => true);
      vi.stubGlobal('createUnityInstance', vi.fn().mockRejectedValue(new Error('boom')));

      const fixture = createViewport('build-broken');
      const ready = vi.fn();
      const errored = vi.fn();
      fixture.componentInstance.instanceReady.subscribe(ready);
      fixture.componentInstance.loadError.subscribe(errored);
      fixture.detectChanges();
      await settleLoaderScript('build-broken/Build/build-broken.loader.js', 'load');

      await vi.waitFor(() => expect(ready).toHaveBeenCalledOnce());
      expect(errored).toHaveBeenCalledOnce();
      expect(errored.mock.calls[0][0].message).toBe('boom');
      expect(fixture.componentInstance.useMock()).toBe(true);
      expect(fixture.componentInstance.loadFailed()).toBe(false);
    });

    it('sets loadFailed instead of falling back when fallbackToMock is false', async () => {
      stubFetch(() => true);
      vi.stubGlobal('createUnityInstance', vi.fn().mockRejectedValue(new Error('boom')));

      const fixture = createViewport('build-broken-2');
      fixture.componentRef.setInput('fallbackToMock', false);
      const ready = vi.fn();
      const errored = vi.fn();
      fixture.componentInstance.instanceReady.subscribe(ready);
      fixture.componentInstance.loadError.subscribe(errored);
      fixture.detectChanges();
      await settleLoaderScript('build-broken-2/Build/build-broken-2.loader.js', 'load');

      await vi.waitFor(() => expect(fixture.componentInstance.loadFailed()).toBe(true));
      expect(errored).toHaveBeenCalledOnce();
      expect(ready).not.toHaveBeenCalled();
      expect(fixture.componentInstance.isLoaded()).toBe(false);
      expect(fixture.componentInstance.useMock()).toBe(false);
    });

    it('evicts a failed loader script from the cache so the same buildPath can retry', async () => {
      stubFetch(() => true);
      vi.stubGlobal('createUnityInstance', vi.fn().mockResolvedValue(createSpyInstance()));

      const first = createViewport('build-retry');
      const firstError = vi.fn();
      first.componentInstance.loadError.subscribe(firstError);
      first.detectChanges();
      await settleLoaderScript('build-retry/Build/build-retry.loader.js', 'error');
      await vi.waitFor(() => expect(firstError).toHaveBeenCalledOnce());

      // The failed script was removed and the cache evicted — a new viewport retries.
      const second = createViewport('build-retry');
      second.detectChanges();
      await settleLoaderScript('build-retry/Build/build-retry.loader.js', 'load');
      await vi.waitFor(() => expect(second.componentInstance.isLoaded()).toBe(true));
      expect(second.componentInstance.useMock()).toBe(false);
    });
  });

  describe('lifecycle', () => {
    it('assigns distinct canvas ids to separate instances', () => {
      const a = TestBed.createComponent(NgxUnityViewport);
      const b = TestBed.createComponent(NgxUnityViewport);

      expect(a.componentInstance.resolvedCanvasId()).not.toBe(b.componentInstance.resolvedCanvasId());
    });

    it('uses the canvasId input when provided', () => {
      const fixture = TestBed.createComponent(NgxUnityViewport);
      fixture.componentRef.setInput('canvasId', 'my-custom-canvas');
      fixture.detectChanges();

      expect(fixture.componentInstance.resolvedCanvasId()).toBe('my-custom-canvas');
      expect(fixture.nativeElement.querySelector('canvas').id).toBe('my-custom-canvas');
    });

    it('calls Quit on the instance when destroyed', async () => {
      stubFetch(() => false);
      const customMock = createSpyInstance();
      const fixture = createViewport('missing-build-destroy');
      fixture.componentRef.setInput('mockFactory', () => customMock);
      fixture.detectChanges();
      await vi.waitFor(() => expect(fixture.componentInstance.isLoaded()).toBe(true));

      fixture.destroy();

      expect(customMock.Quit).toHaveBeenCalledOnce();
    });
  });
});
