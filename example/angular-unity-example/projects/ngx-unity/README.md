# ngx-unity

Angular library for embedding and communicating with Unity WebGL/WebGPU builds.

Part of the [Unity-Angular-Bridge](https://github.com/jjmhalew/ngx-unity) project,
which also provides Unity C# code generators for type-safe bidirectional
communication (typed method wrappers, signals, JSON DTOs, callbacks).

## Installation

```bash
npm install ngx-unity
```

Requires Angular 22+.

## Quick start

```typescript
import { Component } from '@angular/core';
import { NgxUnityViewport, type IUnityInstance } from 'ngx-unity';

@Component({
  imports: [NgxUnityViewport],
  template: `
    <ngx-unity-viewport
      buildPath="unity"
      height="500px"
      (instanceReady)="onUnityReady($event)" />
  `,
})
export class MyComponent {
  onUnityReady(instance: IUnityInstance): void {
    instance.SendMessage('SceneManager', 'LoadObject', 'Cube-001');
  }
}
```

Place your Unity WebGL/WebGPU build under `public/<buildPath>/` (so the loader is
at `public/unity/Build/unity.loader.js` for `buildPath="unity"`). `.br` and `.gz`
compressed builds are detected automatically. If no build is found, the viewport
falls back to a mock instance so the rest of your app keeps working during development.

## `<ngx-unity-viewport>`

| Input | Type | Default | Description |
|---|---|---|---|
| `buildPath` | `string` | `'unity'` | Path to the Unity build folder (relative to `public/`) |
| `height` | `string` | `'400px'` | CSS height of the canvas |
| `canvasId` | `string` | auto-generated | DOM id for the canvas (identifies the instance for multi-instance routing) |
| `mockFactory` | `() => IUnityInstance` | built-in mock | Custom mock factory for development |
| `fallbackToMock` | `boolean` | `true` | Fall back to a mock when a real build fails to load; `false` surfaces the failure |

| Output | Type | Description |
|---|---|---|
| `instanceReady` | `IUnityInstance` | Emitted when Unity (or mock) is ready |
| `instanceCreated` | `{ instance, canvasId }` | Like `instanceReady`, plus the canvas id |
| `loadError` | `Error` | Emitted when a build was found but failed to load |

Exposed signals: `isLoading`, `isLoaded`, `useMock`, `loadFailed`,
`loadingProgress`, `renderingBackend`, `resolvedCanvasId`.

Multiple viewports can coexist on one page; the Unity loader script is shared
per `buildPath`, and each canvas gets a unique DOM id.

## Testing utilities

`createMockUnityInstance()` creates a mock `IUnityInstance` — useful both as the
viewport's development fallback and in unit tests:

```typescript
import { createMockUnityInstance } from 'ngx-unity';

const mock = createMockUnityInstance({
  onSendMessage: (gameObjectName, methodName, data) => {
    // Simulate Unity responses, e.g. invoke the window callbacks your
    // generated service listens to:
    if (methodName === 'LoadObject') {
      (window as any)['sendSelectedObjectFromUnity']?.(data);
    }
  },
});
```

`MockUnityOptions` is exported for typing custom factories.

## Unity side

Type-safe bridge code (`unity-client.ts`, `unity-jslib-exported.service.ts`, and
the `.jslib` plugin) is generated from C# attributes by the Unity editor scripts
in the [main repository](https://github.com/jjmhalew/ngx-unity) — see its README
for the full setup, JSON payload support, callbacks, and multi-instance routing.

## Building & publishing (maintainers)

```bash
ng build ngx-unity
cd dist/ngx-unity
npm publish
```

## License

GPL-3.0 — see [LICENSE](https://github.com/jjmhalew/ngx-unity/blob/main/LICENSE).
