import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  computed,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { IUnityInstance } from '../models/unity-instance';
import { createMockUnityInstance } from '../testing/mock-unity';

/** Track which Unity loader scripts have already been loaded (or are loading). */
const loaderPromises = new Map<string, Promise<void>>();

/** Auto-incrementing counter for generating unique canvas IDs. */
let nextCanvasId = 0;

/**
 * Embeds a Unity WebGL/WebGPU player in a `<canvas>` element.
 *
 * Each instance of this component creates its own canvas with a unique DOM ID,
 * so multiple viewports can coexist on the same page.
 * When two viewports share the same `buildPath`, the Unity loader script is
 * loaded only once and reused.
 *
 * If a real Unity build exists at the given `buildPath`, it is loaded automatically.
 * Otherwise a mock Unity instance is created so the rest of the app still works.
 *
 * @example
 * ```html
 * <ngx-unity-viewport
 *   buildPath="unity"
 *   height="500px"
 *   (instanceReady)="onUnityReady($event)" />
 * ```
 *
 * @example Multiple instances
 * ```html
 * <ngx-unity-viewport
 *   buildPath="unity"
 *   height="300px"
 *   (instanceReady)="onFirstReady($event)" />
 * <ngx-unity-viewport
 *   buildPath="unity"
 *   height="300px"
 *   (instanceReady)="onSecondReady($event)" />
 * ```
 */
