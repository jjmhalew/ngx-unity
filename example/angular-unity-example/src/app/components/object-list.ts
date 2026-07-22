import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { UnityBridgeService } from '../services/unity-bridge.service';

@Component({
  selector: 'app-object-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    .object-list {
      padding: 1rem;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      background: #fafafa;
    }
    h2 { margin-top: 0; color: #333; }
    .description { color: #666; font-size: 0.9rem; }
    code { background: #e8e8e8; padding: 2px 6px; border-radius: 3px; font-size: 0.85em; }
    .status {
      display: flex;
      gap: 1rem;
      margin-bottom: 1rem;
      flex-wrap: wrap;
      font-size: 0.9rem;
    }
    .badge {
      padding: 2px 10px;
      border-radius: 12px;
      background: #e0e0e0;
      color: #666;
      font-size: 0.85rem;
    }
    .badge.active { background: #4caf50; color: white; }
    ul { list-style: none; padding: 0; margin: 0; }
    li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 8px 12px;
      border-bottom: 1px solid #eee;
      font-size: 0.9rem;
    }
    li.selected { background: #e3f2fd; font-weight: 500; }
    li button {
      padding: 4px 10px;
      border: 1px solid #1976d2;
      border-radius: 4px;
      background: white;
      color: #1976d2;
      cursor: pointer;
      font-size: 0.8rem;
    }
    li button:hover { background: #e3f2fd; }
    .empty { color: #999; font-style: italic; }
  `,
  template: `
    <section class="object-list">
      <h2>Unity → Angular</h2>
      <p class="description">
        Receive data from Unity via the generated
        <code>UnityJSLibExportedService</code>
      </p>

      <div class="status">
        <span class="badge" [class.active]="bridge.isConnected()">
          {{ bridge.isConnected() ? 'Connected' : 'Disconnected' }}
        </span>
        <span>Objects: {{ bridge.objectCount() }}</span>
        <span>Selected: {{ bridge.selectedObject() ?? 'None' }}</span>
        @if (bridge.sceneState(); as state) {
          <span>Scene state (JSON): {{ state.objectCount }} objects, {{ state.visible ? 'visible' : 'hidden' }}</span>
        }
      </div>

      @if (bridge.objectsList().length > 0) {
        <ul>
          @for (obj of bridge.objectsList(); track obj) {
            <li [class.selected]="obj === bridge.selectedObject()">
              {{ obj }}
              <button (click)="bridge.loadObject(obj)">Select</button>
            </li>
          }
        </ul>
      } @else {
        <p class="empty">No objects loaded. Click "Reset Scene" to load sample objects.</p>
      }
    </section>
  `,
})
export class ObjectList {
  protected readonly bridge = inject(UnityBridgeService);
}
