require('./ed.js');
edit('features/requests/requests.html',[
["        [class.amber]=\"(t.id === 'maybe' || t.id === 'details') && (counts()[t.id] ?? 0) > 0\"","        [class.amber]=\"(t.id === 'maybe' || t.id === 'details') && (counts()[t.id] ?? 0) > 0\""],
["              <b>{{ r.clientName || r.emailFromName || r.emailFrom || 'No client yet' }}</b>\n","              <b>{{ r.clientName || r.emailFromName || r.emailFrom || 'No client yet' }}</b>\n              <app-kind-badge [kind]=\"r.kind\" />\n"],
["        } @else if (active().id === 'maybe') {","        } @else if (active().id === 'man_day') {\n          <a class=\"btn go\" routerLink=\"/requests/man-day\">New man day</a>\n        } @else if (active().id === 'maybe') {"],
["            <div class=\"sub\">{{ r.subject || 'No subject' }}</div>","            <div class=\"sub\">{{ r.subject || (r.kind === 'man_day' ? 'Man day · no list of machines' : 'No subject') }}</div>"],
]);
edit('features/requests/requests.ts',[
["import { RouterLink } from '@angular/router';","import { RouterLink } from '@angular/router';"],
["import { formatDate, formatDateTime, plural, relativeTime } from '../../shared/format';","import { formatDate, formatDateTime, plural, relativeTime } from '../../shared/format';\nimport { KindBadge } from '../../shared/kind-badge';"],
["  imports: [RouterLink],\n  changeDetection: ChangeDetectionStrategy.OnPush,\n  templateUrl: './requests.html',","  imports: [RouterLink, KindBadge],\n  changeDetection: ChangeDetectionStrategy.OnPush,\n  templateUrl: './requests.html',"],
]);
edit('features/requests/request-detail.html',[
["      <span class=\"chip\" [class.a]=\"isMaybe()\">{{ isMaybe() ? 'Unknown sender' : stage(r.status) }}</span>","      <span class=\"chip\" [class.a]=\"isMaybe()\">{{ isMaybe() ? 'Unknown sender' : stage(r.status) }}</span>\n      <app-kind-badge [kind]=\"r.kind\" />"],
["      @if (r.quotation) {\n        <a class=\"btn\" [routerLink]","      @if (r.quotation && (!r.kind || r.kind === 'standard')) {\n        <a class=\"btn\" [routerLink]"],
]);
edit('features/quotations/quotations.ts',[
["                @if (r.amountPiastres !== null) { · {{ egp(r.amountPiastres) }} }","                @if (r.amountPiastres !== null && (!r.kind || r.kind === 'standard')) { · {{ egp(r.amountPiastres) }} }"],
["              <b>{{ r.clientName || 'Client' }}</b>\n","              <b>{{ r.clientName || 'Client' }}</b> <app-kind-badge [kind]=\"r.kind\" />\n"],
]);
