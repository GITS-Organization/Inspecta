import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable, switchMap } from 'rxjs';
import { BackendApi } from '../../api/backend.api';
import { ChecklistValue, InspectionResult, MachineForm, SaveMachineFormRequest } from '../../core/models';
import { problemMessage } from '../../core/problem';
import { ToastService } from '../../core/toast.service';
import { CertificatePaper } from '../../shared/certificate-paper';
import { formatDate } from '../../shared/format';
import { MachineKinds } from '../../shared/machine-kinds';

export const ANSWER_LABELS: Record<string, string> = {
  firstExamination: 'First examination after installation',
  installedCorrectly: 'Installed correctly',
  within6Months: 'Examined within the last 6 months',
  within12Months: 'Examined within the last 12 months',
  examinationScheme: 'Examined under a written scheme of examination',
  exceptionalCircumstances: 'Exceptional circumstances affected the item',
  existingDanger: 'A defect is an existing danger',
  futureDanger: 'A defect will become a danger',
};

/** Two pairs of questions always have opposite answers: choosing one sets the other (the API does the same). */
export const ANSWER_PAIRS: readonly (readonly [string, string])[] = [
  ['firstExamination', 'installedCorrectly'],
  ['within6Months', 'within12Months'],
];
const OPPOSITE: Record<string, string> = Object.fromEntries(ANSWER_PAIRS.flatMap(([a, b]) => [[a, b], [b, a]]));

export const TEXT_LABELS: Record<string, string> = {
  testType: 'Type of test',
  testsCarriedOut: 'Tests carried out',
  defectDescription: 'Defects found',
  repairsRequired: 'Repairs required',
  conclusion: 'Conclusion',
  note: 'Remarks',
  clientRepresentative: 'Client representative',
};
const LONG_TEXTS = new Set(['testsCarriedOut', 'defectDescription', 'repairsRequired', 'conclusion', 'note']);

/** The free texts that share the one page of a gear certificate with its serial numbers. */
export const BUDGET_TEXTS = ['defectDescription', 'repairsRequired', 'testsCarriedOut', 'note'] as const;
export const DEFAULT_PAGE_BUDGET = 1200;

/** Characters a text may still take: the budget minus everything else that is on the page. Never negative. */
export function roomFor(budget: number, others: number): number {
  return Math.max(0, budget - others);
}

interface Row {
  value: ChecklistValue | null;
  note: string;
}

/** The inspector's form for one machine (also read-only for the technical manager). */
@Component({
  selector: 'app-machine-form',
  imports: [RouterLink, CertificatePaper],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './machine-form.html',
  styleUrl: './machine-form.scss',
})
export class MachineFormPage implements OnInit {
  readonly requestId = input.required<string>();
  readonly itemId = input.required<string>();

  private readonly api = inject(BackendApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly formatDate = formatDate;
  protected readonly answerKeys = Object.keys(ANSWER_LABELS);
  protected readonly pairs = ANSWER_PAIRS;
  protected readonly singles = Object.keys(ANSWER_LABELS).filter((k) => !OPPOSITE[k]);
  protected readonly answerLabels = ANSWER_LABELS;
  protected readonly textKeys = Object.keys(TEXT_LABELS);
  protected readonly textLabels = TEXT_LABELS;
  private readonly kinds = inject(MachineKinds);
  protected readonly budget = signal(DEFAULT_PAGE_BUDGET);
  protected readonly cutNote = signal<string | null>(null);
  protected readonly isLong = (k: string) => LONG_TEXTS.has(k);

  protected readonly form = signal<MachineForm | null>(null);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);

  protected readonly fields = signal<Record<string, string>>({});
  protected readonly rows = signal<Record<string, Row>>({});
  protected readonly answers = signal<Record<string, boolean | null>>({});
  protected readonly texts = signal<Record<string, string>>({});
  protected readonly dates = signal<Record<string, string>>({});
  protected readonly result = signal<InspectionResult | null>(null);

  protected readonly editable = computed(() => this.form()?.canEdit ?? false);
  protected readonly unsafe = computed(
    () => this.result() === 'unsatisfactory' || Object.values(this.rows()).some((r) => r.value === 'no'),
  );
  protected readonly stamp = computed<'safe' | 'no' | 'dash'>(() => (this.unsafe() ? 'no' : this.result() ? 'safe' : 'dash'));
  protected readonly answered = computed(() => Object.values(this.rows()).filter((r) => r.value).length);
  protected readonly total = computed(() => Object.keys(this.rows()).length);
  protected readonly serialValue = computed(() => {
    const f = this.form();
    const hit = f?.fields.find((x) => /serial/i.test(x.key));
    return hit ? (this.fields()[hit.key] ?? '') : '';
  });
  protected readonly manufacturerValue = computed(() => {
    const hit = this.form()?.fields.find((x) => /manufacturer/i.test(x.key));
    return hit ? (this.fields()[hit.key] ?? '') : '';
  });

