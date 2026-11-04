import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, of, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { CertificateVerification } from './api.models';

const NOT_FOUND: CertificateVerification = {
  valid: false, status: 'not_found', certificateNumber: null, itemName: null, clientName: null,
  examinationDate: null, nextExaminationDate: null, issuedAt: null, issuedBy: null, pdfUrl: null, result: null,
};

/** The public check behind a certificate's QR code. A 404 still carries a result body (`valid: false`). */
@Injectable({ providedIn: 'root' })
export class VerifyService {
  private readonly http = inject(HttpClient);

  verify(code: string): Observable<CertificateVerification> {
    const url = `${environment.apiUrl}/verify/${encodeURIComponent(code.trim())}`;
    return this.http.get<CertificateVerification>(url).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 404) {
          const body = error.error as Partial<CertificateVerification> | null;
          return of({ ...NOT_FOUND, ...(body && typeof body === 'object' ? body : {}), valid: false });
        }
        return throwError(() => error);
      }),
    );
  }
}
