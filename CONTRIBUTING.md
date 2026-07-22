# Contributing

## Repository layout

| Path | Contents |
|---|---|
| `*.cs` (repo root) | Unity C# source: attributes + TypeScript/jslib code generators |
| `example/angular-unity-example/` | Angular workspace: demo app + the publishable `ngx-unity` library (`projects/ngx-unity/`) |
| `example/unity-project/` | Demo Unity project (contains a **copy** of the root `.cs` files) |

**Important:** the root `.cs` files are duplicated verbatim under
`example/unity-project/Assets/Scripts/UnityAngularBridge/`. When you change one,
sync the other and keep them byte-identical (`diff -r` them before committing).
New `.cs` files need a Unity-generated `.meta` file — open the Unity project once
and commit it.

## Running the demo

```bash
cd example/angular-unity-example
npm ci
npm run build:ngx-unity   # library must be built first (tsconfig maps ngx-unity → dist/)
npm start                 # serves on localhost:4200, uses the mock Unity instance
```

## Running tests

```bash
npm test          # both suites
npm run test:lib  # ngx-unity library only
npm run test:app  # demo app only (build the library first)
```

CI (`.github/workflows/ci.yml`) runs the same sequence on every push/PR.
There are no automated C# tests — Unity editor code is verified manually.

## How the generators work

Both run automatically in the Unity editor on recompile / Play (`[InitializeOnLoad]`):

- `AngularExposedExport.cs` scans `[AngularExposed]` methods → generates `unity-client.ts`
  (typed `SendMessage` wrappers + TS interfaces for `JsonType` DTOs).
- `JSLibExport.cs` scans `[DllImport("__Internal")]` methods → generates
  `BrowserInteractions.jslib` (with per-instance canvas-id tagging) and
  `unity-jslib-exported.service.ts` (signals + `forInstance()` channels).

Output paths are configured via `Tools > UnityAngularBridge > Settings`. Files are
only rewritten when content changes.

The generated files under `example/angular-unity-example/src/app/generated/` are
**committed on purpose** (the demo builds without Unity). If you change a generator,
update the committed generated files to exactly match the new output — regenerate
from the Unity editor, or hand-apply the diff.

## Releasing ngx-unity

```bash
cd example/angular-unity-example
npm run build:ngx-unity
cd dist/ngx-unity
npm publish
```

Update `CHANGELOG.md` and tag the release.
