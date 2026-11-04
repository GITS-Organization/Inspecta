/** The four roles of the backend (the `role` claim of the access token, snake_case). */
export type Role = 'admin' | 'technical_manager' | 'coordinator' | 'inspector';

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  isActive: boolean;
  managerId: string | null;
}

export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}

export type Session = AuthResult;

/** RFC 7807 body the API answers errors with. */
export interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export type JobStatus =
  | 'request_received'
  | 'quote_draft'
  | 'quote_sent'
  | 'counter_offer_sent'
  | 'client_countered'
  | 'quote_rejected'
  | 'client_declined'
  | 'unassigned'
  | 'assigned'
  | 'task_accepted'
  | 'in_progress'
  | 'certificate_submitted'
  | 'certificate_returned'
  | 'certificate_approved'
  | 'sent_to_client';

export type MachineStatus = 'not_started' | 'draft' | 'submitted' | 'returned' | 'approved' | 'delivered';
export type InspectionResult = 'satisfactory' | 'unsatisfactory';

export interface MachineProgress {
  total: number;
  submitted: number;
  approved: number;
  delivered: number;
}

/** `man_day` and `direct` jobs carry a placeholder quotation of price 0: never show it as money. */
export type RequestKind = 'standard' | 'man_day' | 'direct';

export interface QueueRow {
  id: string;
  kind?: RequestKind;
  requestId: string;
  requestStatus: JobStatus;
  quotationNumber: string | null;
  proposalNumber: string;
  clientName: string | null;
  location: string;
  totalUnits: number;
  inspectorId?: string | null;
  inspectorName?: string | null;
  scheduledAt?: string | null;
  queue?: string | null;
  certificates?: MachineProgress | null;
  updatedAt: string;
}

export type QueueFilter = 'unassigned' | 'assigned' | 'certify' | 'review' | 'returned' | 'approved' | 'delivered';

export interface MachineSummary {
  requestItemId: string;
  name: string;
  quantity: number;
  serialNumber: string | null;
  templateKey: string;
  status: MachineStatus;
  checklistTotal: number;
  checklistAnswered: number;
  reviewNote: string | null;
  certificateNumber: string | null;
  canEdit: boolean;
  canReview: boolean;
  /** The catalogue says this machine is a gear (inspected in numbers). */
  isGear?: boolean;
  /** Machines of one gear that was divided into certificates share this id. */
  divisionGroupId?: string | null;
  /** The quantity of the gear before the first division. */
  divisionTotal?: number | null;
  result?: InspectionResult | null;
  type?: string | null;
  templateName?: string | null;
  manufacturer?: string | null;
  capacity?: string | null;
  model?: string | null;
  inspectionStandards?: string[] | null;
  examinationDate?: string | null;
  nextExaminationDate?: string | null;
  pdfUrl?: string | null;
  qrUrl?: string | null;
}

export interface ReviewComment {
  at: string;
  by: string | null;
  requestItemId: string | null;
  machineName: string | null;
  text: string;
}

export interface MachineList {
  requestId: string;
  requestStatus: JobStatus;
  clientName: string;
  location: string;
  scheduledAt: string | null;
  progress: MachineProgress;
  canStartInspection: boolean;
  canDeliver: boolean;
  machines: MachineSummary[];
  canReview?: boolean;
  reviewHistory?: ReviewComment[] | null;
  canApprove?: boolean;
  reviewBlockers?: string[] | null;
  previewZipUrl?: string | null;
}

/** The details of one machine of a job: what `PUT .../machines/{id}/details` replaces and what a new machine is added with. */
export interface JobMachineDetails {
  name: string;
  quantity: number;
  type?: string | null;
  capacity?: string | null;
  serialNumber?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  location?: string | null;
  issueDescription?: string | null;
}

/** `POST .../machines`: a registered machine of the client, or a new one by its details. */
export type AddJobMachine = { equipmentId: string } | JobMachineDetails;

/** One certificate of a divided gear: its own quantity and its serial numbers written together as free text. */
export interface CertificateShare {
  quantity: number;
  serialNumbers: string;
}

/** A job's machines can change from the assignment until the certificates are submitted. */
export const MACHINES_OPEN: JobStatus[] = ['assigned', 'task_accepted', 'in_progress', 'certificate_returned'];

export interface MachineFormField {
  key: string;
  label: string;
  value: string | null;
}
export interface MachineFormRow {
  key: string;
  label: string;
  value: string | null;
  note: string | null;
}
export interface MachineFormSection {
  key: string;
  title: string;
  items: MachineFormRow[];
}

export interface MachineForm {
  requestId: string;
  requestItemId: string;
  requestStatus: JobStatus;
  status: MachineStatus;
  canEdit: boolean;
  canReview: boolean;
  readyToSubmit: boolean;
  missing: string[];
  templateKey: string;
  templateName: string;
  header: {
    clientName: string;
    clientAddress: string | null;
    clientRepresentative: string | null;
    inspectedItem: string;
    standards: string[];
    lastExaminationDate: string | null;
    nextExaminationDue: string | null;
    inspectorName: string | null;
  };
  fields: MachineFormField[];
  checklist: MachineFormSection[];
  answers: Record<string, boolean>;
  texts: Record<string, string>;
  dates: Record<string, string>;
  reviewNote: string | null;
  certificateNumber: string | null;
  result?: InspectionResult | null;
}

export type ChecklistValue = 'yes' | 'no' | 'na';

export interface SaveMachineFormRequest {
  fields?: Record<string, string>;
  checklist?: Record<string, { value: ChecklistValue; note?: string | null }>;
  answers?: Record<string, boolean>;
  texts?: Record<string, string>;
  dates?: Record<string, string>;
  result?: InspectionResult | null;
}

export interface ReturnJobRequest {
  note: string;
  machines?: { requestItemId: string; comment: string }[];
}

export interface RequestStatusCount {
  status: JobStatus;
  count: number;
}

export interface DashboardOverview {
  period: { from: string; to: string; chartFrom: string };
  attention: { equipmentOverdue: number; equipmentDueSoon: number; jobsAwaitingReview: number };
  pipeline: RequestStatusCount[];
  certificates: {
    thisMonth: number;
    previousMonth: number;
    byMonth: { month: string; safe: number; withConditions: number; notSafe: number; total: number }[];
  };
  equipmentDue: {
    total: number;
    overdue: number;
    items: {
      id: string;
      name: string;
      serialNumber: string | null;
      clientId: string;
      clientName: string;
      dueAt: string;
      daysUntilDue: number;
      overdue: boolean;
    }[];
  };
  inspectorWorkload: { inspectorId: string; name: string; certificates: number; quotationsDone: number }[];
}
