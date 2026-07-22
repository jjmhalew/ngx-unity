import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { UnityBridgeService } from '../services/unity-bridge.service';

@Component({
  selector: 'app-unity-controls',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .controls {
      padding: 1rem;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      background: #fafafa;
    }
    h2 { margin-top: 0; color: #333; }
    .description { color: #666; font-size: 0.9rem; }
    code { background: #e8e8e8; padding: 2px 6px; border-radius: 3px; font-size: 0.85em; }
    .control-group {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 0.75rem;
      flex-wrap: wrap;
    }
    label { font-weight: 500; min-width: 70px; }
    input[type="text"] {
      padding: 6px 10px;
      border: 1px solid #ccc;
      border-radius: 4px;
      font-size: 0.9rem;
    }
    input[type="color"] { width: 40px; height: 32px; border: 1px solid #ccc; border-radius: 4px; cursor: pointer; }
    button {
      padding: 6px 14px;
      border: none;
      border-radius: 4px;
      background: #1976d2;
      color: white;
      cursor: pointer;
      font-size: 0.9rem;
    }
    button:hover:not(:disabled) { background: #1565c0; }
    button:disabled { opacity: 0.5; cursor: not-allowed; }
  `,
  template: `
    <section class="controls">
      <h2>Angular → Unity</h2>
      <p class="description">
        Send commands to Unity using the generated <code>UnityClient</code>
      </p>

      <div class="control-group">
        <label for="objectId">Object ID:</label>
        <input
          id="objectId"
          type="text"
          [value]="objectId()"
          (input)="objectId.set(toInputValue($event))"
        />
        <button (click)="bridge.loadObject(objectId())" [disabled]="!bridge.isConnected()">
          Load Object
        </button>
      </div>

      <div class="control-group">
        <label for="color">Color:</label>
        <input
          id="color"
          type="color"
          [value]="color()"
          (input)="color.set(toInputValue($event))"
        />
        <button (click)="bridge.setColor(color())" [disabled]="!bridge.isConnected()">
          Set Color
        </button>
      </div>

      <div class="control-group">
        <button (click)="bridge.toggleVisibility()" [disabled]="!bridge.isConnected()">
          Toggle Visibility
        </button>
        <button (click)="bridge.resetScene()" [disabled]="!bridge.isConnected()">
          Reset Scene
        </button>
        <button (click)="spawnObject()" [disabled]="!bridge.isConnected()">
          Spawn Object (JSON)
        </button>
      </div>
    </section>
  `,
})
export class UnityControls {
  protected readonly bridge = inject(UnityBridgeService);
  protected readonly objectId = signal('Cube-001');
  protected readonly color = signal('#ff6600');

  private spawnCounter = 0;

  /** Demonstrates the typed JSON parameter feature: sends a SpawnRequest object to Unity. */
  protected spawnObject(): void {
    this.spawnCounter++;
    this.bridge.spawnObject({
      objectId: `Spawned-${String(this.spawnCounter).padStart(3, '0')}`,
      x: -3 + (this.spawnCounter % 7),
      y: 2.5,
      z: 0,
      colorHex: this.color(),
    });
  }

  protected toInputValue(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
