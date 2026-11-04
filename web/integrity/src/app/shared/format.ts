import { InspectionResult, JobStatus, MachineStatus } from '../core/models';

/** `2026-05-14` or an ISO timestamp -> `14 May 2026`. Empty when there is no date. */
export function formatDate(value: string | null | undefined): string {
  if (!value) return '';
  const d = new Date(value.length <= 10 ? value + 'T00:00' : value);
  return isNaN(d.getTime()) ? '' : d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
}

export const RESULT_LABEL: Record<InspectionResult, string> = { satisfactory: 'Safe', unsatisfactory: 'Not safe' };
export const resultChip = (r: InspectionResult | null | undefined): string => (r === 'unsatisfactory' ? 'r' : r ? 'g' : '');

export const MACHINE_STATUS_LABEL: Record<MachineStatus, string> = {
  not_started: 'Not started',
  draft: 'Draft saved',
  submitted: 'Submitted',
  returned: 'Returned with a note',
  approved: 'Approved',
  delivered: 'Delivered',
};
export const machineChip = (s: MachineStatus): string =>
  s === 'returned' ? 'r' : s === 'approved' || s === 'delivered' || s === 'submitted' ? 'g' : s === 'draft' ? 'a' : '';

export const STAGE_LABEL: Partial<Record<JobStatus, string>> = {
  request_received: 'Offers',
  quote_draft: 'Drafting offer',
  quote_sent: 'Offer sent',
  counter_offer_sent: 'Counter offer sent',
  client_countered: 'Client countered',
  quote_rejected: 'Offer rejected',
  client_declined: 'Client declined',
  unassigned: 'Unassigned',
  assigned: 'Assigned',
  task_accepted: 'Accepted',
  in_progress: 'In progress',
  certificate_submitted: 'Review',
  certificate_returned: 'Returned',
  certificate_approved: 'Approved',
  sent_to_client: 'Sent to client',
};

export function plural(n: number, one: string, many = one + 's'): string {
  return `${n} ${n === 1 ? one : many}`;
}
