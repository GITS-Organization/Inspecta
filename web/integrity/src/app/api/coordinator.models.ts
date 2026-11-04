import { JobStatus, MachineProgress } from '../core/models';

export type Triage = 'request' | 'maybe';

export interface RequestItem {
  id: string;
  equipmentId: string | null;
  name: string;
  type: string | null;
  capacity: string | null;
  quantity: number;
  serialNumber: string | null;
  issueDescription: string | null;
  location: string | null;
}

export interface RequestHistory {
  from: JobStatus | null;
  to: JobStatus;
  actorId: string;
  at: string;
  note: string | null;
}

export interface QuoteItem {
  requestItemId: string | null;
  description: string;
  type: string;
  prNumber: string | null;
  quantity: number;
  unitPricePiastres: number | null;
  pricingUnit: string;
  totalPiastres: number;
}

export interface QuoteEmail {
  kind: string;
  status: 'pending' | 'sent' | 'failed';
  toAddress: string;
  attempts: number;
  sentAt: string | null;
  lastError: string | null;
  cc?: string[] | null;
  subject?: string | null;
}

export interface ClientReply {
  id: string;
  quoteId: string | null;
  fromAddress: string | null;
  fromName: string | null;
  subject: string | null;
  receivedAt: string;
  text: string;
  html: string | null;
  attachments: { fileName: string; contentType: string; size: number }[];
}

export interface Quote {
  id: string;
  requestId: string;
  version: number;
  amountPiastres: number;
  validUntil: string;
  status: 'open' | 'accepted' | 'rejected';
  clientReply: string | null;
  createdAt: string;
  proposalNumber: string;
  clientName: string | null;
  validForDays: number;
  message: string | null;
  items: QuoteItem[] | null;
  scopeOfWork: string[] | null;
  notes: string[] | null;
  missingPrices: string[] | null;
  totalUnits: number;
  canEdit: boolean;
  isExpired: boolean;
  sentAt: string | null;
  updatedAt: string;
  supersededAt: string | null;
  emails: QuoteEmail[] | null;
  contactName: string | null;
  quotationNumber: string | null;
  clientReplies: ClientReply[] | null;
}

export interface RequestDetails {
  id: string;
  clientId: string | null;
  clientName: string;
  location: string;
  subject: string | null;
  receivedAt: string;
  preferredDate: string | null;
  status: JobStatus;
  coordinatorId: string | null;
  inspectorId: string | null;
  scheduledAt: string | null;
  source: 'manual' | 'email';
  emailFrom: string | null;
  emailPreview: string | null;
  items: RequestItem[];
  history: RequestHistory[];
  managerId: string | null;
  emailFromName: string | null;
  accessNotes: string | null;
  missingFields: string[];
  canEditDetails: boolean;
  updatedAt: string;
  triage: Triage;
  maybeExpiresAt: string | null;
  quotation: Quote | null;
  client: { id: string; name: string; location: string; hasContract: boolean } | null;
  hasUnreadClientReply: boolean;
}

export interface RequestSummary {
  requests: number;
  maybe: number;
  byStatus: { status: JobStatus; count: number }[];
}

export interface MachineSuggestion {
  equipmentId: string;
  name: string;
  quantity: number;
  confidence: number;
  level: 'high' | 'medium' | 'ambiguous';
  reason: string;
  evidence: string;
}

export interface RequestEmail {
  requestId: string;
  fromAddress: string | null;
  fromName: string | null;
  subject: string | null;
  to: { address: string; name: string | null }[];
  cc: { address: string; name: string | null }[];
  sentAt: string | null;
  receivedAt: string;
  textBody: string | null;
  attachments: { fileName: string; contentType: string; size: number }[];
}

export interface SaveRequestItem {
  id?: string | null;
  name: string;
  quantity: number;
  equipmentId?: string | null;
  type?: string | null;
  capacity?: string | null;
  serialNumber?: string | null;
  location?: string | null;
  issueDescription?: string | null;
}

export interface SaveRequestDetails {
  clientId?: string | null;
  clientName?: string | null;
  location?: string | null;
  preferredDate?: string | null;
  accessNotes?: string | null;
  items?: SaveRequestItem[] | null;
  expectedUpdatedAt?: string | null;
  prepareQuotation?: boolean | null;
}

export interface CreateRequest {
  clientId?: string | null;
  clientName: string;
  location: string;
  subject?: string | null;
  preferredDate?: string | null;
  accessNotes?: string | null;
  items: SaveRequestItem[];
}

export type QuoteStage = 'draft' | 'sent' | 'accepted' | 'rejected';

