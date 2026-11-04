import { Injectable, inject, signal } from '@angular/core';
import { BackendApi } from '../api/backend.api';
import { ItemKind, KIND_LABELS, MachineType } from '../api/certification.api';

/** Which machine names are gears (inspected in numbers), from `GET /certifications/machine-types`, loaded once and kept. */
@Injectable({ providedIn: 'root' })
export class MachineKinds {
  private readonly api = inject(BackendApi);
  readonly types = signal<MachineType[]>([]);
  private started = false;

  /** Starts the one call (later calls do nothing). A failed call leaves every machine a non-gear. */
  load(): void {
    if (this.started) return;
    this.started = true;
    this.api.machineTypes().subscribe({
      next: (t) => this.types.set(t),
      error: () => {
        this.started = false;
      },
    });
  }

  /** The catalogue entry of a name (or alias), case-insensitive; undefined for an unknown name. */
  find(name: string | null | undefined): MachineType | undefined {
    const w = (name ?? '').trim().toLowerCase();
    return w ? this.types().find((t) => t.name.toLowerCase() === w || t.aliases.some((a) => a.toLowerCase() === w)) : undefined;
  }

  /** The kind of an entry; older answers without `kind` fall back on `isGear`. */
  kindOf(t: MachineType): ItemKind {
    return t.kind ?? (t.isGear ? 'gear' : 'equipment');
  }
  kindLabel(k: ItemKind | null | undefined): string {
    return k ? KIND_LABELS[k] : '';
  }

  /** True when the name (or the type) is a gear of the catalogue; unknown names are not gears. Reads a signal, so templates update when the catalogue arrives. */
  isGear(name: string | null | undefined, type?: string | null): boolean {
    const wanted = [name, type].map((v) => (v ?? '').trim().toLowerCase()).filter(Boolean);
    return wanted.some((w) => this.types().some((t) => t.isGear && (t.name.toLowerCase() === w || t.aliases.some((a) => a.toLowerCase() === w))));
  }
}
