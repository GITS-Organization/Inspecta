/* ==========================================================================
   Inspecta website: replicas of the app's screens (sample data only).
   Every label mirrors the app's own English/Arabic strings (lib/l10n).
   All clients, people and figures are fictional.
   ========================================================================== */
(function () {
  'use strict';
  var I = (window.Inspecta = window.Inspecta || { lang: 'en' });

  var L = function (en, ar) { return I.lang === 'ar' ? ar : en; };
  var ic = function (name, cls) { return '<svg class="' + (cls || 'ico') + '" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; };
  var esc = function (s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/"/g, '&quot;'); };

  /* ---------- Shared pieces ---------- */
  function status(light) {
    return '<div class="ui-status' + (light ? ' ui-status--light ui-status--blue' : '') + '"><span>9:41</span><span class="ui-sys"><i class="sig"></i><i class="bat"></i></span></div>';
  }
  function head(o) {
    return '<div class="ui-head">' +
      '<div class="ui-head-top">' +
        '<span class="ui-av">' + o.initials + '</span>' +
        '<div class="ui-greet"><small>' + o.greet + '</small><b>' + o.name + '</b></div>' +
        '<span class="ui-iconbtn">' + ic('bell') + '</span>' +
      '</div>' + (o.below || '') + '</div>';
  }
  function titleHead(title, sub, below, action) {
    return '<div class="ui-head">' +
      '<div class="ui-head-top"><div class="ui-greet"><b style="font-size:1.15em">' + title + '</b>' + (sub ? '<small>' + sub + '</small>' : '') + '</div>' + (action || '') + '</div>' + (below || '') + '</div>';
  }
  function appBar(title, chip) {
    return '<div class="ui-apptitle"><span class="ui-back">' + ic('chevron') + '</span><h4>' + title + '</h4>' + (chip || '') + '</div>';
  }
  function nav(items, on) {
    return '<div class="ui-nav">' + items.map(function (it, i) {
      return '<span class="ui-nav-i' + (i === on ? ' is-on' : '') + '">' + ic(it[0]) + '<span>' + it[1] + '</span></span>';
    }).join('') + '</div>';
  }
  function chip(text, tone, icon) { return '<span class="ui-chip is-' + tone + '">' + (icon ? ic(icon) : '') + text + '</span>'; }
  function shell(inner, toast) { return inner + '<div class="ui-toast" role="status" aria-live="polite"></div>'; }
  function T(msg) { return ' data-toast="' + esc(msg) + '"'; }

  function supervisorNav(on) { return nav([['inbox', L('Requests', 'الطلبات')], ['doc', L('Quotations', 'عروض الأسعار')], ['users', L('Clients', 'العملاء')]], on); }
  function coordinatorNav(on) { return nav([['clip', L('Queue', 'القائمة')], ['cal', L('Schedule', 'الجدول')], ['users', L('Inspectors', 'المفتشون')], ['clock', L('Timesheets', 'سجلات الساعات')]], on); }
  function inspectorNav(on) { return nav([['clip', L('Tasks', 'المهام')], ['badge', L('Certificates', 'الشهادات')], ['clock', L('Timesheet', 'سجل الساعات')]], on); }
  function reviewNav(on) { return nav([['pen', L('Review', 'المراجعة')], ['send', L('Sent', 'المرسلة')]], on); }

  function jstep(now) { // now: 0..5
    var labels = [L('Request', 'الطلب'), L('Quote', 'التسعير'), L('Assign', 'التعيين'), L('Inspect', 'الفحص'), L('Certify', 'الشهادة'), L('Sent', 'الإرسال')];
    return '<div class="ui-jstep">' + labels.map(function (t, i) {
      var cls = i < now ? 'is-done' : (i === now ? 'is-now' : '');
      return '<div class="ui-js ' + cls + '"><i>' + (i < now ? ic('check') : (i + 1)) + '</i><span>' + t + '</span></div>';
    }).join('') + '</div>';
  }
  function seg(opts, on, tone) { // opts: [['Yes','نعم'],…]
    return '<span class="ui-seg" role="group">' + opts.map(function (o, i) {
      var cls = 'ui-seg-b' + (i === on ? ' is-on' : '') + (tone && tone[i] ? ' ' + tone[i] : '');
      return '<button type="button" class="' + cls + '" data-seg data-tone="' + (tone && tone[i] ? tone[i] : '') + '">' + L(o[0], o[1]) + '</button>';
    }).join('') + '</span>';
  }
  var yesNo = [['Yes', 'نعم'], ['No', 'لا']];
  function question(text, on) { return '<div class="ui-q"><p>' + text + '</p>' + seg(yesNo, on) + '</div>'; }

  var signPath = '<svg viewBox="0 0 200 70" preserveAspectRatio="xMidYMid meet" aria-hidden="true"><path d="M22 46c10-26 18-30 20-22 2 9-8 24-2 24 8 0 12-22 22-22 6 0 2 14 8 14 7 0 10-12 18-12 5 0 3 10 9 10 9 0 14-6 22-9" /></svg>';

  /* ---------- Screens ---------- */
  var screens = {};

  // 1. Supervisor: requests inbox
  screens.inbox = function () {
    function card(o) {
      return '<div class="ui-card ui-gap">' +
        '<div class="ui-row ui-between"><span class="ui-h">' + o.client + '</span>' + o.chips + '</div>' +
        '<div class="ui-text">' + o.equip + '</div>' +
        '<div class="ui-row ui-between"><span class="ui-meta">' + o.units + ' · ' + o.when + '</span>' +
        '<button type="button" class="ui-btn is-sm' + (o.outline ? ' is-outline' : '') + '"' + T(o.toast) + '>' + o.cta + '</button></div></div>';
    }
    var newCards =
      card({ client: L('Delta Steel Works', 'دلتا للصلب'), chips: chip(L('New', 'جديد'), 'amber') + chip(L('Waiting 24h+', 'أكثر من 24 ساعة'), 'red', 'clock'),
             equip: L('Overhead crane — 10 t', 'رافعة علوية — 10 طن'), units: L('2 units', 'وحدتان'), when: L('26 h ago', 'منذ 26 ساعة'), cta: L('Review & quote', 'مراجعة وتسعير'), toast: L('Opens the request, ready to quote.', 'يفتح الطلب جاهزًا للتسعير.') }) +
      card({ client: L('Nile Packaging', 'النيل للتغليف'), chips: chip(L('New', 'جديد'), 'amber') + chip(L('New client', 'عميل جديد'), 'blue'),
             equip: L('Air compressor — 10 bar', 'ضاغط هواء — 10 بار'), units: L('1 unit', 'وحدة واحدة'), when: L('2 h ago', 'منذ ساعتين'), cta: L('Review & quote', 'مراجعة وتسعير'), toast: L('Opens the request, ready to quote.', 'يفتح الطلب جاهزًا للتسعير.') }) +
      card({ client: L('Al-Safwa Logistics', 'الصفوة للخدمات اللوجستية'), chips: chip(L('Al-Safwa replied', 'رد الصفوة'), 'blue', 'mail'),
             equip: L('Passenger lift — 8 persons', 'مصعد ركاب — 8 أشخاص'), units: L('3 units', '3 وحدات'), when: L('40 m ago', 'منذ 40 دقيقة'), cta: L('Reply', 'رد'), outline: true, toast: L('Opens the quote conversation.', 'يفتح محادثة عرض السعر.') });
    var allCards =
      card({ client: L('Harbor Cement', 'الميناء للأسمنت'), chips: chip(L('Sent', 'تم الإرسال'), 'blue'), equip: L('Gantry crane — 20 t', 'رافعة جسرية — 20 طن'), units: L('1 unit', 'وحدة واحدة'), when: L('2 d ago', 'منذ يومين'), cta: L('View job', 'عرض المهمة'), outline: true, toast: L('Opens the job.', 'يفتح المهمة.') }) +
      card({ client: L('Sunrise Foods', 'صن رايز للأغذية'), chips: chip(L('Accepted', 'مقبول'), 'green'), equip: L('Air compressor — 8 bar', 'ضاغط هواء — 8 بار'), units: L('2 units', 'وحدتان'), when: L('3 d ago', 'منذ 3 أيام'), cta: L('View job', 'عرض المهمة'), outline: true, toast: L('Opens the job.', 'يفتح المهمة.') });
    return shell(status(true) +
      head({ initials: L('MA', 'م ع'), greet: L('Good morning', 'صباح الخير'), name: L('Mona Adel · Supervisor', 'منى عادل · المشرف'),
        below: '<div class="ui-search">' + ic('search') + '<span>' + L('Search client, equipment or ID', 'ابحث بالعميل أو المعدة أو الرقم') + '</span></div>' }) +
      '<div class="ui-body">' +
        '<div class="ui-tabs" data-tabs="inbox"><button type="button" class="ui-tab is-on" data-tab="new">' + L('New · 3', 'جديد · 3') + '</button><button type="button" class="ui-tab" data-tab="all">' + L('All · 5', 'الكل · 5') + '</button></div>' +
        '<div class="ui-gap" data-pane-of="inbox" data-pane="new"><div class="ui-row ui-between"><span class="ui-h">' + L('Needs your reply', 'بحاجة إلى ردك') + '</span><span class="ui-sub">' + L('Newest first', 'الأحدث أولاً') + '</span></div>' + newCards + '</div>' +
        '<div class="ui-gap" data-pane-of="inbox" data-pane="all" hidden><div class="ui-row ui-between"><span class="ui-h">' + L('All requests', 'جميع الطلبات') + '</span><span class="ui-sub">' + L('Newest first', 'الأحدث أولاً') + '</span></div>' + newCards + allCards + '</div>' +
      '</div>' + supervisorNav(0));
  };

  // 2. Supervisor: request detail with the six-step tracker
  screens.request = function () {
    function eq(name, cap, qty) {
      return '<div class="ui-card ui-gap" style="padding:.75em .9em"><div class="ui-row ui-between"><span class="ui-h">' + name + '</span>' + chip(L('Qty ', 'الكمية ') + qty, 'soft') + '</div><div class="ui-meta">' + L('Capacity', 'السعة') + ': ' + cap + '</div></div>';
    }
    return shell(status(false) + appBar(L('Request', 'الطلب'), chip(L('Sent', 'تم الإرسال'), 'blue')) +
      '<div class="ui-body">' +
        '<div class="ui-card">' + jstep(1) + '</div>' +
        '<div class="ui-card ui-gap">' +
          '<div class="ui-row ui-between"><span class="ui-h">' + L('Delta Steel Works', 'دلتا للصلب') + '</span><span class="ui-meta">R-2041</span></div>' +
          '<div class="ui-kv">' +
            '<div><small>' + L('Location', 'الموقع') + '</small><b>' + L('Sadat City, Zone 2', 'مدينة السادات، المنطقة 2') + '</b></div>' +
            '<div><small>' + L('Preferred date', 'التاريخ المفضل') + '</small><b>' + L('22 Oct', '22 أكتوبر') + '</b></div>' +
            '<div style="grid-column:1/-1"><small>' + L('Access notes', 'ملاحظات الوصول') + '</small><b>' + L('Gate 3. Safety induction on arrival.', 'البوابة 3. تعريف بالسلامة عند الوصول.') + '</b></div>' +
          '</div></div>' +
        '<span class="ui-h">' + L('Equipment', 'المعدات') + '</span>' +
        eq(L('Overhead crane', 'رافعة علوية'), L('10 t', '10 طن'), 2) + eq(L('Hoist', 'رافعة سلسلة'), L('5 t', '5 طن'), 1) +
        '<div class="ui-card ui-gap"><div class="ui-row"><span class="ui-av ui-av--sm">' + ic('mail', 'ico" style="width:1.3em;height:1.3em') + '</span><span class="ui-meta ui-grow">purchasing@deltasteel.example</span></div>' +
          '<p class="ui-text">' + L('“Please quote the annual examination of our two overhead cranes and the 5 t hoist at the Sadat City site…”', '«نرجو تسعير الفحص السنوي لرافعتينا العلويتين ورافعة 5 طن في موقع مدينة السادات…»') + '</p>' +
          '<button type="button" class="ui-btn is-sm is-outline"' + T(L('Shows the original client email.', 'يعرض بريد العميل الأصلي.')) + '>' + L('Read full email', 'قراءة البريد الكامل') + '</button></div>' +
      '</div>' + supervisorNav(0));
  };

  // 3. Supervisor: quote history / negotiation
  screens.quote = function () {
    return shell(status(false) + appBar(L('Quote history', 'سجل عرض السعر'), chip(L('Your turn', 'دورك'), 'amber')) +
      '<div class="ui-body">' +
        '<div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + L('Delta Steel Works', 'دلتا للصلب') + '</span><span class="ui-meta">Q-1187</span></div>' +
          '<div class="ui-text">' + L('Overhead crane — 10 t × 2', 'رافعة علوية — 10 طن × 2') + '</div></div>' +
        '<span class="ui-h">' + L('Negotiation history', 'سجل التفاوض') + '</span>' +
        '<div class="ui-card"><div class="ui-tl">' +
          '<div class="ui-tl-i"><span class="ui-tl-dot"></span><div class="ui-tl-b"><b>' + L('Request received', 'تم استلام الطلب') + '</b><span>' + L('15 Oct · by email', '15 أكتوبر · عبر البريد الإلكتروني') + '</span></div></div>' +
          '<div class="ui-tl-i"><span class="ui-tl-dot"></span><div class="ui-tl-b"><b>' + L('Quote v1 sent', 'تم إرسال عرض الإصدار 1') + '</b><span>' + L('16 Oct · by you · valid 14 days · EGP 4,800 per unit', '16 أكتوبر · بواسطتك · صالح 14 يومًا · 4,800 ج.م للوحدة') + '</span></div></div>' +
          '<div class="ui-tl-i"><span class="ui-tl-dot is-amber"></span><div class="ui-tl-b"><b>' + L('Client countered EGP 4,200', 'عرض العميل المضاد 4,200 ج.م') + '</b><span>' + chip(L('Counter-offer', 'عرض مضاد'), 'blue') + '</span><span style="margin-block-start:.3em">' + L('Client asks: can the load test be included?', 'طلب العميل: هل يمكن تضمين اختبار الحمل؟') + '</span></div></div>' +
          '<div class="ui-tl-i"><span class="ui-tl-dot is-now"></span><div class="ui-tl-b"><b>' + L('Your response', 'ردّك') + '</b><span>' + L('Choose an option below', 'اختر خيارًا أدناه') + '</span></div></div>' +
        '</div></div>' +
        '<div class="ui-gap">' +
          '<button type="button" class="ui-btn is-block"' + T(L('Sent as quote v2. The history keeps v1.', 'يُرسل كإصدار 2، ويحتفظ السجل بالإصدار 1.')) + '>' + L('Send new price (v2)', 'إرسال سعر جديد (إصدار 2)') + '</button>' +
          '<div class="ui-btns"><button type="button" class="ui-btn is-ok is-sm"' + T(L('Accepted. The price is locked and the job goes to the coordinator.', 'تم القبول. السعر مقفل وتنتقل المهمة إلى المنسق.')) + '>' + L("Accept client's price", 'قبول سعر العميل') + '</button>' +
          '<button type="button" class="ui-btn is-outline is-sm"' + T(L('Log the reply in the history.', 'يسجّل الرد في السجل.')) + '>' + L('Log other reply', 'تسجيل رد آخر') + '</button></div>' +
          '<button type="button" class="ui-btn is-danger is-block is-sm"' + T(L('The quote is marked declined.', 'يُحدَّد العرض كمرفوض.')) + '>' + L('Mark declined', 'تحديد كمرفوض') + '</button>' +
          '<p class="ui-meta" style="text-align:center">' + L('Accepting sends the job to the coordinator and locks the price.', 'القبول يرسل المهمة إلى المنسق ويقفل السعر.') + '</p>' +
        '</div>' +
      '</div>' + supervisorNav(1));
  };

  // 4. Supervisor: clients
  screens.clients = function () {
    function client(ini, name, loc, chips, tone) {
      return '<div class="ui-card ui-gap"><div class="ui-row"><span class="ui-av ui-av--tint" style="width:2.6em;height:2.6em;font-size:.85em">' + ini + '</span><div class="ui-grow"><div class="ui-h">' + name + '</div><div class="ui-meta">' + loc + '</div></div>' + ic('chevron', 'ico flip" style="width:1.1em;height:1.1em;color:#64748b') + '</div><div class="ui-row" style="flex-wrap:wrap;gap:.4em">' + chips + '</div></div>';
    }
    return shell(status(true) +
      titleHead(L('Clients', 'العملاء'), L('Sorted by latest activity', 'مرتبة حسب آخر نشاط'),
        '<div class="ui-search">' + ic('search') + '<span>' + L('Search company, contact or email', 'ابحث بالشركة أو جهة الاتصال أو البريد الإلكتروني') + '</span></div>',
        '<span class="ui-iconbtn">' + ic('plus') + '</span>') +
      '<div class="ui-body">' +
        '<div class="ui-tabs" data-tabs="clients"><button type="button" class="ui-tab is-on" data-tab="all">' + L('All · 6', 'الكل · 6') + '</button><button type="button" class="ui-tab" data-tab="open">' + L('Open jobs · 3', 'مهام مفتوحة · 3') + '</button><button type="button" class="ui-tab" data-tab="due">' + L('Due soon · 2', 'قريبة الاستحقاق · 2') + '</button></div>' +
        '<div class="ui-card ui-gap" style="background:#fffbeb"><div class="ui-row ui-between"><span class="ui-h" style="font-size:.82em">' + L('New sender needs a client', 'مرسل جديد يحتاج إلى عميل') + '</span><button type="button" class="ui-btn is-sm"' + T(L('Match the sender to an existing or new client.', 'يربط المرسل بعميل حالي أو جديد.')) + '>' + L('Match', 'ربط') + '</button></div><div class="ui-meta">procurement@harbor-cement.example</div></div>' +
        '<div class="ui-gap" data-pane-of="clients" data-pane="all">' +
          client(L('DS', 'دص'), L('Delta Steel Works', 'دلتا للصلب'), L('Sadat City', 'مدينة السادات'), chip(L('3 equipment', '3 معدات'), 'soft') + chip(L('1 open job', 'مهمة مفتوحة'), 'blue') + chip(L('2 due in 30 days', '2 تستحق خلال 30 يومًا'), 'amber')) +
          client(L('NP', 'نت'), L('Nile Packaging', 'النيل للتغليف'), L('10th of Ramadan', 'العاشر من رمضان'), chip(L('1 equipment', 'معدة'), 'soft') + chip(L('1 open job', 'مهمة مفتوحة'), 'blue')) +
          client(L('AS', 'صل'), L('Al-Safwa Logistics', 'الصفوة للخدمات اللوجستية'), L('Alexandria', 'الإسكندرية'), chip(L('5 equipment', '5 معدات'), 'soft') + chip(L('1 due in 30 days', '1 تستحق خلال 30 يومًا'), 'amber')) +
          client(L('SF', 'صف'), L('Sunrise Foods', 'صن رايز للأغذية'), L('Cairo', 'القاهرة'), chip(L('2 equipment', 'معدتان'), 'soft')) +
        '</div>' +
        '<div class="ui-gap" data-pane-of="clients" data-pane="open" hidden>' +
          client(L('DS', 'دص'), L('Delta Steel Works', 'دلتا للصلب'), L('Sadat City', 'مدينة السادات'), chip(L('3 equipment', '3 معدات'), 'soft') + chip(L('1 open job', 'مهمة مفتوحة'), 'blue') + chip(L('2 due in 30 days', '2 تستحق خلال 30 يومًا'), 'amber')) +
          client(L('NP', 'نت'), L('Nile Packaging', 'النيل للتغليف'), L('10th of Ramadan', 'العاشر من رمضان'), chip(L('1 equipment', 'معدة'), 'soft') + chip(L('1 open job', 'مهمة مفتوحة'), 'blue')) +
        '</div>' +
        '<div class="ui-gap" data-pane-of="clients" data-pane="due" hidden>' +
          client(L('DS', 'دص'), L('Delta Steel Works', 'دلتا للصلب'), L('Sadat City', 'مدينة السادات'), chip(L('3 equipment', '3 معدات'), 'soft') + chip(L('1 open job', 'مهمة مفتوحة'), 'blue') + chip(L('2 due in 30 days', '2 تستحق خلال 30 يومًا'), 'amber')) +
          client(L('AS', 'صل'), L('Al-Safwa Logistics', 'الصفوة للخدمات اللوجستية'), L('Alexandria', 'الإسكندرية'), chip(L('5 equipment', '5 معدات'), 'soft') + chip(L('1 due in 30 days', '1 تستحق خلال 30 يومًا'), 'amber')) +
        '</div>' +
      '</div>' + supervisorNav(2));
  };

  // 5. Coordinator: assign an inspector
  screens.assign = function () {
    function insp(ini, name, quals, sub, o) {
      o = o || {};
      return '<button type="button" class="ui-card ui-row" style="width:100%;text-align:start;border:0;font:inherit;cursor:' + (o.disabled ? 'default' : 'pointer') + ';' + (o.disabled ? 'opacity:.55;' : '') + '"' +
        (o.disabled ? ' aria-disabled="true"' : ' data-pick="' + esc(name) + '"') + '>' +
        '<span class="ui-radio' + (o.on ? ' is-on' : '') + '"></span><span class="ui-av ui-av--tint" style="width:2.4em;height:2.4em;font-size:.8em">' + ini + '</span>' +
        '<span class="ui-grow ui-gap" style="gap:.2em"><span class="ui-row" style="gap:.4em;flex-wrap:wrap"><span class="ui-h" style="font-size:.85em">' + name + '</span>' + (o.best ? chip(L('Best match', 'الأنسب'), 'green') : '') + '</span>' +
        '<span class="ui-row" style="gap:.3em;flex-wrap:wrap">' + quals + '</span><span class="ui-meta">' + sub + '</span></span></button>';
    }
    var q = function (a) { return chip(a, 'soft'); };
    return shell(status(false) + appBar(L('Assign inspector', 'تعيين مفتش')) +
      '<div class="ui-body" data-pickgroup>' +
        '<div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + L('Delta Steel Works', 'دلتا للصلب') + '</span>' + chip(L('Due tomorrow', 'مستحق غدًا'), 'amber') + '</div><div class="ui-text">' + L('Overhead crane — 10 t × 2', 'رافعة علوية — 10 طن × 2') + '</div></div>' +
        '<span class="ui-h">' + L('1. Pick a time', '1. اختر الموعد') + '</span>' +
        '<div class="ui-grid2"><label class="ui-field"><span>' + L('Date', 'التاريخ') + '</span><span class="ui-input">' + L('Tue, 20 Oct', 'الثلاثاء 20 أكتوبر') + ic('cal') + '</span></label><label class="ui-field"><span>' + L('Start', 'وقت البدء') + '</span><span class="ui-input">09:30' + ic('clock') + '</span></label></div>' +
        '<span class="ui-h">' + L('2. Choose inspector', '2. اختر المفتش') + '</span>' +
        insp(L('OH', 'عح'), L('Omar Hassan', 'عمر حسن'), q(L('Cranes', 'رافعات')) + q(L('Lifts', 'مصاعد')), L('Free today', 'متاح اليوم'), { on: true, best: true }) +
        insp(L('KF', 'كف'), L('Karim Fathy', 'كريم فتحي'), q(L('Pressure', 'ضغط')), L('1 of 3 slots', '1 من 3 مهام')) +
        insp(L('LS', 'لس'), L('Laila Samir', 'ليلى سمير'), q(L('Cranes', 'رافعات')), L('On leave until 23 Oct', 'في إجازة حتى 23 أكتوبر'), { disabled: true }) +
        '<span class="ui-h">' + L('3. Notes (optional)', '3. ملاحظات (اختياري)') + '</span>' +
        '<span class="ui-input is-muted">' + L('Access or safety notes', 'ملاحظات الوصول أو السلامة') + '</span>' +
        '<button type="button" class="ui-btn is-block" data-pick-target data-pattern="' + esc(L('Assign & notify {name}', 'تعيين وإشعار {name}')) + '"' + T(L('Inspector assigned.', 'تم تعيين المفتش.')) + '>' + L('Assign & notify Omar Hassan', 'تعيين وإشعار عمر حسن') + '</button>' +
      '</div>');
  };

  // 6. Coordinator: schedule
  screens.schedule = function () {
    function row(name, track, cls, note) {
      return '<div class="ui-trow"><b>' + name + '</b><div class="ui-track ' + (cls || '') + '"' + (note ? ' data-note="' + esc(note) + '"' : '') + '>' + track + '</div></div>';
    }
    function blk(l, w, text, cls) { return '<span class="ui-block ' + (cls || '') + '" style="inset-inline-start:' + l + '%;width:' + w + '%">' + text + '</span>'; }
    function pane(key, rows, hidden) {
      return '<div class="ui-gap" data-pane-of="sched" data-pane="' + key + '"' + (hidden ? ' hidden' : '') + '><div class="ui-axis"><span></span><span>08</span><span>10</span><span>12</span><span>14</span><span>16</span></div>' + rows + '</div>';
    }
    var d19 =
      row(L('Omar Hassan', 'عمر حسن'), blk(2, 38, L('Nile Pack.', 'النيل'), 'is-done') + blk(48, 44, L('Harbor', 'الميناء'), 'is-alt')) +
      row(L('Karim Fathy', 'كريم فتحي'), blk(22, 56, L('Sunrise Foods', 'صن رايز'))) +
      row(L('Laila Samir', 'ليلى سمير'), '', 'is-leave', L('On leave until 23 Oct', 'في إجازة حتى 23 أكتوبر')) +
      row(L('Mariam Said', 'مريم سعيد'), '', 'is-free', L('Free all day', 'متفرغ طوال اليوم'));
    var d20 =
      row(L('Omar Hassan', 'عمر حسن'), blk(12, 62, L('Delta Steel Works', 'دلتا للصلب'), 'is-amber')) +
      row(L('Karim Fathy', 'كريم فتحي'), '', 'is-free', L('Free all day', 'متفرغ طوال اليوم')) +
      row(L('Laila Samir', 'ليلى سمير'), '', 'is-leave', L('On leave until 23 Oct', 'في إجازة حتى 23 أكتوبر')) +
      row(L('Mariam Said', 'مريم سعيد'), blk(46, 40, L('Al-Safwa', 'الصفوة')));
    var d21 =
      row(L('Omar Hassan', 'عمر حسن'), '', 'is-free', L('Free all day', 'متفرغ طوال اليوم')) +
      row(L('Karim Fathy', 'كريم فتحي'), blk(4, 30, L('Sunrise', 'صن رايز')) + blk(40, 50, L('Nile Pack.', 'النيل'), 'is-alt')) +
      row(L('Laila Samir', 'ليلى سمير'), '', 'is-leave', L('On leave until 23 Oct', 'في إجازة حتى 23 أكتوبر')) +
      row(L('Mariam Said', 'مريم سعيد'), blk(22, 56, L('Harbor', 'الميناء'), 'is-alt'));
    function day(k, dn, num, on, has) { return '<button type="button" class="ui-day' + (on ? ' is-on' : '') + (has ? ' has' : '') + '" data-tab="' + k + '"><small>' + dn + '</small><b>' + num + '</b><i></i></button>'; }
    return shell(status(true) +
      titleHead(L('Schedule', 'الجدول'), L('Tasks per inspector, by day', 'مهام كل مفتش حسب اليوم'),
        '<div class="ui-days" data-tabs="sched" style="grid-template-columns:repeat(3,minmax(0,1fr));margin-block-start:1em">' + day('d19', L('Mon', 'الاثنين'), 19, true, true) + day('d20', L('Tue', 'الثلاثاء'), 20, false, true) + day('d21', L('Wed', 'الأربعاء'), 21, false, true) + '</div>') +
      '<div class="ui-body">' + pane('d19', d19) + pane('d20', d20, true) + pane('d21', d21, true) +
        '<p class="ui-meta">' + L('Pick a day to see each inspector’s load before you assign.', 'اختر يومًا لترى حمل كل مفتش قبل التعيين.') + '</p></div>' + coordinatorNav(1));
  };

  // 7. Inspector: my tasks
  screens.tasks = function () {
    function dayb(dn, num, on, has) { return '<button type="button" class="ui-day' + (on ? ' is-on' : '') + (has ? ' has' : '') + '" data-tab="t' + num + '"><small>' + dn + '</small><b>' + num + '</b><i></i></button>'; }
    var days = [[L('Mon', 'إثن'), 19, 1, 1], [L('Tue', 'ثلا'), 20, 0, 1], [L('Wed', 'أرب'), 21, 0, 0], [L('Thu', 'خمي'), 22, 0, 1], [L('Fri', 'جمع'), 23, 0, 0], [L('Sat', 'سبت'), 24, 0, 0], [L('Sun', 'أحد'), 25, 0, 0]];
    function task(client, equip, time, st, cta, filled) {
      return '<div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + client + '</span>' + st + '</div><div class="ui-text">' + equip + '</div><div class="ui-row ui-between"><span class="ui-meta">' + ic('clock', 'ico" style="width:1.1em;height:1.1em;vertical-align:-.2em;margin-inline-end:.3em') + time + '</span><button type="button" class="ui-btn is-sm' + (filled ? '' : ' is-outline') + '"' + T(L('Opens the examination report.', 'يفتح تقرير الفحص.')) + '>' + cta + '</button></div></div>';
    }
    return shell(status(true) +
      head({ initials: L('OH', 'عح'), greet: L('Good morning', 'صباح الخير'), name: L('Omar Hassan · Inspector', 'عمر حسن · مفتش'),
        below: '<div class="ui-days" data-tabs="tasks" style="margin-block-start:1.1em">' + days.map(function (d) { return dayb(d[0], d[1], d[2], d[3]); }).join('') + '</div>' }) +
      '<div class="ui-body">' +
        '<div data-pane-of="tasks" data-pane="t19" class="ui-gap">' +
          '<span class="ui-h">' + L('3 tasks today', '3 مهام اليوم') + '</span>' +
          '<div class="ui-card ui-gap" style="background:#eff4ff;box-shadow:0 0 0 1.5px #bfdbfe"><div class="ui-row ui-between"><span class="ui-h" style="font-size:.82em">' + L('New task', 'مهمة جديدة') + '</span><span class="ui-meta">' + L('from your coordinator', 'من المنسق الخاص بك') + '</span></div><div class="ui-text"><b class="ui-strong">' + L('Al-Safwa Logistics', 'الصفوة للخدمات اللوجستية') + '</b><br>' + L('Passenger lift × 3 · 14:00', 'مصعد ركاب × 3 · 14:00') + '</div>' +
            '<div class="ui-btns"><button type="button" class="ui-btn is-sm"' + T(L('Task accepted.', 'تم قبول المهمة.')) + '>' + L('Accept', 'قبول') + '</button><button type="button" class="ui-btn is-sm is-outline"' + T(L('Task declined. The coordinator is told.', 'تم رفض المهمة. يُبلَّغ المنسق.')) + '>' + L('Decline', 'رفض') + '</button></div></div>' +
          task(L('Nile Packaging', 'النيل للتغليف'), L('Air compressor — 10 bar', 'ضاغط هواء — 10 بار'), '09:00', chip(L('Task accepted', 'تم قبول المهمة'), 'green'), L('Continue certificate', 'متابعة الشهادة'), true) +
          task(L('Harbor Cement', 'الميناء للأسمنت'), L('Gantry crane — 20 t', 'رافعة جسرية — 20 طن'), '11:30', chip(L('In progress', 'قيد التنفيذ'), 'blue'), L('Continue certificate', 'متابعة الشهادة'), false) +
        '</div>' +
        '<div data-pane-of="tasks" data-pane="t20" class="ui-gap" hidden><span class="ui-h">' + L('1 task on Tue', 'مهمة واحدة يوم الثلاثاء') + '</span>' +
          task(L('Delta Steel Works', 'دلتا للصلب'), L('Overhead crane — 10 t × 2', 'رافعة علوية — 10 طن × 2'), '09:30', chip(L('Task accepted', 'تم قبول المهمة'), 'green'), L('Open', 'فتح'), false) + '</div>' +
        '<div data-pane-of="tasks" data-pane="t21" class="ui-gap" hidden><div class="ui-card ui-text" style="text-align:center;padding-block:2em">' + L('No tasks on this day.', 'لا توجد مهام في هذا اليوم.') + '</div></div>' +
        '<div data-pane-of="tasks" data-pane="t22" class="ui-gap" hidden><span class="ui-h">' + L('1 task on Thu', 'مهمة واحدة يوم الخميس') + '</span>' +
          task(L('Sunrise Foods', 'صن رايز للأغذية'), L('Air compressor — 8 bar × 2', 'ضاغط هواء — 8 بار × 2'), '10:00', chip(L('Needs response', 'بانتظار الرد'), 'amber'), L('Open', 'فتح'), false) + '</div>' +
        '<div data-pane-of="tasks" data-pane="t23" class="ui-gap" hidden><div class="ui-card ui-text" style="text-align:center;padding-block:2em">' + L('No tasks on this day.', 'لا توجد مهام في هذا اليوم.') + '</div></div>' +
        '<div data-pane-of="tasks" data-pane="t24" class="ui-gap" hidden><div class="ui-card ui-text" style="text-align:center;padding-block:2em">' + L('No tasks on this day.', 'لا توجد مهام في هذا اليوم.') + '</div></div>' +
        '<div data-pane-of="tasks" data-pane="t25" class="ui-gap" hidden><div class="ui-card ui-text" style="text-align:center;padding-block:2em">' + L('No tasks on this day.', 'لا توجد مهام في هذا اليوم.') + '</div></div>' +
      '</div>' + inspectorNav(0));
  };

  // 8. Inspector: examination report (5-step wizard)
  screens.report = function () {
    var sc = '<span class="ui-saved" data-save>' + ic('check') + '<span>' + L('Saved', 'تم الحفظ') + '</span></span>';
    var stepsBar = '<div class="ui-steps" data-wiz-bar><i class="is-done"></i><i class="is-done"></i><i class="is-done"></i><i></i><i></i></div>';
    var s3 = '<div class="ui-gap" data-wiz-pane="3">' +
      '<span class="ui-h">' + L('Examination questions', 'أسئلة الفحص') + '</span>' +
      '<div class="ui-card">' +
        question(L('Is this the first examination after installation or assembly at a new site or location?', 'هل هذا هو الفحص الأول بعد التركيب أو التجميع في موقع أو مكان جديد؟'), 1) +
        question(L('Has the equipment been installed correctly?', 'هل تم تركيب المعدة بشكل صحيح؟'), 0) +
      '</div><div class="ui-card"><p class="ui-meta" style="margin-block-end:.3em">' + L('Was the examination carried out:', 'هل أُجري الفحص:') + '</p>' +
        question(L('Within an interval of 6 months?', 'خلال فترة 6 أشهر؟'), 0) +
        question(L('Within an interval of 12 months?', 'خلال فترة 12 شهرًا؟'), 1) +
        question(L('In accordance with an examination scheme?', 'وفقًا لبرنامج فحص؟'), 0) +
        question(L('After the occurrence of exceptional circumstances?', 'بعد حدوث ظروف استثنائية؟'), 1) +
      '</div></div>';
    var s4 = '<div class="ui-gap" data-wiz-pane="4" hidden>' +
      '<span class="ui-h">' + L('Defects & remedial action', 'العيوب والإجراءات التصحيحية') + '</span>' +
      '<div class="ui-card ui-gap"><p class="ui-text">' + L('Any part found to have a defect which is or could become a danger to persons, and a description of the defect', 'أي جزء وُجد به عيب يشكّل أو قد يشكّل خطرًا على الأشخاص، ووصف العيب') + '</p>' +
        '<span class="ui-input" style="align-items:flex-start;min-height:4.4em;line-height:1.4">' + L('Broken wires found in the hoist rope near the hook block.', 'أسلاك مقطوعة في حبل الرفع بالقرب من كتلة الخطاف.') + '</span></div>' +
      '<div class="ui-card">' +
        question(L('Is this an existing or imminent danger to persons? (a reportable defect)', 'هل هذا خطر قائم أو وشيك على الأشخاص؟ (عيب يجب الإبلاغ عنه)'), 1) +
        question(L('Not yet, but could become a danger to persons?', 'ليس بعد، لكنه قد يصبح خطرًا على الأشخاص؟'), 0) +
        '<div class="ui-row ui-between" style="padding-block:.5em"><span class="ui-meta">' + L('If yes, the date by when', 'إن كانت الإجابة نعم، التاريخ المحدد') + '</span><span class="ui-chip is-amber">' + L('2 Nov 2026', '2 نوفمبر 2026') + '</span></div>' +
      '</div>' +
      '<div class="ui-card ui-gap"><p class="ui-meta">' + L('Repair, renewal or alteration required', 'الإصلاح أو الاستبدال أو التعديل المطلوب') + '</p><span class="ui-input" style="min-height:2.6em">' + L('Replace the hoist rope.', 'استبدال حبل الرفع.') + '</span></div></div>';
    var s5 = '<div class="ui-gap" data-wiz-pane="5" hidden>' +
      '<span class="ui-h">' + L('Conclusion', 'الخلاصة') + '</span>' +
      '<div class="ui-gap" data-pickgroup>' +
        concl(L('Safe to operate', 'آمن للتشغيل'), 'ok', false) +
        concl(L('Safe with conditions (fix in 14 days)', 'آمن بشروط (الإصلاح خلال 14 يومًا)'), 'amber', true) +
        concl(L('Not safe — out of service', 'غير آمن — خارج الخدمة'), 'red', false) +
      '</div><p class="ui-meta">' + L('Once submitted, you won’t be able to make further changes.', 'بعد الإرسال، لن تتمكن من إجراء أي تعديلات أخرى.') + '</p></div>';
    function concl(text, tone, on) {
      return '<button type="button" class="ui-card ui-row" style="width:100%;text-align:start;border:0;font:inherit;cursor:pointer" data-pick="' + esc(text) + '"><span class="ui-radio' + (on ? ' is-on' : '') + '"></span><span class="ui-chip is-' + tone + '" style="font-size:.78em;white-space:normal">' + text + '</span></button>';
    }
    return shell(status(false) +
      '<div class="ui-apptitle" style="flex-direction:column;align-items:stretch;gap:.55em"><div class="ui-row"><span class="ui-back">' + ic('chevron') + '</span><h4 style="white-space:normal;line-height:1.25">' + L('Report of thorough examination', 'تقرير الفحص الشامل') + '</h4></div>' +
        '<div class="ui-row ui-between"><span class="ui-meta" data-wiz-label>' + L('Step 3 of 5 · Examination questions', 'الخطوة 3 من 5 · أسئلة الفحص') + '</span>' + sc + '</div>' + stepsBar + '</div>' +
      '<div class="ui-body" data-wiz>' + s3 + s4 + s5 + '</div>' +
      '<div class="ui-nav" style="gap:.5em;padding-block:.7em 1em;grid-auto-flow:column"><button type="button" class="ui-btn is-outline is-sm" data-wiz-back>' + L('Back', 'رجوع') + '</button><button type="button" class="ui-btn is-sm" data-wiz-next data-submit-toast="' + esc(L('Certificate submitted.', 'تم إرسال الشهادة.')) + '">' + L('Next', 'التالي') + '</button></div>');
  };

  // 9. Inspector: timesheet
  screens.timesheet = function () {
    function entry(o) {
      return '<div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + o.client + '</span>' + o.chip + '</div><div class="ui-text">' + o.equip + '</div>' +
        '<div class="ui-row ui-between"><div><div class="ui-meta">' + L('Time spent', 'الوقت المنقضي') + '</div><b class="ui-strong">' + o.time + '</b></div><div style="text-align:end"><div class="ui-meta">' + L('Price', 'السعر') + '</div><b class="ui-strong">' + o.price + '</b></div></div>' +
        (o.note ? '<div class="ui-note is-amber">' + o.note + '</div>' : '') +
        (o.details ? '<button type="button" class="ui-btn is-sm is-soft" data-toggle="' + o.key + '">' + L('Details', 'التفاصيل') + '</button><div class="ui-gap" data-reveal="' + o.key + '" hidden style="gap:.35em">' + o.details + '</div>' : '') + '</div>';
    }
    function line(a, b, strong) { return '<div class="ui-row ui-between"><span class="ui-meta">' + a + '</span><span class="' + (strong ? 'ui-strong' : 'ui-meta') + '" style="font-size:.76em">' + b + '</span></div>'; }
    return shell(status(true) +
      titleHead(L('Timesheet', 'سجل الساعات'), L('October 2026', 'أكتوبر 2026'),
        '<div class="ui-tiles"><div class="ui-tile"><b>' + L('EGP 5,150', '5,150 ج.م') + '</b><small>' + L('Total', 'الإجمالي') + '</small></div><div class="ui-tile"><b>' + L('13h 20m', '13س 20د') + '</b><small>' + L('Time logged', 'الوقت المسجّل') + '</small></div><div class="ui-tile is-warn"><b>5/6</b><small>' + L('tasks logged', 'مهام مسجّلة') + '</small></div></div>') +
      '<div class="ui-body">' +
        '<div class="ui-card ui-row ui-between"><span class="ui-text">' + L('Hourly rate', 'أجر الساعة') + '</span><b class="ui-strong">' + L('EGP 400 / hour', '400 ج.م / ساعة') + '</b></div>' +
        entry({ key: 'e1', client: L('Delta Steel Works', 'دلتا للصلب'), equip: L('Overhead crane — 10 t', 'رافعة علوية — 10 طن'), chip: chip(L('Approved', 'معتمد'), 'green', 'lock'), time: L('2h 30m', '2س 30د'), price: L('EGP 1,150', '1,150 ج.م'),
          details: line(L('Inspection & load testing', 'الفحص واختبار الحمل'), L('1h 30m', '1س 30د')) + line(L('Report preparation', 'إعداد التقرير'), L('45m', '45د')) + line(L('Waiting', 'انتظار'), L('15m', '15د')) + line(L('Unpaid break', 'استراحة غير مدفوعة'), L('−30m', '−30د')) + '<div class="ui-divider"></div>' + line(L('Net working time', 'صافي وقت العمل'), L('2h 30m × EGP 400 per hour', '2س 30د × 400 ج.م للساعة'), true) + line(L('Internal transportation', 'النقل الداخلي'), L('EGP 150', '150 ج.م')) }) +
        entry({ key: 'e2', client: L('Nile Packaging', 'النيل للتغليف'), equip: L('Air compressor — 10 bar', 'ضاغط هواء — 10 بار'), chip: chip(L('Awaiting approval', 'بانتظار الاعتماد'), 'amber'), time: L('1h 45m', '1س 45د'), price: L('EGP 700', '700 ج.م') }) +
        entry({ key: 'e3', client: L('Harbor Cement', 'الميناء للأسمنت'), equip: L('Gantry crane — 20 t', 'رافعة جسرية — 20 طن'), chip: chip(L('Returned', 'معاد'), 'red'), time: L('5h 00m', '5س 00د'), price: L('EGP 2,000', '2,000 ج.م'), note: L('Returned by the coordinator: please confirm the waiting time.', 'أعادها المنسق: يرجى تأكيد وقت الانتظار.') }) +
        '<button type="button" class="ui-btn is-block"' + T(L('Opens the time and costs sheet.', 'يفتح نموذج الوقت والتكاليف.')) + '>' + ic('plus') + L('Log time & costs', 'تسجيل الوقت والتكاليف') + '</button>' +
      '</div>' + inspectorNav(2));
  };

  // 10. Technical manager: review and sign
  screens.review = function () {
    return shell(status(false) + appBar(L('Review certificate', 'مراجعة الشهادة'), chip(L('Awaiting you', 'بانتظارك'), 'amber')) +
      '<div class="ui-body" data-review>' +
        '<div class="ui-card ui-gap"><span class="ui-h">' + L('Summary', 'الملخص') + '</span>' +
          '<div class="ui-kv"><div style="grid-column:1/-1"><small>' + L('Client', 'العميل') + '</small><b>' + L('Delta Steel Works', 'دلتا للصلب') + '</b></div>' +
          '<div style="grid-column:1/-1"><small>' + L('Inspected item', 'المعدة المفحوصة') + '</small><b>' + L('Overhead crane — 10 t', 'رافعة علوية — 10 طن') + '</b></div><div style="grid-column:1/-1"><small>' + L('Inspector', 'المفتش') + '</small><b>' + L('Omar Hassan', 'عمر حسن') + '</b></div>' +
          '<div><small>' + L('Inspected', 'تاريخ الفحص') + '</small><b>' + L('19 Oct 2026', '19 أكتوبر 2026') + '</b></div><div><small>' + L('Next due', 'الاستحقاق التالي') + '</small><b>' + L('19 Apr 2027', '19 أبريل 2027') + '</b></div></div>' +
          '<div class="ui-verdict is-amber">' + ic('alert') + '<span>' + L('Safe with conditions', 'آمنة بشروط') + '</span></div>' +
          '<p class="ui-note is-amber">' + L('Repair required: replace the hoist rope within 14 days.', 'الإصلاح المطلوب: استبدال حبل الرفع خلال 14 يومًا.') + '</p>' +
          '<button type="button" class="ui-btn is-outline is-block is-sm"' + T(L('Opens the full certificate PDF.', 'يفتح ملف PDF الكامل للشهادة.')) + '>' + ic('pdf') + L('View full PDF', 'عرض ملف PDF كاملاً') + '</button></div>' +
        '<label class="ui-field"><span>' + L('Your comments', 'تعليقاتك') + '</span><span class="ui-input is-muted">' + L('Add a note for the record (optional)', 'أضف ملاحظة للسجل (اختياري)') + '</span></label>' +
        '<div class="ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + L('Sign here', 'وقّع هنا') + '</span></div>' +
          '<div class="ui-sig" data-sig><button type="button" class="ui-sig-clear" data-sign-clear>' + L('Clear', 'مسح') + '</button><span class="ui-sig-hint" data-sig-hint hidden>' + L('Tap to sign again', 'اضغط للتوقيع مجددًا') + '</span>' + signPath + '</div></div>' +
        '<div class="ui-grid2"><label class="ui-field"><span>' + L('Full name', 'الاسم الكامل') + '</span><span class="ui-input">' + L('Eng. Adel Mansour', 'م. عادل منصور') + '</span></label><label class="ui-field"><span>' + L('License no.', 'رقم الترخيص') + '</span><span class="ui-input">LIC-4471</span></label></div>' +
        '<label class="ui-field"><span>' + L('Send signed PDF to', 'إرسال ملف PDF الموقّع إلى') + '</span><span class="ui-input" dir="ltr">certificates@deltasteel.example</span></label>' +
        '<p class="ui-meta" data-sign-hint hidden>' + L('Sign above and fill in your name, license and a valid email to approve.', 'وقّع أعلاه واملأ اسمك ورقم الترخيص وبريدًا صالحًا للاعتماد.') + '</p>' +
        '<div class="ui-btns"><button type="button" class="ui-btn is-danger"' + T(L('Certificate returned to the inspector.', 'أُعيدت الشهادة إلى المفتش.')) + '>' + ic('return') + L('Return', 'إعادة') + '</button><button type="button" class="ui-btn is-ok" data-approve' + T(L('Certificate approved and signed.', 'تم اعتماد الشهادة وتوقيعها.')) + ' style="background:#15803d;border-color:#15803d;color:#fff">' + ic('check') + L('Approve', 'اعتماد') + '</button></div>' +
        '<p class="ui-meta" style="text-align:center">' + L('Approving records the certificate as sent to the client’s certificates email.', 'يسجّل الاعتماد الشهادة كمرسلة إلى بريد الشهادات الخاص بالعميل.') + '</p>' +
      '</div>' + reviewNav(0));
  };

  // 11. Technical manager: sent certificates (the last step of the tour)
  screens.sent = function () {
    function card(client, equip, verdict, to, when) {
      return '<div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + client + '</span>' + verdict + '</div><div class="ui-text">' + equip + '</div>' +
        '<div class="ui-row ui-between"><span class="ui-meta">' + ic('mail', 'ico" style="width:1.1em;height:1.1em;vertical-align:-.2em;margin-inline-end:.3em') + L('Sent to ', 'أُرسلت إلى ') + '<span dir="ltr">' + to + '</span></span></div>' +
        '<div class="ui-row ui-between"><span class="ui-meta">' + when + '</span><button type="button" class="ui-btn is-sm is-outline"' + T(L('Opens the signed certificate PDF.', 'يفتح ملف PDF للشهادة الموقّعة.')) + '>' + ic('pdf') + L('View PDF', 'عرض PDF') + '</button></div></div>';
    }
    return shell(status(true) +
      titleHead(L('Sent certificates', 'الشهادات المرسلة'), L('Signed and recorded as sent', 'موقّعة ومسجّلة كمرسلة')) +
      '<div class="ui-body">' +
        card(L('Delta Steel Works', 'دلتا للصلب'), L('Overhead crane — 10 t', 'رافعة علوية — 10 طن'), chip(L('With conditions', 'بشروط'), 'amber'), 'certificates@deltasteel.example', L('Today, 10:20', 'اليوم 10:20')) +
        card(L('Nile Packaging', 'النيل للتغليف'), L('Air compressor — 10 bar', 'ضاغط هواء — 10 بار'), chip(L('Safe', 'آمنة'), 'green'), 'quality@nilepackaging.example', L('Yesterday', 'أمس')) +
        card(L('Sunrise Foods', 'صن رايز للأغذية'), L('Air compressor — 8 bar', 'ضاغط هواء — 8 بار'), chip(L('Safe', 'آمنة'), 'green'), 'ops@sunrisefoods.example', L('15 Oct', '15 أكتوبر')) +
      '</div>' + reviewNav(1));
  };

  /* ---------- Fragments for the page (outside a phone) ---------- */
  var frags = {};

  frags.lifecycle = function () {
    function tl(dot, t, s) { return '<div class="ui-tl-i"><span class="ui-tl-dot ' + dot + '"></span><div class="ui-tl-b"><b>' + t + '</b><span>' + s + '</span></div></div>'; }
    return '<div class="ui-frag"><div class="ui-card">' + jstep(3) + '</div><div class="ui-card"><div class="ui-row ui-between" style="margin-block-end:12px"><span class="ui-h">' + L('Delta Steel Works · R-2041', 'دلتا للصلب · R-2041') + '</span>' + chip(L('In progress', 'قيد التنفيذ'), 'blue') + '</div><div class="ui-tl">' +
      tl('is-green', L('Task accepted', 'تم قبول المهمة'), L('Omar Hassan · 19 Oct, 08:40', 'عمر حسن · 19 أكتوبر، 08:40')) +
      tl('is-green', L('Assigned', 'تم التعيين'), L('Coordinator · 16 Oct, 14:05', 'المنسق · 16 أكتوبر، 14:05')) +
      tl('', L('Quote accepted · price locked', 'مقبول · السعر مقفل'), L('Mona Adel · 16 Oct, 10:22', 'منى عادل · 16 أكتوبر، 10:22')) +
      tl('', L('Request received', 'تم استلام الطلب'), L('by email · 15 Oct, 09:12', 'عبر البريد الإلكتروني · 15 أكتوبر، 09:12')) +
    '</div></div></div>';
  };
  frags.questions = function () {
    return '<div class="ui-frag"><div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + L('Step 3 of 5 · Examination questions', 'الخطوة 3 من 5 · أسئلة الفحص') + '</span><span class="ui-saved">' + ic('check') + L('Saved', 'تم الحفظ') + '</span></div><div class="ui-steps"><i class="is-done"></i><i class="is-done"></i><i class="is-done"></i><i></i><i></i></div>' +
      question(L('Has the equipment been installed correctly?', 'هل تم تركيب المعدة بشكل صحيح؟'), 0) +
      question(L('Within an interval of 6 months?', 'خلال فترة 6 أشهر؟'), 0) +
      question(L('After the occurrence of exceptional circumstances?', 'بعد حدوث ظروف استثنائية؟'), 1) +
      '</div></div>';
  };
  frags.signoff = function () {
    return '<div class="ui-frag"><div class="ui-card ui-gap"><div class="ui-verdict is-amber">' + ic('alert') + '<span>' + L('Safe with conditions', 'آمنة بشروط') + '</span></div>' +
      '<div class="ui-sig" style="height:5.4em">' + signPath + '</div>' +
      '<div class="ui-btns"><span class="ui-btn is-danger">' + ic('return') + L('Return', 'إعادة') + '</span><span class="ui-btn" style="background:#15803d">' + ic('check') + L('Approve', 'اعتماد') + '</span></div></div></div>';
  };
  /* Larger cut-outs for the bento cards: real app UI, cropped by the card */
  frags['peek-quote'] = function () {
    function tl(dot, t, sub) { return '<div class="ui-tl-i"><span class="ui-tl-dot ' + dot + '"></span><div class="ui-tl-b"><b>' + t + '</b><span>' + sub + '</span></div></div>'; }
    return '<div class="ui-frag" style="width:min(100%,460px)"><div class="ui-card"><div class="ui-row ui-between" style="margin-block-end:12px"><span class="ui-h">' + L('Negotiation history', 'سجل التفاوض') + '</span>' + chip(L('Your turn', 'دورك'), 'amber') + '</div><div class="ui-tl">' +
      tl('', L('Quote v1 sent', 'تم إرسال عرض الإصدار 1'), L('16 Oct · by you · valid 14 days', '16 أكتوبر · بواسطتك · صالح 14 يومًا')) +
      tl('is-amber', L('Client countered EGP 4,200', 'عرض العميل المضاد 4,200 ج.م'), L('Counter-offer', 'عرض مضاد')) +
      tl('is-now', L('Your response', 'ردّك'), L('Send new price (v2) · Accept client’s price', 'إرسال سعر جديد (إصدار 2) · قبول سعر العميل')) +
    '</div></div></div>';
  };
  frags['peek-schedule'] = function () {
    function row(name, track, cls, note) { return '<div class="ui-trow"><b>' + name + '</b><div class="ui-track ' + (cls || '') + '"' + (note ? ' data-note="' + esc(note) + '"' : '') + '>' + track + '</div></div>'; }
    function blk(l, w, text, cls) { return '<span class="ui-block ' + (cls || '') + '" style="inset-inline-start:' + l + '%;width:' + w + '%">' + text + '</span>'; }
    return '<div class="ui-frag" style="width:min(100%,420px)"><div class="ui-card ui-gap"><div class="ui-axis"><span></span><span>08</span><span>10</span><span>12</span><span>14</span><span>16</span></div>' +
      row(L('Omar Hassan', 'عمر حسن'), blk(2, 38, L('Nile Pack.', 'النيل'), 'is-done') + blk(48, 44, L('Harbor', 'الميناء'), 'is-alt')) +
      row(L('Karim Fathy', 'كريم فتحي'), blk(22, 56, L('Sunrise Foods', 'صن رايز'))) +
      row(L('Laila Samir', 'ليلى سمير'), '', 'is-leave', L('On leave until 23 Oct', 'في إجازة حتى 23 أكتوبر')) +
      row(L('Mariam Said', 'مريم سعيد'), '', 'is-free', L('Free all day', 'متفرغ طوال اليوم')) +
    '</div></div>';
  };
  frags['peek-review'] = function () {
    return '<div class="ui-frag" style="width:min(100%,430px)"><div class="ui-row" style="flex-wrap:wrap;gap:8px"><span class="tag tag--safe"><span class="tag-hole"></span>' + L('Safe to operate', 'آمن للتشغيل') + '</span><span class="tag tag--cond"><span class="tag-hole"></span>' + L('Safe with conditions', 'آمن بشروط') + '</span><span class="tag tag--bad"><span class="tag-hole"></span>' + L('Not safe — out of service', 'غير آمن — خارج الخدمة') + '</span></div>' +
      '<div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + L('Sign here', 'وقّع هنا') + '</span><span class="ui-meta">' + L('Eng. Adel Mansour · LIC-4471', 'م. عادل منصور · LIC-4471') + '</span></div><div class="ui-sig" style="height:5em">' + signPath + '</div></div></div>';
  };
  frags['peek-clients'] = function () {
    function client(ini, name, loc, chips) {
      return '<div class="ui-card ui-gap"><div class="ui-row"><span class="ui-av ui-av--tint" style="width:2.6em;height:2.6em;font-size:.85em">' + ini + '</span><div class="ui-grow"><div class="ui-h">' + name + '</div><div class="ui-meta">' + loc + '</div></div></div><div class="ui-row" style="flex-wrap:wrap;gap:.4em">' + chips + '</div></div>';
    }
    return '<div class="ui-frag" style="width:min(100%,420px)">' +
      client(L('DS', 'دص'), L('Delta Steel Works', 'دلتا للصلب'), L('Sadat City', 'مدينة السادات'), chip(L('3 equipment', '3 معدات'), 'soft') + chip(L('1 open job', 'مهمة مفتوحة'), 'blue') + chip(L('2 due in 30 days', '2 تستحق خلال 30 يومًا'), 'amber')) +
      client(L('NP', 'نت'), L('Nile Packaging', 'النيل للتغليف'), L('10th of Ramadan', 'العاشر من رمضان'), chip(L('1 equipment', 'معدة'), 'soft') + chip(L('1 open job', 'مهمة مفتوحة'), 'blue')) +
    '</div>';
  };
  frags['peek-time'] = function () {
    function line(a, b, strong) { return '<div class="ui-row ui-between"><span class="ui-meta">' + a + '</span><span class="' + (strong ? 'ui-strong' : 'ui-meta') + '" style="font-size:.78em">' + b + '</span></div>'; }
    return '<div class="ui-frag" style="width:min(100%,420px)"><div class="ui-card ui-gap"><div class="ui-row ui-between"><span class="ui-h">' + L('Delta Steel Works', 'دلتا للصلب') + '</span>' + chip(L('Approved', 'معتمد'), 'green', 'lock') + '</div>' +
      line(L('Inspection & load testing', 'الفحص واختبار الحمل'), L('1h 30m', '1س 30د')) + line(L('Report preparation', 'إعداد التقرير'), L('45m', '45د')) + line(L('Waiting', 'انتظار'), L('15m', '15د')) +
      '<div class="ui-divider"></div>' + line(L('Net working time', 'صافي وقت العمل'), L('2h 30m × EGP 400 per hour', '2س 30د × 400 ج.م للساعة'), true) + '</div></div>';
  };

  /* ---------- Interactions inside a rendered screen ---------- */
  function toast(root, msg) {
    var t = root.querySelector('.ui-toast');
    if (!t) return;
    t.textContent = msg; t.classList.add('is-show');
    clearTimeout(t._h); t._h = setTimeout(function () { t.classList.remove('is-show'); }, 2200);
  }
  function setPane(root, group, key) {
    root.querySelectorAll('[data-pane-of="' + group + '"]').forEach(function (p) { p.hidden = p.getAttribute('data-pane') !== key; });
  }
  function wizard(root, step) {
    var bar = root.querySelectorAll('[data-wiz-bar] i');
    var titles = [null, null, null, L('Examination questions', 'أسئلة الفحص'), L('Defects & remedial action', 'العيوب والإجراءات التصحيحية'), L('Conclusion', 'الخلاصة')];
    root.querySelectorAll('[data-wiz-pane]').forEach(function (p) { p.hidden = p.getAttribute('data-wiz-pane') !== String(step); });
    bar.forEach(function (b, i) { b.classList.toggle('is-done', i < step); });
    var lab = root.querySelector('[data-wiz-label]');
    if (lab) lab.textContent = L('Step ' + step + ' of 5 · ' + titles[step], 'الخطوة ' + step + ' من 5 · ' + titles[step]);
    var next = root.querySelector('[data-wiz-next]');
    if (next) next.textContent = step === 5 ? L('Submit to technical manager', 'إرسال إلى المدير الفني') : L('Next', 'التالي');
    var back = root.querySelector('[data-wiz-back]');
    if (back) back.disabled = step <= 3;
    root.setAttribute('data-step', step);
    var body = root.querySelector('.ui-body'); if (body) body.scrollTop = 0;
  }
  function syncApprove(root) {
    var pad = root.querySelector('[data-sig]');
    if (!pad) return;
    var signed = !pad.classList.contains('is-empty');
    var btn = root.querySelector('[data-approve]'); if (btn) btn.disabled = !signed;
    var hint = root.querySelector('[data-sign-hint]'); if (hint) hint.hidden = signed;
    var h2 = root.querySelector('[data-sig-hint]'); if (h2) h2.hidden = signed;
    var p = pad.querySelector('path'); if (p) p.style.display = signed ? '' : 'none';
    var clr = pad.querySelector('[data-sign-clear]'); if (clr) clr.hidden = !signed;
  }

  function bind(root) {
    root.addEventListener('click', function (e) {
      var el = e.target.closest('button, [data-sig]');
      if (!el || !root.contains(el)) return;

      if (el.hasAttribute('data-tab')) {
        var box = el.closest('[data-tabs]');
        box.querySelectorAll('[data-tab]').forEach(function (b) { b.classList.toggle('is-on', b === el); });
        setPane(root, box.getAttribute('data-tabs'), el.getAttribute('data-tab'));
        return;
      }
      if (el.hasAttribute('data-seg')) {
        var grp = el.parentNode, tone = el.getAttribute('data-tone');
        grp.querySelectorAll('[data-seg]').forEach(function (b) { b.classList.remove('is-on', 'is-pass', 'is-fail'); });
        el.classList.add('is-on'); if (tone) el.classList.add(tone);
        var sv = root.querySelector('[data-save]');
        if (sv) {
          sv.lastChild.textContent = L('Saving…', 'جارٍ الحفظ…');
          clearTimeout(sv._h); sv._h = setTimeout(function () { sv.lastChild.textContent = L('Saved', 'تم الحفظ'); }, 700);
        }
        return;
      }
      if (el.hasAttribute('data-pick')) {
        var g = el.closest('[data-pickgroup]');
        g.querySelectorAll('[data-pick]').forEach(function (b) { var r = b.querySelector('.ui-radio'); if (r) r.classList.toggle('is-on', b === el); });
        var tgt = root.querySelector('[data-pick-target]');
        if (tgt) tgt.textContent = tgt.getAttribute('data-pattern').replace('{name}', el.getAttribute('data-pick'));
        return;
      }
      if (el.hasAttribute('data-toggle')) {
        var r = root.querySelector('[data-reveal="' + el.getAttribute('data-toggle') + '"]');
        if (r) { r.hidden = !r.hidden; el.textContent = r.hidden ? L('Details', 'التفاصيل') : L('Hide details', 'إخفاء التفاصيل'); }
        return;
      }
      if (el.hasAttribute('data-wiz-next')) {
        var s = parseInt(root.getAttribute('data-step') || '3', 10);
        if (s >= 5) { toast(root, el.getAttribute('data-submit-toast')); return; }
        wizard(root, s + 1); return;
      }
      if (el.hasAttribute('data-wiz-back')) { wizard(root, Math.max(3, parseInt(root.getAttribute('data-step') || '3', 10) - 1)); return; }
      if (el.hasAttribute('data-sign-clear')) { var pad = root.querySelector('[data-sig]'); pad.classList.add('is-empty'); pad.classList.remove('is-drawing'); syncApprove(root); return; }
      if (el.hasAttribute('data-sig')) {
        if (el.classList.contains('is-empty')) {
          el.classList.remove('is-empty'); syncApprove(root);
          el.classList.remove('is-drawing'); void el.offsetWidth; el.classList.add('is-drawing');
        }
        return;
      }
      if (el.hasAttribute('data-toast')) { toast(root, el.getAttribute('data-toast')); }
    });
  }

  /* ---------- Public API ---------- */
  I.screens = screens;
  I.frags = frags;
  I.renderScreen = function (id, container) {
    var html = screens[id] ? screens[id]() : '';
    container.innerHTML = html;
    container.setAttribute('data-screen-id', id);
    if (!container._bound) { bind(container); container._bound = true; }
    container.querySelectorAll('.ui-body').forEach(function (b) { b.tabIndex = 0; b.setAttribute('role', 'region'); b.setAttribute('aria-label', L('Screen content (scrollable)', 'محتوى الشاشة (قابل للتمرير)')); });
    if (id === 'report') wizard(container, 3);
    if (id === 'review') { var pad = container.querySelector('[data-sig]'); if (pad) pad.classList.remove('is-empty'); syncApprove(container); }
  };
  I.renderFrag = function (name, container) {
    if (frags[name]) container.innerHTML = frags[name]();
  };
})();
