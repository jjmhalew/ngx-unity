# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [22.1.0]

### Added
- **Typed JSON payloads**: `JsonType` property on `[AngularExposed]` and `[JSLibExport]` —
  the generators emit matching TypeScript interfaces and handle `JSON.stringify`/`JSON.parse`
  automatically (supports public fields of primitives, arrays/`List<T>`, and nested
  `[Serializable]` classes).
- **Multi-instance routing**: the generated `.jslib` tags every Unity → Angular call with
  the originating canvas id; the generated service exposes `forInstance(canvasId)` returning
  a per-instance channel of signals and callbacks. Flat signals keep their
  last-event-from-any-instance behavior.
- `NgxUnityViewport`: `canvasId` input, `resolvedCanvasId` signal, `instanceCreated` output,
  `fallbackToMock` input, `loadError` output, and `loadFailed` signal.
- Configurable `IUnityInstance` import path for the generated client
  (`Tools > UnityAngularBridge > Settings`, default `ngx-unity`, empty = inline interface).
- Vitest test suite (~40 tests) covering the viewport component, mock utilities, the
  generated service contract, and the demo bridge service.
- CI workflow (build library + run tests + build app) on pushes and pull requests.
- `CONTRIBUTING.md` and an expanded `ngx-unity` package README.

### Changed
- Generated client renamed `UnityClient.ts` → `unity-client.ts`; it now imports
  `IUnityInstance` from `ngx-unity` instead of redefining it (configurable). Delete the
  old `UnityClient.ts` after regenerating.
- Generators write output only when content changed (no more file-watcher churn on every
  Unity domain reload) and log errors instead of crashing editor initialization.
- `NgxUnityViewport` probes build compression extensions in parallel and overlaps them
  with the loader script load (fewer serialized round-trips before Unity starts).

### Fixed
- `ngx-unity` peerDependencies: `@angular/core` now correctly requires `^22.0.0`.
- Empty string-array events from Unity now produce `[]` instead of `[""]`.
- A failed Unity loader script no longer stays cached forever — the same `buildPath`
  can retry after a failure.
- A broken Unity build no longer silently falls back to a mock: `loadError` is emitted,
  and the fallback can be disabled via `fallbackToMock`.
- README requirements corrected (Angular 22+, not 16+) and the license badge now points
  at the repository's GPL-3.0 license.

## [22.0.0] - 2026-06-07

### Changed
- Upgraded to Angular 22 (zoneless, signal-based inputs/outputs).
- `ngx-unity` released on npm for Angular 22.

## [21.0.0] - 2026-04-23

### Added
- First `ngx-unity` npm release (project renamed from Unity-Angular-Bridge).
- `NgxUnityViewport` component with automatic mock fallback and multi-viewport support.
- Signals-based generated Angular service (no RxJS).
- Firebase-hosted demo with automatic deployment.
