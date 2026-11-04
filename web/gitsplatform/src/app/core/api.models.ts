export type UserRole = 'coordinator' | 'inspector' | 'technical_manager' | 'admin';
export type SubscriptionStatus = 'active' | 'suspended' | 'expired';

export interface AuthUser {
  id: string;
  email: string;
  fullName: string;
  role: UserRole;
  isActive: boolean;
  qualifications: string[];
  isOnLeave: boolean;
  leaveUntil: string | null;
  managerId: string | null;
  createdById: string | null;
  subscriptionStatus: SubscriptionStatus | null;
  subscriptionEndDate: string | null;
}

export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}

/** Body of POST /technical-managers (as `companyProfile`) and PUT /admin/managers/{id}/company-profile. Every field is optional. */
export interface SaveCompanyProfileRequest {
  name?: string;
  certificateTitle?: string;
  issuerName?: string;
  tagline?: string;
  address?: string;
  phones?: string[];
  emails?: string[];
  website?: string;
  signerName?: string;
  primaryColor?: string;
  accentColor?: string;
  /** Raw base64 of a PNG (no `data:` prefix), at most 1 MB. */
  logoBase64?: string;
  accreditationsBase64?: string;
}

export interface CreateTechnicalManagerRequest {
  email: string;
  fullName: string;
  password: string;
  subscriptionMonths: number;
  companyProfile?: SaveCompanyProfileRequest;
}

export interface CompanyProfile {
  managerId: string;
  name: string;
  certificateTitle: string | null;
  issuerName: string | null;
  tagline: string | null;
  address: string;
  phones: string[];
  emails: string[];
  website: string;
  signerName: string | null;
  primaryColor: string;
  accentColor: string;
  hasLogo: boolean;
  hasAccreditations: boolean;
  logoUrl: string | null;
  updatedAt: string;
  usesBuiltInDetails: boolean;
}

export interface CertificateVerification {
  valid: boolean;
  status: string;
  certificateNumber: string | null;
  itemName: string | null;
  clientName: string | null;
  examinationDate: string | null;
  nextExaminationDate: string | null;
  issuedAt: string | null;
  issuedBy: string | null;
  pdfUrl: string | null;
  result: string | null;
}

export interface CleanupEmailBacklogRequest {
  createdBefore: string;
  managerId?: string;
  dryRun: boolean;
}

export interface CleanupEmailBacklogResponse {
  matched: number;
  deleted: number;
  dryRun: boolean;
}

/** One row of the companies list: a technical manager, the team around them and their company profile if any. */
export interface CompanyRow {
  manager: AuthUser;
  profile: CompanyProfile | null;
  inspectors: number;
  coordinators: number;
}
