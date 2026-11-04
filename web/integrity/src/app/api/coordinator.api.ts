import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { MachineList, QueueFilter, QueueRow } from '../core/models';
import {
  Client,
  ClientHistory,
  CreateManDay,
  CreateRequest,
  DeliveryPreview,
  Equipment,
  Inspector,
  IssuedCertificate,
  MachineSuggestion,
  MailRule,
  Quote,
  QuoteEmailPreview,
  QuoteRow,
  QuoteStage,
  RequestDetails,
  RequestEmail,
  RequestSummary,
  SaveClient,
  SaveEquipment,
  SaveRequestDetails,
  SendEmail,
  Triage,
  UpdateQuote,
} from './coordinator.models';

const params = (o: Record<string, string | number | boolean | null | undefined>): HttpParams => {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(o)) if (v !== null && v !== undefined && v !== '') p = p.set(k, String(v));
  return p;
};

/** Every call the coordinator screens make. Paths are relative to `/api/v1`. */
@Injectable({ providedIn: 'root' })
export class CoordinatorApi {
  private readonly http = inject(HttpClient);
  private readonly b = environment.apiBase;

  /** Turns a url the API returned (`/api/v1/...`) into one this app can call with the bearer token. */
  apiUrl(url: string): string {
    return url.startsWith('/api/v1') ? this.b + url.slice('/api/v1'.length) : url;
  }
  file(url: string): Observable<Blob> {
    return this.http.get(this.apiUrl(url), { responseType: 'blob' });
  }