export interface QuoteRow {
  id: string;
  requestId: string;
  version: number;
  stage: QuoteStage;
  status: 'open' | 'accepted' | 'rejected';
  requestStatus: JobStatus;
  quotationNumber: string | null;
  proposalNumber: string;
  clientName: string | null;
  location: string;
  amountPiastres: number | null;
  totalUnits: number;
  missingPrices: number;
  validUntil: string;
  isExpired: boolean;
  isCurrent: boolean;
  canEdit: boolean;
  sentAt: string | null;
  updatedAt: string;
  inspectorName?: string | null;
  scheduledAt?: string | null;
  certificates?: MachineProgress | null;
  hasUnreadClientReply: boolean;
}

export interface UpdateQuote {
  items: { requestItemId: string; unitPricePiastres: number | null }[];
  validForDays?: number | null;
  scopeOfWork?: string[] | null;
  notes?: string[] | null;
  message?: string | null;
  proposalNumber?: string | null;
  contactName?: string | null;
}

export interface EmailAttachmentUpload {
  fileName: string;
  contentBase64: string;
}

export interface SendEmail {
  sentTo?: string | null;
  cc?: string[] | null;
  subject?: string | null;
  message?: string | null;
  attachments?: EmailAttachmentUpload[] | null;
}

export interface QuoteEmailPreview {
  action: string;
  emailEnabled: boolean;
  to: string | null;
  toName: string | null;
  cc: string[];
  subject: string;
  body: string;
  from: string | null;
  attachmentName: string;
  pdfUrl: string;
  repliesToOriginalEmail: boolean;
  notice: string | null;
  canSend: boolean;
  blockers: string[];
  bodyHtml?: string | null;
}

export interface Inspector {
  id: string;
  fullName: string;
  email: string;
  qualifications: string[];
  isOnLeave: boolean;
  leaveUntil: string | null;
  activeJobs: number;
}

export interface DeliveryPreview {
  emailEnabled: boolean;
  to: string | null;
  toName: string | null;
  subject: string;
  body: string;
  attachments: string[];
  repliesToOriginalEmail: boolean;
  notice: string | null;
  cc?: string[] | null;
  from?: string | null;
  canSend: boolean;
  blockers?: string[] | null;
  alreadyDelivered: boolean;
  testEmailTo?: string | null;
  certificates?: {
    requestItemId: string | null;
    itemName: string;
    certificateNumber: string;
    result: 'satisfactory' | 'unsatisfactory' | null;
    examinationDate: string | null;
    nextExaminationDate: string | null;
    fileName: string;
    sizeBytes: number;
    pdfUrl: string;
    verifyUrl: string | null;
  }[];
}

export interface Contact {
  id?: string;
  name: string;
  email: string | null;
  phone: string | null;
  isPrimary: boolean;
}

export interface Client {
  id: string;
  name: string;
  isCompany: boolean;
  hasContract: boolean;
  location: string | null;
  latitude: number | null;
  longitude: number | null;
  certificatesEmail: string | null;
  taxNumber: string | null;
  notes: string | null;
  createdAt: string;
  archivedAt: string | null;
  contacts: (Contact & { id: string })[];
}

export interface SaveClient {
  name: string;
  isCompany: boolean;
  hasContract: boolean;
  location?: string | null;
  certificatesEmail?: string | null;
  taxNumber?: string | null;
  notes?: string | null;
  contacts?: Contact[] | null;
}

export interface MailRule {
  id: string;
  pattern: string;
  isDomain: boolean;
  clientId: string | null;
  createdAt: string;
  promotedRequests?: number;
}

export type ItemType = 'gear' | 'equipment' | 'non_lifting';

export interface Equipment {
  id: string;
  clientId: string;
  name: string;
  category: string | null;
  manufacturer: string | null;
  model: string | null;
  serialNumber: string | null;
  capacity: string | null;
  itemType: ItemType;
  locationInsideClientLocation: string | null;
  notes: string | null;
  lastInspectedOn: string | null;
  nextInspectionDueAt: string | null;
  archivedAt: string | null;
  inspectionStandard?: string | null;
  testType?: string | null;
}

export interface SaveEquipment {
  clientId: string;
  name: string;
  itemType?: ItemType | null;
  category?: string | null;
  manufacturer?: string | null;
  model?: string | null;
  serialNumber?: string | null;
  capacity?: string | null;
  locationInsideClientLocation?: string | null;
  inspectionStandard?: string | null;
  testType?: string | null;
  notes?: string | null;
}

export interface IssuedCertificate {
  id: string;
  requestId: string;
  certificateNumber: string;
  itemName: string;
  examinationDate: string | null;
  nextExaminationDate: string | null;
  issuedAt: string;
  fileName: string;
  verificationCode: string;
  qrUrl?: string | null;
  result?: 'satisfactory' | 'unsatisfactory' | null;
  delivery?: { emailSent: boolean; sentTo: string | null; cc: string[]; subject: string | null; sentAt: string } | null;
}

export interface ClientHistory {
  clientId: string;
  requests: { request: RequestDetails; issuedCertificates?: IssuedCertificate[] | null }[];
}