  /** A gear (a divided gear certificate is one too): its page has a character budget. */
  protected readonly gear = computed(() => {
    const f = this.form();
    return !!f && this.kinds.isGear(f.header.inspectedItem);
  });
  private serialKey(): string | null {
    return this.form()?.fields.find((x) => /serial/i.test(x.key))?.key ?? null;
  }
  /** The serial text plus the four free texts. */
  protected readonly used = computed(() => this.serialValue().length + BUDGET_TEXTS.reduce((a, k) => a + (this.texts()[k] ?? '').length, 0));

  ngOnInit(): void {
    this.kinds.load();
    this.api.serialLimits().subscribe({ next: (l) => l.pageBudgetCharacters && this.budget.set(l.pageBudgetCharacters), error: () => undefined });
    this.api.machine(this.requestId(), this.itemId()).subscribe({
      next: (f) => {
        this.apply(f);
        this.loading.set(false);
      },
      error: (e: unknown) => {
        this.error.set(problemMessage(e, 'The machine could not be loaded.'));
        this.loading.set(false);
      },
    });
  }

  private apply(f: MachineForm): void {
    this.form.set(f);
    this.fields.set(Object.fromEntries(f.fields.map((x) => [x.key, x.value ?? ''])));
    this.rows.set(
      Object.fromEntries(
        f.checklist.flatMap((s) =>
          s.items.map((i) => [i.key, { value: (['yes', 'no', 'na'] as const).find((v) => v === i.value) ?? null, note: i.note ?? '' }] as const),
        ),
      ),
    );
    this.answers.set(Object.fromEntries(this.answerKeys.map((k) => [k, f.answers[k] ?? null])));
    this.texts.set({ ...f.texts });
    this.dates.set({ ...f.dates });
    this.result.set(f.result ?? null);
  }

  protected setField(k: string, v: string, el?: HTMLInputElement | HTMLTextAreaElement): void {
    if (this.gear() && k === this.serialKey()) {
      const room = roomFor(this.budget(), this.used() - this.serialValue().length);
      if (v.length > room) {
        v = v.slice(0, room);
        if (el) el.value = v;
        this.cutNote.set('The serial numbers were cut: with the texts, a gear certificate holds ' + this.budget() + ' characters on its one page.');
      } else this.cutNote.set(null);
    }
    this.fields.update((m) => ({ ...m, [k]: v }));
    this.saved.set(false);
  }
  protected setText(k: string, v: string, el?: HTMLInputElement | HTMLTextAreaElement): void {
    if (this.gear() && (BUDGET_TEXTS as readonly string[]).includes(k)) {
      const room = roomFor(this.budget(), this.used() - (this.texts()[k] ?? '').length);
      if (v.length > room) {
        v = v.slice(0, room);
        if (el) el.value = v;
        this.cutNote.set('The text was cut: the serial numbers and the texts of a gear certificate share ' + this.budget() + ' characters, one page.');
      } else this.cutNote.set(null);
    }
    this.texts.update((m) => ({ ...m, [k]: v }));
    this.saved.set(false);
  }
  protected setDate(k: string, v: string): void {
    this.dates.update((m) => ({ ...m, [k]: v }));
    this.saved.set(false);
  }
  protected setAnswer(k: string, v: boolean): void {
    const other = OPPOSITE[k];
    this.answers.update((m) => ({ ...m, [k]: v, ...(other ? { [other]: !v } : {}) }));
    this.saved.set(false);
  }
  protected setRow(k: string, patch: Partial<Row>): void {
    this.rows.update((m) => ({ ...m, [k]: { ...m[k], ...patch } }));
    this.saved.set(false);
  }
  protected setResult(r: InspectionResult): void {
    this.result.set(r);
    this.saved.set(false);
  }

  /** What `PUT .../machines/{requestItemId}` receives. Unanswered checklist rows and questions are left out. */
  buildRequest(): SaveMachineFormRequest {
    const checklist: NonNullable<SaveMachineFormRequest['checklist']> = {};
    for (const [k, r] of Object.entries(this.rows())) {
      if (r.value) checklist[k] = r.value === 'no' ? { value: 'no', note: r.note.trim() || null } : { value: r.value };
    }
    const answers: Record<string, boolean> = {};
    for (const [k, v] of Object.entries(this.answers())) if (v !== null) answers[k] = v;
    const texts = Object.fromEntries(Object.entries(this.texts()).filter(([, v]) => v.trim() !== ''));
    const dates = Object.fromEntries(Object.entries(this.dates()).filter(([, v]) => v));
    return { fields: this.fields(), checklist, answers, texts, dates, result: this.result() };
  }

  protected save(): void {
    this.run(this.api.saveMachine(this.requestId(), this.itemId(), this.buildRequest()), 'Draft saved.');
  }

  protected submit(): void {
    const body = this.buildRequest();
    this.run(
      this.api.saveMachine(this.requestId(), this.itemId(), body).pipe(switchMap(() => this.api.submitMachine(this.requestId(), this.itemId()))),
      'Sent to your technical manager for review.',
      true,
    );
  }

  private run(call: Observable<MachineForm>, message: string, leave = false): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    call.subscribe({
      next: (f) => {
        this.apply(f);
        this.busy.set(false);
        this.saved.set(true);
        this.toast.show(message);
        if (leave) void this.router.navigate(['/jobs', this.requestId()]);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.error.set(problemMessage(e));
      },
    });
  }
}
