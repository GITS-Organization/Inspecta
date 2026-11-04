import { Injectable, inject } from '@angular/core';
import { Observable, catchError, from, map, mergeMap, of, switchMap, toArray } from 'rxjs';
import { BackendApi } from '../../api/backend.api';
import { CERTIFICATION_API } from '../../api/certification.api';
import { InspectionResult, MachineList } from '../../core/models';
import { problemMessage } from '../../core/problem';

export interface DirectPlan {
  clientId: string;
  machine: { equipmentId?: string; name: string; type?: string; serialNumber?: string; isGear: boolean };
  /** Gears only: every serial number of the batch, and how many certificates they are shared over. */
  serials: string[];
  certificates: number;
  examinedOn: string;
  nextExaminationOn: string;
  result: InspectionResult;
  /** false leaves every certificate a saved draft. */
  submit: boolean;
}

export interface DirectOutcome {
  requestId: string;
  machines: { requestItemId: string; name: string }[];
  submitted: number;
  /** Certificates that could not be submitted yet (for instance the checklist is not complete) and why. */
  pending: { requestItemId: string; name: string; reason: string }[];
}

/**
 * The steps behind "Send to technical manager": create the job, share the serial numbers over the certificates (gears),
 * then save the examination on every machine and submit it. A machine the backend will not submit yet stays a draft.
 */
@Injectable({ providedIn: 'root' })
export class DirectCertification {
  private readonly cert = inject(CERTIFICATION_API);
  private readonly api = inject(BackendApi);

  run(plan: DirectPlan): Observable<DirectOutcome> {
    const m = plan.machine;
    return this.cert
      .create({
        clientId: plan.clientId,
        machines: [
          {
            name: m.name,
            quantity: m.isGear ? Math.max(1, plan.serials.length) : 1,
            ...(m.equipmentId ? { equipmentId: m.equipmentId } : {}),
            ...(m.type ? { type: m.type } : {}),
            ...(!m.isGear && m.serialNumber ? { serialNumber: m.serialNumber } : {}),
          },
        ],
      })
      .pipe(
        switchMap((job) =>
          m.isGear && plan.serials.length
            ? this.cert.split(job.requestId, job.machines[0].requestItemId, plan.serials, plan.certificates)
            : of(job),
        ),
        switchMap((job) => this.fill(job, plan)),
      );
  }

  private fill(job: MachineList, plan: DirectPlan): Observable<DirectOutcome> {
    return from(job.machines).pipe(
      mergeMap(
        (machine) =>
          this.api
            .saveMachine(job.requestId, machine.requestItemId, {
              dates: { examination: plan.examinedOn, nextExamination: plan.nextExaminationOn },
              result: plan.result,
            })
            .pipe(
              switchMap(() => (plan.submit ? this.api.submitMachine(job.requestId, machine.requestItemId) : of(null))),
              map(() => ({ machine, error: null as string | null })),
              catchError((e: unknown) => of({ machine, error: problemMessage(e) as string | null })),
            ),
        4,
      ),
      toArray(),
      map((done) => ({
        requestId: job.requestId,
        machines: job.machines.map((x) => ({ requestItemId: x.requestItemId, name: x.name })),
        submitted: plan.submit ? done.filter((d) => !d.error).length : 0,
        pending: done
          .filter((d) => d.error)
          .map((d) => ({ requestItemId: d.machine.requestItemId, name: d.machine.name, reason: d.error as string })),
      })),
    );
  }
}
