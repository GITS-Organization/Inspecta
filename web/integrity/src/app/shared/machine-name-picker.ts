import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, input, model, output, signal, viewChild } from '@angular/core';
import { MachineType } from '../api/certification.api';
import { MachineKinds } from './machine-kinds';

/**
 * The name of a machine is one of the catalogue: a searchable select, no free typing. The name fixes two read-only fields shown
 * under it, the kind and the standards of inspection. A name that is not in the catalogue (old data) is kept as it is, with a notice.
 */
@Component({
  selector: 'app-machine-name-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:click)': 'outside($event)' },
  template: `
    <label [for]="inputId()">{{ label() }}</label>
    <div class="wrap" #wrap>
      <button type="button" class="sel" [id]="inputId()" [disabled]="disabled()" aria-haspopup="listbox" [attr.aria-expanded]="open()" (click)="toggle()">
        <span [class.ph]="!value()">{{ value() || 'Choose a name' }}</span>
        <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M6 9l6 6 6-6" /></svg>
      </button>
      @if (open()) {
        <div class="pop">
          <label class="sr" [for]="inputId() + '-q'">Search the names</label>
          <input [id]="inputId() + '-q'" type="search" autocomplete="off" placeholder="Search the names" [value]="query()" (input)="query.set($any($event.target).value)" (keydown.escape)="open.set(false)" (keydown.enter)="$event.preventDefault(); chooseFirst()" />
          <ul role="listbox" [attr.aria-label]="label()">
            @for (t of matches(); track t.name) {
              <li role="option" [attr.aria-selected]="t.name === value()">
                <button type="button" (click)="choose(t)">
                  <span>{{ t.name }}</span>
                  <span class="muted k">{{ kinds.kindLabel(kinds.kindOf(t)) }}</span>
                </button>
              </li>
            } @empty {
              <li class="muted none">{{ kinds.types().length ? 'No name matches.' : 'Loading the names…' }}</li>
            }
          </ul>
        </div>
      }
    </div>
    @if (value() && !found()) {
      <small class="warn" role="note">Not in the list: choose a name.</small>
    }
    @if (showInfo() && found(); as t) {
      <dl class="info">
        <div>
          <dt>Kind</dt>
          <dd>{{ kinds.kindLabel(kinds.kindOf(t)) }}</dd>
        </div>
        <div>
          <dt>Standard of inspection</dt>
          <dd>
            @for (s of t.standards; track s) { <span class="std">{{ s }}</span> } @empty { <span class="muted">No standard defined yet</span> }
          </dd>
        </div>
      </dl>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .wrap {
      position: relative;
    }
    .sel {
      width: 100%;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 8px;
      text-align: left;
      padding: 11px 13px;
      border: 1.5px solid var(--line);
      border-radius: 8px;
      background: #fff;
      cursor: pointer;
      color: var(--body);
      &:hover:not(:disabled) {
        border-color: #b9c6da;
      }
      &:disabled {
        background: var(--band);
        cursor: not-allowed;
      }
      .ph {
        color: var(--muted);
      }
    }
    .pop {
      position: absolute;
      z-index: 30;
      left: 0;
      right: 0;
      top: calc(100% + 4px);
      background: #fff;
      border: 1.5px solid var(--ink);
      border-radius: 8px;
      box-shadow: 0 12px 30px rgb(19 40 93 / 0.2);
      padding: 8px;
      input {
        margin-bottom: 6px;
      }
      ul {
        list-style: none;
        margin: 0;
        padding: 0;
        max-height: 240px;
        overflow-y: auto;
      }
      li button {
        width: 100%;
        display: flex;
        justify-content: space-between;
        gap: 12px;
        background: none;
        border: 0;
        padding: 8px 10px;
        border-radius: 6px;
        text-align: left;
        cursor: pointer;
        &:hover,
        &:focus-visible {
          background: var(--band);
        }
      }
      li[aria-selected='true'] button {
        font-weight: 600;
        color: var(--ink);
      }
      .k {
        font-size: 13px;
      }
      .none {
        padding: 8px 10px;
      }
    }
    .warn {
      display: block;
      color: var(--amber);
      font-weight: 600;
      margin-top: 5px;
      font-size: 13px;
    }
    .info {
      margin: 8px 0 0;
      padding: 8px 12px;
      background: var(--band);
      border-radius: 8px;
      border-left: 4px solid var(--teal);
      div {
        display: grid;
        grid-template-columns: 150px 1fr;
        gap: 8px;
        padding: 2px 0;
      }
      dt {
        color: var(--muted);
        font-size: 13px;
      }
      dd {
        margin: 0;
        color: var(--ink);
        font-weight: 600;
        .muted {
          font-weight: 400;
        }
      }
      .std {
        display: block;
        font-weight: 500;
      }
    }
  `,
})
export class MachineNamePicker implements OnInit {
  readonly value = model('');
  readonly label = input('Name');
  readonly inputId = input('mname');
  readonly disabled = input(false);
  /** Hide the Kind and Standard lines when the form shows them itself. */
  readonly showInfo = input(true);
  /** The catalogue entry that was chosen (or undefined when the name is not in the list). */
  readonly picked = output<MachineType>();

  protected readonly kinds = inject(MachineKinds);
  private readonly root = inject(ElementRef<HTMLElement>);
  private readonly wrap = viewChild<ElementRef<HTMLElement>>('wrap');
  protected readonly open = signal(false);
  protected readonly query = signal('');
  protected readonly found = computed(() => this.kinds.find(this.value()));
  protected readonly matches = computed(() => {
    const q = this.query().trim().toLowerCase();
    return this.kinds.types().filter((t) => !q || t.name.toLowerCase().includes(q) || t.aliases.some((a) => a.toLowerCase().includes(q)));
  });

  ngOnInit(): void {
    this.kinds.load();
  }

  protected toggle(): void {
    this.query.set('');
    this.open.update((o) => !o);
    if (this.open()) queueMicrotask(() => this.root.nativeElement.querySelector('input[type=search]')?.focus());
  }

  protected choose(t: MachineType): void {
    this.value.set(t.name);
    this.picked.emit(t);
    this.open.set(false);
  }

  protected chooseFirst(): void {
    const t = this.matches()[0];
    if (t) this.choose(t);
  }

  protected outside(e: Event): void {
    if (this.open() && !this.wrap()?.nativeElement.contains(e.target as Node)) this.open.set(false);
  }
}