  // ---- requests
  requests(f: { triage?: Triage; status?: string; q?: string; page?: number; pageSize?: number; from?: string; to?: string }): Observable<RequestDetails[]> {
    return this.http.get<RequestDetails[]>(`${this.b}/requests`, { params: params({ pageSize: 100, ...f }) });
  }
  requestSummary(from?: string, to?: string): Observable<RequestSummary> {
    return this.http.get<RequestSummary>(`${this.b}/requests/summary`, { params: params({ from, to }) });
  }
  request(id: string): Observable<RequestDetails> {
    return this.http.get<RequestDetails>(`${this.b}/requests/${id}`);
  }
  createRequest(body: CreateRequest): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(`${this.b}/requests`, body);
  }
  saveRequest(id: string, body: SaveRequestDetails): Observable<RequestDetails> {
    return this.http.put<RequestDetails>(`${this.b}/requests/${id}`, body);
  }
  suggestions(id: string): Observable<MachineSuggestion[]> {
    return this.http.get<MachineSuggestion[]>(`${this.b}/requests/${id}/suggestions`);
  }
  autofill(id: string): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(`${this.b}/requests/${id}/autofill`, {});
  }
  requestEmail(id: string): Observable<RequestEmail> {
    return this.http.get<RequestEmail>(`${this.b}/requests/${id}/email`);
  }
  confirmMaybe(id: string): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(`${this.b}/requests/${id}/confirm`, {});
  }
  deleteMaybe(id: string): Observable<void> {
    return this.http.delete<void>(`${this.b}/requests/${id}`);
  }
  rejectRequest(id: string, reason: string): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(`${this.b}/requests/${id}/reject`, { reason });
  }
  assign(id: string, inspectorId: string, scheduledAt: string, note?: string): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(`${this.b}/requests/${id}/assignment`, { inspectorId, scheduledAt, note: note || null });
  }
  changeInspectionDate(requestId: string, scheduledAt: string, note?: string): Observable<RequestDetails> {
    return this.http.put<RequestDetails>(`${this.b}/quotation-queue/${requestId}/inspection-date`, { scheduledAt, note: note || null });
  }
  /** Admin passes the team's manager id. */
  inspectors(managerId?: string): Observable<Inspector[]> {
    return this.http.get<Inspector[]>(`${this.b}/inspectors`, { params: params({ managerId }) });
  }
  /** Contract clients: gives the request to an inspector at once, with no quotation. */
  assignWithoutQuotation(id: string, inspectorId: string, scheduledAt: string, note?: string): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(`${this.b}/requests/${id}/assign-without-quotation`, { inspectorId, scheduledAt, note: note || null });
  }
  createManDay(body: CreateManDay): Observable<RequestDetails> {
    return this.http.post<RequestDetails>(`${this.b}/requests/man-day`, body);
  }

  // ---- quotations
  quotations(f: { stage?: QuoteStage; q?: string; includeSuperseded?: boolean; from?: string; to?: string }): Observable<QuoteRow[]> {
    return this.http.get<QuoteRow[]>(`${this.b}/quotations`, { params: params({ ...f, pageSize: 100 }) });
  }
  quote(requestId: string, quoteId: string): Observable<Quote> {
    return this.http.get<Quote>(`${this.b}/requests/${requestId}/quotes/${quoteId}`);
  }
  quotes(requestId: string): Observable<Quote[]> {
    return this.http.get<Quote[]>(`${this.b}/requests/${requestId}/quotes`);
  }
  saveQuote(requestId: string, quoteId: string, body: UpdateQuote): Observable<Quote> {
    return this.http.put<Quote>(`${this.b}/requests/${requestId}/quotes/${quoteId}`, body);
  }
  quotePdf(requestId: string, quoteId: string): Observable<Blob> {
    return this.http.get(`${this.b}/requests/${requestId}/quotes/${quoteId}/pdf`, { responseType: 'blob' });
  }
  quoteEmail(requestId: string, quoteId: string, action: 'send' | 'accept'): Observable<QuoteEmailPreview> {
    return this.http.get<QuoteEmailPreview>(`${this.b}/requests/${requestId}/quotes/${quoteId}/email`, { params: { action } });
  }
  sendQuote(requestId: string, quoteId: string, body: SendEmail): Observable<Quote> {
    return this.http.post<Quote>(`${this.b}/requests/${requestId}/quotes/${quoteId}/send`, body);
  }
  acceptQuote(requestId: string, quoteId: string, body: SendEmail): Observable<Quote> {
    return this.http.post<Quote>(`${this.b}/requests/${requestId}/quotes/${quoteId}/accept`, body);
  }
  /** Records the client's answer without sending an email (for example an answer given by phone). */
  decideQuote(requestId: string, quoteId: string, decision: 'accepted' | 'rejected', note?: string): Observable<Quote> {
    return this.http.post<Quote>(`${this.b}/requests/${requestId}/quotes/${quoteId}/decision`, { decision, note: note || null });
  }
  reopenQuote(requestId: string, quoteId: string): Observable<Quote> {
    return this.http.post<Quote>(`${this.b}/requests/${requestId}/quotes/${quoteId}/reopen`, {});
  }
  dismissReply(requestId: string, quoteId: string): Observable<Quote> {
    return this.http.post<Quote>(`${this.b}/requests/${requestId}/quotes/${quoteId}/dismiss-reply`, {});
  }

  // ---- queue and delivery
  queue(status?: QueueFilter, q?: string, from?: string, to?: string): Observable<QueueRow[]> {
    return this.http.get<QueueRow[]>(`${this.b}/quotation-queue`, { params: params({ status, q, from, to, pageSize: 100 }) });
  }
  deliveryPreview(requestId: string): Observable<DeliveryPreview> {
    return this.http.get<DeliveryPreview>(`${this.b}/quotation-queue/${requestId}/deliver-to-client/preview`);
  }
  deliver(requestId: string, body: SendEmail): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.b}/quotation-queue/${requestId}/deliver-to-client`, body);
  }
  deliverTest(requestId: string, body: SendEmail): Observable<{ sentTo: string; subject: string }> {
    return this.http.post<{ sentTo: string; subject: string }>(`${this.b}/quotation-queue/${requestId}/deliver-to-client/test-email`, body);
  }
  deliverManually(requestId: string, deliveredTo?: string, note?: string): Observable<MachineList> {
    return this.http.post<MachineList>(`${this.b}/quotation-queue/${requestId}/deliver-to-client/manual`, {
      deliveredTo: deliveredTo || null,
      note: note || null,
    });
  }
  certificatesZip(requestId: string): Observable<Blob> {
    return this.http.get(`${this.b}/quotation-queue/${requestId}/certificates.zip`, { responseType: 'blob' });
  }
  machines(requestId: string): Observable<MachineList> {
    return this.http.get<MachineList>(`${this.b}/quotation-queue/${requestId}/machines`);
  }

  // ---- clients, rules, items, certificates
  clients(includeArchived = false): Observable<Client[]> {
    return this.http.get<Client[]>(`${this.b}/clients`, { params: params({ includeArchived: includeArchived || null }) });
  }
  client(id: string): Observable<Client> {
    return this.http.get<Client>(`${this.b}/clients/${id}`);
  }
  createClient(body: SaveClient): Observable<Client> {
    return this.http.post<Client>(`${this.b}/clients`, body);
  }
  updateClient(id: string, body: SaveClient): Observable<Client> {
    return this.http.put<Client>(`${this.b}/clients/${id}`, body);
  }
  deleteClient(id: string): Observable<void> {
    return this.http.delete<void>(`${this.b}/clients/${id}`);
  }
  clientHistory(id: string): Observable<ClientHistory> {
    return this.http.get<ClientHistory>(`${this.b}/clients/${id}/history`);
  }
  clientEmails(id: string): Observable<MailRule[]> {
    return this.http.get<MailRule[]>(`${this.b}/clients/${id}/emails`);
  }
  addClientEmail(id: string, pattern: string): Observable<MailRule> {
    return this.http.post<MailRule>(`${this.b}/clients/${id}/emails`, { pattern });
  }
  deleteClientEmail(id: string, ruleId: string): Observable<void> {
    return this.http.delete<void>(`${this.b}/clients/${id}/emails/${ruleId}`);
  }
  clientCertificates(id: string): Observable<IssuedCertificate[]> {
    return this.http.get<IssuedCertificate[]>(`${this.b}/clients/${id}/certificates`);
  }
  certificatePdf(clientId: string, fileId: string): Observable<Blob> {
    return this.http.get(`${this.b}/clients/${clientId}/certificates/${fileId}/pdf`, { responseType: 'blob' });
  }
  certificateQr(clientId: string, fileId: string): Observable<Blob> {
    return this.http.get(`${this.b}/clients/${clientId}/certificates/${fileId}/qr.png`, { responseType: 'blob' });
  }
  equipment(clientId?: string, includeArchived = false): Observable<Equipment[]> {
    return this.http.get<Equipment[]>(`${this.b}/equipment`, { params: params({ clientId, includeArchived: includeArchived || null }) });
  }
  createEquipment(body: SaveEquipment): Observable<Equipment> {
    return this.http.post<Equipment>(`${this.b}/equipment`, body);
  }
  updateEquipment(id: string, body: SaveEquipment): Observable<Equipment> {
    return this.http.put<Equipment>(`${this.b}/equipment/${id}`, body);
  }
  deleteEquipment(id: string): Observable<void> {
    return this.http.delete<void>(`${this.b}/equipment/${id}`);
  }
}

