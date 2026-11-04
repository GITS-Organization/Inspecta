import { CreateTechnicalManagerRequest, SaveCompanyProfileRequest } from '../core/api.models';

export const MAX_IMAGE_BYTES = 1024 * 1024;
export const MAX_LIST_ITEMS = 5;
export const MIN_PASSWORD_LENGTH = 12;
export const HEX_COLOR = /^#[0-9a-fA-F]{6}$/;

/** What the "Add a company" form holds, before it becomes the API body. */
export interface CompanyFormValue {
  fullName: string;
  email: string;
  password: string;
  subscriptionMonths: number;
  /** Keep the built-in company details (the first customer's) and change only the images and colors. */
  useBuiltInDetails: boolean;
  name: string;
  address: string;
  certificateTitle: string;
  issuerName: string;
  tagline: string;
  website: string;
  signerName: string;
  /** One per line or comma separated. */
  phones: string;
  emails: string;
  primaryColor: string;
  accentColor: string;
  logoBase64: string | null;
  accreditationsBase64: string | null;
}

export function splitList(text: string): string[] {
  return text.split(/[\n,;]/).map((item) => item.trim()).filter((item) => item.length > 0);
}

/** Builds POST /technical-managers. `name` and `address` go together or not at all; empty optional text is left out. */
export function buildCreateRequest(value: CompanyFormValue): CreateTechnicalManagerRequest {
  const profile: SaveCompanyProfileRequest = {
    primaryColor: value.primaryColor,
    accentColor: value.accentColor,
  };
  if (!value.useBuiltInDetails) {
    profile.name = value.name.trim();
    profile.address = value.address.trim();
    const optional = {
      certificateTitle: value.certificateTitle,
      issuerName: value.issuerName,
      tagline: value.tagline,
      website: value.website,
      signerName: value.signerName,
    } as const;
    for (const [key, text] of Object.entries(optional)) {
      if (text.trim()) profile[key as keyof typeof optional] = text.trim();
    }
    const phones = splitList(value.phones);
    const emails = splitList(value.emails);
    if (phones.length > 0) profile.phones = phones;
    if (emails.length > 0) profile.emails = emails;
  }
  if (value.logoBase64) profile.logoBase64 = value.logoBase64;
  if (value.accreditationsBase64) profile.accreditationsBase64 = value.accreditationsBase64;

  return {
    email: value.email.trim(),
    fullName: value.fullName.trim(),
    password: value.password,
    subscriptionMonths: value.subscriptionMonths,
    companyProfile: profile,
  };
}

/** Checks what the form can check before the server does; an empty list means the request may be sent. */
export function validateCompanyForm(value: CompanyFormValue): string[] {
  const problems: string[] = [];
  if (!value.fullName.trim()) problems.push("Enter the technical manager's full name.");
  if (!/^\S+@\S+\.\S+$/.test(value.email.trim())) problems.push("Enter the technical manager's email address.");
  if (value.password.length < MIN_PASSWORD_LENGTH) problems.push(`The password needs at least ${MIN_PASSWORD_LENGTH} characters.`);
  if (!(value.subscriptionMonths >= 1 && value.subscriptionMonths <= 120)) problems.push('Subscription length is 1 to 120 months.');
  if (!value.useBuiltInDetails) {
    if (!value.name.trim()) problems.push('Enter the company name.');
    if (!value.address.trim()) problems.push('Enter the company address.');
    if (splitList(value.phones).length > MAX_LIST_ITEMS) problems.push(`Give at most ${MAX_LIST_ITEMS} phone numbers.`);
    if (splitList(value.emails).length > MAX_LIST_ITEMS) problems.push(`Give at most ${MAX_LIST_ITEMS} company emails.`);
  }
  if (!HEX_COLOR.test(value.primaryColor) || !HEX_COLOR.test(value.accentColor)) problems.push('Colors must look like #RRGGBB.');
  return problems;
}

/** Reads a PNG file as raw base64 (what the API expects, without the `data:` prefix). Rejects with a plain message. */
export function readPngAsBase64(file: File): Promise<string> {
  if (file.type !== 'image/png') return Promise.reject(new Error('Choose a PNG image.'));
  if (file.size > MAX_IMAGE_BYTES) return Promise.reject(new Error('The image is larger than 1 MB. Choose a smaller PNG.'));
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('The file could not be read. Try another one.'));
    reader.onload = () => resolve(String(reader.result).replace(/^data:[^,]*,/, ''));
    reader.readAsDataURL(file);
  });
}

export function initials(name: string): string {
  const letters = name.trim().split(/\s+/).filter(Boolean).map((word) => word[0]).join('').slice(0, 2).toUpperCase();
  return letters || '··';
}
