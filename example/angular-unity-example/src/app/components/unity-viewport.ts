import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { NgxUnityViewport, type IUnityInstance } from 'ngx-unity';
import { UnityBridgeService } from '../services/unity-bridge.service';
import { createProjectMockUnityInstance } from '../services/mock-unity';

/**
 * Wraps the library's `<ngx-unity-viewport>` with project-specific wiring:
 * connects the Unity instance to the bridge service and provides the project mock.
 *
 * Accepts `buildPath` and `height` inputs so the same component can be reused
 * for multiple viewports on the same page.
 */
@Component({
  selector: 'app-unity-viewport',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgxUnityViewport],
  template: `
    <ngx-unity-viewport
      [buildPath]="buildPath()"
      [height]="height()"
      [mockFactory]="mockFactory"
      (instanceCreated)="onInstanceCreated($event)" />
  `,
})
export class UnityViewport {
  /** Path to the Unity WebGL/WebGPU build folder. */
  readonly buildPath = input('unity');

  /** CSS height of the canvas element. */
  readonly height = input('400px');

  private readonly bridge = inject(UnityBridgeService);

  protected readonly mockFactory = () => createProjectMockUnityInstance();

  protected onInstanceCreated(event: { instance: IUnityInstance; canvasId: string }): void {
    // The canvasId keys per-instance Unity → Angular signals via
    // UnityJSLibExportedService.forInstance(event.canvasId).
    this.bridge.setUnityInstance(event.instance);
  }
}