@Component({
  selector: 'ngx-unity-viewport',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host { display: block; }
    .viewport {
      width: 100%;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      overflow: hidden;
      background: #1a1a2e;
      position: relative;
    }
    canvas {
      display: block;
      width: 100%;
    }
    .overlay {
      position: absolute;
      inset: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      color: #aaa;
      font-size: 0.95rem;
      text-align: center;
      padding: 1rem;
      pointer-events: none;
    }
    .overlay.hidden { display: none; }
    .overlay h3 { color: #ddd; margin: 0 0 0.5rem; }
    .overlay code {
      background: rgba(255,255,255,0.1);
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 0.85em;
    }
    .status-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0.4rem 0.75rem;
      background: #f5f5f5;
      border-top: 1px solid #e0e0e0;
      font-size: 0.8rem;
      color: #666;
    }
    .badge {
      padding: 2px 8px;
      border-radius: 10px;
      font-size: 0.75rem;
    }
    .badge.mock { background: #fff3e0; color: #e65100; }
    .badge.webgl { background: #e8f5e9; color: #2e7d32; }
    .badge.webgpu { background: #e0f2f1; color: #00695c; }
    .progress { color: #1976d2; }
  `,
  template: `
    <div class="viewport">
      <canvas #unityCanvas [id]="resolvedCanvasId()" [style.height]="height()"></canvas>

      <div class="overlay" [class.hidden]="isLoaded()">
        @if (isLoading()) {
          <p class="progress">{{ loadingProgress() }}</p>
        } @else if (useMock()) {
          <h3>Unity Viewport</h3>
          <p>No Unity build found at <code>public/{{ buildPath() }}/</code></p>
          <p>Using mock instance for development.</p>
        } @else if (loadFailed()) {
          <h3>Unity Viewport</h3>
          <p>The Unity build at <code>public/{{ buildPath() }}/</code> failed to load.</p>
          <p>Check the browser console for details.</p>
        }
      </div>
    </div>

    <div class="status-bar">
      <span>
        @if (useMock()) {
          <span class="badge mock">Mock</span>
        } @else if (renderingBackend() === 'webgpu') {
          <span class="badge webgpu">WebGPU</span>
        } @else {
          <span class="badge webgl">WebGL</span>
        }
        Unity Viewport
      </span>
      <span>{{ isLoaded() ? 'Connected' : 'Disconnected' }}</span>
    </div>
  `,
})
export class NgxUnityViewport implements OnInit, OnDestroy {
  /** Path to the Unity WebGL/WebGPU build folder, relative to Angular's public/assets directory. */
  readonly buildPath = input('unity');

  /** CSS height of the canvas element. */
  readonly height = input('400px');

  /**
   * Optional DOM id for the canvas element. When omitted, a unique id is generated.
   * The canvas id identifies this instance in `UnityJSLibExportedService.forInstance()`.
   */
  readonly canvasId = input<string | undefined>(undefined);

  /**
   * Optional factory function that creates a mock `IUnityInstance`.
   * Called when no real Unity build is found.
   * If not provided, a default logging-only mock is used.
   */
  readonly mockFactory = input<(() => IUnityInstance) | undefined>(undefined);

  /**
   * When `true` (default), fall back to a mock instance if a real Unity build
   * is found but fails to load. Set to `false` to surface the failure via
   * `loadError` and the `loadFailed` signal instead.
   * A missing build always falls back to a mock, regardless of this setting.
   */
  readonly fallbackToMock = input(true);

  /** Emitted when a Unity instance (real or mock) is ready. */
  readonly instanceReady = output<IUnityInstance>();

  /**
   * Emitted when a Unity instance (real or mock) is ready, including the canvas id
   * for per-instance routing via `UnityJSLibExportedService.forInstance(canvasId)`.
   */
  readonly instanceCreated = output<{ instance: IUnityInstance; canvasId: string }>();

  /**
   * Emitted when a real Unity build was found but failed to load.
   * Emitted even when falling back to a mock, so consumers can distinguish
   * "no build" from "build present but broken".
   */
  readonly loadError = output<Error>();

  /** Whether the Unity loader is currently loading. */
  readonly isLoading = signal(false);

  /** Whether a Unity instance (real or mock) has been created. */
  readonly isLoaded = signal(false);

  /** Whether the instance is a mock (no real Unity build found). */
  readonly useMock = signal(false);

  /** Whether loading a real build failed and no mock fallback was created. */
  readonly loadFailed = signal(false);

  /** Detected rendering backend after Unity loads. */
  readonly renderingBackend = signal<'webgl' | 'webgpu'>('webgl');

  /** Current loading progress message. */
  readonly loadingProgress = signal('Loading Unity...');

  /** Auto-generated fallback DOM id, used when no `canvasId` input is provided. */
  private readonly autoCanvasId = `ngx-unity-canvas-${nextCanvasId++}`;

  /** The DOM id actually assigned to the canvas: the `canvasId` input, or an auto-generated one. */
  readonly resolvedCanvasId = computed(() => this.canvasId() ?? this.autoCanvasId);

  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('unityCanvas');
  private unityInstance: IUnityInstance | null = null;

  async ngOnInit(): Promise<void> {
    const path = this.buildPath();
    const loaderUrl = `${path}/Build/${path}.loader.js`;
    const buildExists = await this.checkBuildExists(loaderUrl);

    if (buildExists) {
      await this.loadRealUnity(loaderUrl);
    } else {
      this.useMock.set(true);
      this.createAndEmitMock();
    }
  }

  ngOnDestroy(): void {
    this.unityInstance?.Quit();
  }

  private async checkBuildExists(loaderUrl: string): Promise<boolean> {
    try {
      const resp = await fetch(loaderUrl, { method: 'HEAD' });
      return resp.ok;
    } catch {
      return false;
    }
  }

  private async loadRealUnity(loaderUrl: string): Promise<void> {
    this.isLoading.set(true);

    try {
      const path = this.buildPath();
      const buildPath = `${path}/Build`;

      // Probe compression extensions in parallel while the loader script loads.
      const extsPromise = Promise.all([
        this.detectExtension(`${buildPath}/${path}.data`),
        this.detectExtension(`${buildPath}/${path}.framework.js`),
        this.detectExtension(`${buildPath}/${path}.wasm`),
      ]);

      await this.loadLoaderScript(loaderUrl);
      const [dataExt, frameworkExt, wasmExt] = await extsPromise;

      const canvas = this.canvasRef().nativeElement;
      const config = {
        dataUrl: `${buildPath}/${path}.data${dataExt}`,
        frameworkUrl: `${buildPath}/${path}.framework.js${frameworkExt}`,
        codeUrl: `${buildPath}/${path}.wasm${wasmExt}`,
        streamingAssetsUrl: `${path}/StreamingAssets`,
        companyName: 'DefaultCompany',
        productName: 'UnityApp',
        productVersion: '1.0.0',
        autoSyncPersistentDataPath: true,
      };

      /* eslint-disable @typescript-eslint/no-explicit-any */
      const createUnityInstance = (window as any).createUnityInstance;
      const instance: IUnityInstance = await createUnityInstance(
        canvas,
        config,
        (progress: number) => {
          this.loadingProgress.set(`Loading Unity... ${Math.round(progress * 100)}%`);
        },
      );

      this.unityInstance = instance;
      this.renderingBackend.set(this.detectRenderingBackend(canvas));
      this.isLoaded.set(true);
      this.instanceReady.emit(instance);
      this.instanceCreated.emit({ instance, canvasId: this.resolvedCanvasId() });
    } catch (err) {
      const error = err instanceof Error ? err : new Error(String(err));
      console.error('Unity failed to load:', error);
      this.loadError.emit(error);
      if (this.fallbackToMock()) {
        this.useMock.set(true);
        this.createAndEmitMock();
      } else {
        this.loadFailed.set(true);
      }
    } finally {
      this.isLoading.set(false);
    }
  }

  /** Create a mock instance (custom factory or default) and emit it as ready. */
  private createAndEmitMock(): void {
    const factory = this.mockFactory();
    const mock = factory ? factory() : createMockUnityInstance();
    this.unityInstance = mock;
    this.isLoaded.set(true);
    this.instanceReady.emit(mock);
    this.instanceCreated.emit({ instance: mock, canvasId: this.resolvedCanvasId() });
  }

  /**
   * Load the Unity loader script, reusing an in-flight or completed load for
   * the same URL. A failed load is evicted from the cache so it can be retried.
   */
  private loadLoaderScript(loaderUrl: string): Promise<void> {
    if (!loaderPromises.has(loaderUrl)) {
      const loaderPromise = new Promise<void>((resolve, reject) => {
        const script = document.createElement('script');
        script.src = loaderUrl;
        script.onload = () => resolve();
        script.onerror = () => {
          script.remove();
          reject(new Error(`Failed to load Unity loader: ${loaderUrl}`));
        };
        document.body.appendChild(script);
      }).catch((err) => {
        loaderPromises.delete(loaderUrl);
        throw err;
      });
      loaderPromises.set(loaderUrl, loaderPromise);
    }
    return loaderPromises.get(loaderUrl)!;
  }

  /** Detect whether Unity is using WebGL or WebGPU by probing the canvas context. */
  private detectRenderingBackend(canvas: HTMLCanvasElement): 'webgl' | 'webgpu' {
    try {
      if (canvas.getContext('webgl2') || canvas.getContext('webgl')) {
        return 'webgl';
      }
    } catch { /* context not available */ }
    return 'webgpu';
  }

  /** Detect which compression extension a file has (.br, .gz, or none), probing all variants in parallel. */
  private async detectExtension(basePath: string): Promise<string> {
    const exts = ['', '.br', '.gz'];
    const results = await Promise.all(
      exts.map((ext) =>
        fetch(basePath + ext, { method: 'HEAD' })
          .then((resp) => resp.ok)
          .catch(() => false),
      ),
    );
    const idx = results.findIndex(Boolean);
    return idx >= 0 ? exts[idx] : '';
  }
}
