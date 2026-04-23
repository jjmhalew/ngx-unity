![Release](https://img.shields.io/github/v/release/jjmhalew/ngx-unity)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/jjmhalew/ngx-unity)
![Downloads](https://img.shields.io/npm/dw/ngx-unity)
[![License](https://img.shields.io/:license-GNU-blue.svg?style=flat)](https://opensource.org/licenses/GNU)

<img width="170" height="180" alt="ngx-unity logo" src="https://github.com/user-attachments/assets/2eb16db5-f296-49b0-a0af-a2e43a7d9ac0" />

# ngx-unity

A type-safe bridge for bidirectional communication between Unity WebGL/WebGPU and Angular.

This project has two parts:

| Package | Distribution | Contents |
|---|---|---|
| **`ngx-unity`** | Angular library (npm) | Reusable viewport component, `IUnityInstance` type, mock utilities |
| **Unity C# scripts** | Copy to `Assets/` | Attributes, TypeScript code generators, editor settings |

## Demo
https://ngx-unity.web.app/

## Features

- **Angular → Unity**: Call Unity methods from Angular via auto-generated `UnityClient.ts`
- **Unity → Angular**: Receive Unity events in Angular via auto-generated signals (no RxJS required)
- **Callback Support**: Request-response and event registration patterns between C# and JavaScript
- **TSDoc Generation**: C# XML documentation automatically appears in generated TypeScript
- **Custom Attributes**: `[AngularExposed]` and `[JSLibExport]` for clean, declarative setup
- **Configurable Output**: Control where generated files are placed via Editor settings
- **`<ngx-unity-viewport>`**: Ready-to-use component that loads Unity WebGL/WebGPU with automatic mock fallback

## Requirements

- Unity 2021.2+ (for `makeDynCall` callback support)
- Angular 16+ (for signals support)

## Quick Start

### Unity Setup

1. Copy all `.cs` files from this repository into your Unity project (e.g., `Assets/Scripts/UnityAngularBridge/`).

2. **Configure the output paths** (optional):
   `Tools > UnityAngularBridge > Settings`
   Set where `UnityClient.ts` and `unity-jslib-exported.service.ts` are generated.
   By default, `UnityClient.ts` goes to your Documents folder.

3. **Enable callback support** (if using callbacks):
   `Tools > UnityAngularBridge > Enable Callback Support`
   This sets the required Emscripten arg (`-s ALLOW_TABLE_GROWTH`).

4. Recompile or click Play in Unity Editor to regenerate all TypeScript files.

### Angular Setup

1. Copy the generated TypeScript files into your Angular project (e.g., `src/app/generated/`).

2. Register `UnityClient` in your app config:
   ```typescript
   import { UnityClient } from './generated/unity-client';

   export const appConfig: ApplicationConfig = {
     providers: [UnityClient],
   };
   ```

3. Use the `<ngx-unity-viewport>` component to embed Unity:
   ```typescript
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
       // Wire up your bridge service
     }
   }
   ```

4. Inject `UnityJSLibExportedService` wherever you need Unity events:
   ```typescript
   import { UnityJSLibExportedService } from './generated/unity-jslib-exported.service';

   const jsLib = inject(UnityJSLibExportedService);
   // Access signals directly
   const selectedObject = jsLib.sendSelectedObject; // Signal<string | null>
   ```

---

## Calling Unity from Angular

### How to use

Mark Unity methods with `[AngularExposed]`:

```csharp
/// <summary>
/// Load an object by its ID. Called from Angular.
/// </summary>
[AngularExposed(gameObjectName: "SceneManager")]
public void LoadObject(string objectId)
{
    // Your logic here
}
```

This generates `UnityClient.ts` with typed methods and TSDoc:

```typescript
export class UnityClient {
  /** Load an object by its ID. Called from Angular. */
  public sceneManager_LoadObject(unityInstance: IUnityInstance, objectId: string): void {
    unityInstance?.SendMessage("SceneManager", "LoadObject", objectId);
  }
}
```

### Parameters

- Only `string` and `number` parameter types are supported (Unity WebGL limitation)
- Maximum 1 parameter per method
- The `gameObjectName` identifies which Unity GameObject receives the `SendMessage` call
- Optional `Documentation` property overrides the TSDoc output

**Note**: Generation happens at compile time / Play mode, not at runtime. A default GameObject name is used unless overridden in `[AngularExposed]`.

### How to update

Recompile or click on 'Play' in Unity editor to trigger `AngularExposedExport.cs`.
This will automatically generate `UnityClient.ts` to the configured output path.

---

## Subscribing to Unity Events from Angular

### How to use

Declare `[DllImport("__Internal")]` methods with the optional `[JSLibExport]` attribute:

```csharp
/// <summary>
/// Sends the selected object ID to Angular.
/// </summary>
[DllImport("__Internal")]
[JSLibExport(Category = "Selection")]
private static extern void SendSelectedObject(string objectId);

/// <summary>
/// Sends a pipe-separated list as a string array.
/// </summary>
[DllImport("__Internal")]
[JSLibExport(IsStringArray = true, Category = "Objects")]
private static extern void SendObjectsList(string objectIds);

/// <summary>
/// Notifies Angular (no data, event-only).
/// </summary>
[DllImport("__Internal")]
[JSLibExport(Category = "Lifecycle")]
private static extern void SendSceneReady();
```

Call from Unity:

```csharp
#if PLATFORM_WEBGL && !UNITY_EDITOR
    SendSelectedObject(objectId);
    SendObjectsList(string.Join("|", objectIds));
    SendSceneReady();
#endif
```

This generates an Angular service with **pure signals** (no RxJS):

```typescript
@Injectable({ providedIn: "root" })
export class UnityJSLibExportedService {
  /** Sends the selected object ID to Angular. */
  readonly sendSelectedObject: Signal<string | null>;

  /** Sends a pipe-separated list as a string array. */
  readonly sendObjectsList: Signal<string[]>;

  /** Notifies Angular (no data, event-only). Increments on each event. */
  readonly sendSceneReady: Signal<number>;
}
```

### `[JSLibExport]` Attribute

| Property | Type | Default | Description |
|---|---|---|---|
| `IsStringArray` | `bool` | `false` | Split pipe-delimited string into `string[]` |
| `Category` | `string` | `""` | Organize methods (for documentation) |
| `Documentation` | `string` | `""` | Override TSDoc (falls back to XML docs) |
| `IsCallbackRegistration` | `bool` | `false` | Mark as callback registration point |

### How to update

Recompile or click on 'Play' in Unity editor to trigger `JSLibExport.cs`.
This will automatically generate:
1. `BrowserInteractions.jslib` — placed in `Assets/Plugins/`
2. `unity-jslib-exported.service.ts` — placed at the configured output path

---

## Callback Support

Based on [jmschrack.dev/posts/UnityWebGL](https://jmschrack.dev/posts/UnityWebGL/).

### Request-Response (C# → JS → C#)

Unity sends a request to Angular with a callback. Angular processes and responds:

**Unity (C#):**

```csharp
[DllImport("__Internal")]
[JSLibExport(Category = "Data")]
private static extern void RequestDataFromWeb(string query, Action<string> onResult);

[MonoPInvokeCallback(typeof(Action<string>))]
private static void OnDataReceived(string data)
{
    Debug.Log($"Received: {data}");
}

// Usage:
#if PLATFORM_WEBGL && !UNITY_EDITOR
    RequestDataFromWeb("my-query", OnDataReceived);
#endif
```

**Angular (TypeScript):**

```typescript
const jsLib = inject(UnityJSLibExportedService);

// Register a handler that responds to Unity's requests
jsLib.registerRequestDataFromWebHandler((query, respond) => {
    const result = processQuery(query);
    respond(result); // Sends result back to C# callback
});
```

### Event Registration (JS → C#)

Unity registers a callback that Angular can invoke later:

**Unity (C#):**

```csharp
[DllImport("__Internal")]
[JSLibExport(IsCallbackRegistration = true, Category = "Navigation")]
private static extern void RegisterOnNavigationChanged(Action<string> handler);

[MonoPInvokeCallback(typeof(Action<string>))]
private static void OnNavigationChanged(string route)
{
    Debug.Log($"Navigation: {route}");
}

// Register once on start:
#if PLATFORM_WEBGL && !UNITY_EDITOR
    RegisterOnNavigationChanged(OnNavigationChanged);
#endif
```

**Angular (TypeScript):**

```typescript
const jsLib = inject(UnityJSLibExportedService);

// Later, notify Unity of a navigation change:
jsLib.notifyOnNavigationChanged("/new-route");
```

### Important Notes

- Callback methods must be `static` and marked with `[MonoPInvokeCallback]`
- For registration callbacks, enable Emscripten support via `Tools > UnityAngularBridge > Enable Callback Support`
- Callbacks support `Action` (void) and `Action<string>` (string parameter)

---

## Configuration

### Output Paths

Open `Tools > UnityAngularBridge > Settings` to configure:

| Setting | Default | Description |
|---|---|---|
| UnityClient.ts path | MyDocuments | Where the Angular-to-Unity client is generated |
| Service .ts path | Assets/Plugins | Where the Unity-to-Angular service is generated |

Paths can be absolute or relative to the Unity project folder.

### TSDoc / XML Documentation

Generated TypeScript includes JSDoc comments from either:
1. **C# XML documentation comments** (`/// <summary>`) — requires XML docs enabled in Unity
2. **Attribute `Documentation` property** (fallback/override)

---

## ngx-unity Library

The `ngx-unity` Angular library (in `example/angular-unity-example/projects/ngx-unity/`) provides reusable building blocks:

### `NgxUnityViewport` Component

A drop-in component that handles Unity WebGL/WebGPU loading with automatic mock fallback:

```html
<ngx-unity-viewport
  buildPath="unity"
  height="400px"
  [mockFactory]="myMockFactory"
  (instanceReady)="onReady($event)" />
```

| Input | Type | Default | Description |
|---|---|---|---|
| `buildPath` | `string` | `'unity'` | Path to Unity WebGL/WebGPU build (relative to `public/`) |
| `height` | `string` | `'400px'` | CSS height of the canvas |
| `mockFactory` | `() => IUnityInstance` | built-in mock | Custom mock factory for development |

| Output | Type | Description |
|---|---|---|
| `instanceReady` | `IUnityInstance` | Emitted when Unity (or mock) is ready |

### `createMockUnityInstance()`

A testing utility that creates a basic mock `IUnityInstance`:

```typescript
import { createMockUnityInstance } from 'ngx-unity';

const mock = createMockUnityInstance({
  onSendMessage: (obj, method, data) => {
    // Simulate project-specific Unity responses
  },
});
```

### Multi-Instance Support

Multiple `<ngx-unity-viewport>` components can coexist on the same page.
Each viewport creates its own `<canvas>` with a unique DOM ID, and the Unity
loader script is loaded only once even when viewports share the same `buildPath`.

```html
<!-- Two viewports side by side using the same Unity build -->
<ngx-unity-viewport
  buildPath="unity"
  height="300px"
  (instanceReady)="onFirstReady($event)" />

<ngx-unity-viewport
  buildPath="unity"
  height="300px"
  (instanceReady)="onSecondReady($event)" />
```

Each viewport emits its own `instanceReady` event with an independent
`IUnityInstance`, so you can wire each one to a separate bridge service or
handle them individually:

```typescript
instances: IUnityInstance[] = [];

onFirstReady(instance: IUnityInstance): void {
  this.instances[0] = instance;
}

onSecondReady(instance: IUnityInstance): void {
  this.instances[1] = instance;
}
```

> **Note:** The auto-generated `UnityJSLibExportedService` registers global
> `window` callbacks, so Unity → Angular signals (e.g. `sendSelectedObject`)
> reflect the most recent event from *any* instance. If you need per-instance
> isolation for Unity → Angular events, maintain separate state in your
> bridge service keyed by each `IUnityInstance`.

---

## Project Structure

```
ngx-unity/
├── *.cs                                  ← Unity C# source files
├── README.md
├── example/
│   ├── angular-unity-example/
│   │   ├── projects/
│   │   │   └── ngx-unity/                ← Angular library (publishable to npm)
│   │   │       └── src/lib/
│   │   │           ├── components/        ← NgxUnityViewport
│   │   │           ├── models/            ← IUnityInstance
│   │   │           └── testing/           ← createMockUnityInstance
│   │   └── src/                           ← Example app
│   │       └── app/
│   │           ├── generated/             ← Unity-generated TS files
│   │           ├── services/              ← Project-specific bridge
│   │           └── components/            ← Demo UI
│   └── unity-project/                     ← Example Unity project
```

---

## TODO

- [ ] Publish `ngx-unity` to npm
- [ ] Distribute Unity scripts as a UPM package (git URL)
- [ ] Auto-generate JSON serialization for complex Angular→Unity parameters
