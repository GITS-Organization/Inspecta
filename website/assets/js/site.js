/* ==========================================================================
   Inspecta website: behaviour
   Language (EN/AR + RTL), navigation, product explorer, contact form.
   Plain script, no dependencies; works from file:// as well as a server.
   ========================================================================== */
(function () {
  'use strict';
  var I = (window.Inspecta = window.Inspecta || { lang: 'en' });
  var doc = document, root = doc.documentElement;
  var $ = function (s, c) { return (c || doc).querySelector(s); };
  var $$ = function (s, c) { return Array.prototype.slice.call((c || doc).querySelectorAll(s)); };
  var L = function (en, ar) { return I.lang === 'ar' ? ar : en; };
  var store = {
    get: function (k) { try { return window.localStorage.getItem(k); } catch (e) { return null; } },
    set: function (k, v) { try { window.localStorage.setItem(k, v); } catch (e) { /* storage unavailable */ } }
  };

  /* ------------------------------------------------------------------ *
   * Language: English text lives in the HTML; Arabic in data-ar* attrs *
   * ------------------------------------------------------------------ */
  function translateAttrs(lang) {
    $$('*').forEach(function (el) {
      var attrs = el.attributes, i, a;
      for (i = attrs.length - 1; i >= 0; i--) {
        a = attrs[i].name;
        if (a === 'data-ar') {
          if (!el.hasAttribute('data-en')) el.setAttribute('data-en', el.innerHTML);
          el.innerHTML = el.getAttribute(lang === 'ar' ? 'data-ar' : 'data-en');
        } else if (a.indexOf('data-ar-') === 0) {
          var name = a.slice(8), orig = 'data-en-' + name;
          if (!el.hasAttribute(orig)) el.setAttribute(orig, el.getAttribute(name) || '');
          el.setAttribute(name, el.getAttribute(lang === 'ar' ? a : orig));
        }
      }
    });
  }

  function applyLang(lang, persist) {
    I.lang = lang;
    root.lang = lang;
    root.dir = lang === 'ar' ? 'rtl' : 'ltr';
    translateAttrs(lang);

    $$('[data-set-lang]').forEach(function (b) { b.setAttribute('aria-pressed', String(b.getAttribute('data-set-lang') === lang)); });
    if (persist) store.set('inspecta-lang', lang);

    renderHero();
    renderFrags();
    renderTrail();
    if (explorer) explorer.render();
    if (tour) tour.render();
    labelTable();
    updateFormText();
    var cp = $('[data-copyright]');
    if (cp) cp.textContent = '© ' + new Date().getFullYear();
  }

  function initialLang() {
    var m = /[?&]lang=(ar|en)/.exec(window.location.search);
    if (m) return m[1];
    var saved = store.get('inspecta-lang');
    if (saved === 'ar' || saved === 'en') return saved;
    var nav = (navigator.language || '').toLowerCase();
    return nav.indexOf('ar') === 0 ? 'ar' : 'en';
  }

  /* ------------------------------------------------------------------ *
   * Hero devices, fragments, comparison-table labels                    *
   * ------------------------------------------------------------------ */
  function renderHero() {
    $$('[data-hero-screen]').forEach(function (d) {
      I.renderScreen(d.getAttribute('data-hero-screen'), $('.device-screen', d));
    });
    var stage = $('.hero-stage');
    if (stage) stage.setAttribute('inert', '');
    if (floats) floats.paint();
  }
  function ico(name) { return '<svg class="ico" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }

  /* Real in-app toasts that float beside the hero phones, taking turns */
  var reducedMotion = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
  var floats = (function () {
    var els = { a: $('[data-float="a"]'), b: $('[data-float="b"]') };
    if (!els.a || !els.b) return null;
    var sets = {
      a: [
        { icon: 'check', cls: '', t: ['Certificate approved and signed.', 'تم اعتماد الشهادة وتوقيعها.'] },
        { icon: 'return', cls: 'is-amber', t: ['Certificate returned to the inspector.', 'أُعيدت الشهادة إلى المفتش.'] }
      ],
      b: [
        { icon: 'clip', cls: '', t: ['Task accepted.', 'تم قبول المهمة.'] },
        { icon: 'send', cls: '', t: ['Certificate submitted.', 'تم إرسال الشهادة.'] }
      ]
    };
    var at = { a: 0, b: 0 };
    function paint() {
      ['a', 'b'].forEach(function (k) {
        var m = sets[k][at[k]], el = els[k];
        $('use', el).setAttribute('href', '#i-' + m.icon);
        $('.float-ico', el).className = 'float-ico' + (m.cls ? ' ' + m.cls : '');
        $('.float-text', el).textContent = L(m.t[0], m.t[1]);
      });
    }
    function swap(k) {
      if (doc.hidden) return;
      els[k].classList.add('is-swapping');
      window.setTimeout(function () { at[k] = (at[k] + 1) % sets[k].length; paint(); els[k].classList.remove('is-swapping'); }, 300);
    }
    if (!reducedMotion) {
      window.setInterval(function () { swap('a'); }, 5200);
      window.setInterval(function () { swap('b'); }, 6600);
    }
    return { paint: paint };
  })();
  function renderFrags() {
    $$('[data-frag]').forEach(function (el) { I.renderFrag(el.getAttribute('data-frag'), el); });
  }
  function labelTable() {
    var ths = $$('.compare thead th');
    if (ths.length < 3) return;
    $$('.compare tbody tr').forEach(function (tr) {
      var tds = $$('td', tr);
      if (tds[0]) tds[0].setAttribute('data-label', ths[1].textContent.trim());
      if (tds[1]) tds[1].setAttribute('data-label', ths[2].textContent.trim());
    });
  }

  /* ------------------------------------------------------------------ *
   * Header, mobile menu, active section                                 *
   * ------------------------------------------------------------------ */
  var header = $('.site-header'), nav = $('#site-nav'), toggle = $('.menu-toggle');

  function setMenu(open) {
    nav.classList.toggle('is-open', open);
    header.classList.toggle('menu-open', open);
    toggle.setAttribute('aria-expanded', String(open));
    doc.body.classList.toggle('menu-lock', open);
    updateHeader();
  }
  var toneSections = $$('main > section');
  function updateHeader() {
    var open = nav.classList.contains('is-open'), y = 46, tone = 'dark', i, r;
    for (i = 0; i < toneSections.length; i++) {
      r = toneSections[i].getBoundingClientRect();
      if (r.top <= y && r.bottom > y) tone = toneSections[i].getAttribute('data-tone') || 'light'; // later sheets sit on top
    }
    header.classList.toggle('is-light', tone === 'light' && !open);
  }
  toggle.addEventListener('click', function () { setMenu(!nav.classList.contains('is-open')); });
  $$('a', nav).forEach(function (a) { a.addEventListener('click', function () { setMenu(false); }); });
  doc.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && nav.classList.contains('is-open')) { setMenu(false); toggle.focus(); }
  });
  window.addEventListener('resize', function () { if (window.innerWidth >= 1200 && nav.classList.contains('is-open')) setMenu(false); });
  var ticking = false;
  window.addEventListener('scroll', function () {
    if (ticking) return; ticking = true;
    window.requestAnimationFrame(function () { updateHeader(); ticking = false; });
  }, { passive: true });
  updateHeader();

  var sectionMap = { home: '', problems: 'solutions', solutions: 'solutions', explorer: 'solutions', features: 'features', 'try': 'try', 'how-it-works': 'how-it-works', industries: '', benefits: '', why: '', about: '', where: 'where', faq: 'faq', contact: '' };
  if ('IntersectionObserver' in window) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) return;
        var target = sectionMap[en.target.id];
        $$('.nav-list a').forEach(function (a) {
          if (a.getAttribute('href') === '#' + target) a.setAttribute('aria-current', 'true'); else a.removeAttribute('aria-current');
        });
      });
    }, { rootMargin: '-45% 0px -50% 0px' });
    Object.keys(sectionMap).forEach(function (id) { var s = doc.getElementById(id); if (s) io.observe(s); });
  }

  /* ------------------------------------------------------------------ *
   * Product explorer                                                    *
   * ------------------------------------------------------------------ */
  var explorer = (function () {
    var host = $('[data-explorer]');
    if (!host) return null;
    var order = $$('.xp-item', host).map(function (b) { return b.getAttribute('data-screen'); });
    var items = {}; $$('.xp-item', host).forEach(function (b) { items[b.getAttribute('data-screen')] = b; });
    var panels = {}; $$('.xp-panel', host).forEach(function (p) { panels[p.getAttribute('data-panel')] = p; });
    var stage = $('[data-stage-screen]', host), count = $('[data-xp-count]', host);
    var current = 'inbox';

    function render() {
      I.renderScreen(current, stage);
      var label = $('span', items[current]).textContent;
      stage.setAttribute('aria-label', L('Sample screen: ', 'شاشة تجريبية: ') + label);
      count.textContent = (order.indexOf(current) + 1) + ' / ' + order.length;
    }
    function show(id, focus) {
      if (!items[id]) return;
      current = id;
      order.forEach(function (k) {
        items[k].setAttribute('aria-current', k === id ? 'true' : 'false');
        panels[k].classList.toggle('is-active', k === id);
      });
      render();
      if (focus) items[id].focus({ preventScroll: true });
      var active = items[id];
      var list = active.parentNode;
      if (list.scrollWidth > list.clientWidth) { // horizontal chip row on small screens
        list.scrollTo({ left: active.offsetLeft - list.clientWidth / 2 + active.clientWidth / 2, behavior: 'smooth' });
      }
    }
    function step(d) { show(order[(order.indexOf(current) + d + order.length) % order.length]); }

    order.forEach(function (k) { items[k].addEventListener('click', function () { show(k); }); });
    $('[data-xp-next]', host).addEventListener('click', function () { step(1); });
    $('[data-xp-prev]', host).addEventListener('click', function () { step(-1); });
    host.addEventListener('keydown', function (e) {
      var inList = e.target.closest && e.target.closest('.xp-list');
      if (!inList) return;
      var vertical = window.getComputedStyle(inList).flexDirection === 'column';
      var next = vertical ? 'ArrowDown' : (root.dir === 'rtl' ? 'ArrowLeft' : 'ArrowRight');
      var prev = vertical ? 'ArrowUp' : (root.dir === 'rtl' ? 'ArrowRight' : 'ArrowLeft');
      var i = order.indexOf(e.target.getAttribute('data-screen'));
      if (e.key === next && i < order.length - 1) { e.preventDefault(); items[order[i + 1]].focus(); }
      else if (e.key === prev && i > 0) { e.preventDefault(); items[order[i - 1]].focus(); }
    });

    // Cross-links from the rest of the page: "See the examination report"
    $$('[data-go-screen]').forEach(function (a) {
      a.addEventListener('click', function () { show(a.getAttribute('data-go-screen')); });
    });

    show(current);
    return { render: render, show: show };
  })();

  /* ------------------------------------------------------------------ *
   * Pointer effects: hero parallax, card spotlight                      *
   * ------------------------------------------------------------------ */
  var finePointer = window.matchMedia && window.matchMedia('(hover: hover) and (prefers-reduced-motion: no-preference)').matches;
  if (finePointer) {
    var hero = $('.hero');
    if (hero) {
      hero.addEventListener('pointermove', function (e) {
        var r = hero.getBoundingClientRect();
        hero.style.setProperty('--px', ((e.clientX - r.left) / r.width - 0.5).toFixed(3));
        hero.style.setProperty('--py', ((e.clientY - r.top) / r.height - 0.5).toFixed(3));
      }, { passive: true });
      hero.addEventListener('pointerleave', function () { hero.style.setProperty('--px', 0); hero.style.setProperty('--py', 0); });
    }
    $$('.solution').forEach(function (card) {
      card.addEventListener('pointermove', function (e) {
        var r = card.getBoundingClientRect();
        card.style.setProperty('--mx', (e.clientX - r.left) + 'px');
        card.style.setProperty('--my', (e.clientY - r.top) + 'px');
      }, { passive: true });
    });
  }

  /* ------------------------------------------------------------------ *
   * Pinned tour: the phone follows the step in view                     *
   * ------------------------------------------------------------------ */
  var tour = (function () {
    var host = $('[data-tour]');
    if (!host) return null;
    var steps = $$('.tour-step', host), list = $('.tour-steps', host);
    var device = $('.device--tour', host), screen = $('[data-tour-screen]', host);
    var current = null, swapTimer = null;
    $('.tour-stage', host).setAttribute('inert', '');

    function render() { if (current) I.renderScreen(current, screen); }
    function show(id) {
      if (id === current) return;
      var first = current === null;
      current = id;
      if (first) { render(); return; }
      device.classList.add('is-swapping');
      window.clearTimeout(swapTimer);
      swapTimer = window.setTimeout(function () { render(); device.classList.remove('is-swapping'); }, 160);
    }
    function update() {
      var mid = window.innerHeight * 0.5, active = 0;
      steps.forEach(function (s, i) { if (s.getBoundingClientRect().top < mid) active = i; });
      steps.forEach(function (s, i) {
        s.classList.toggle('is-active', i === active);
        s.classList.toggle('is-done', i < active);
        if (i === active) s.setAttribute('aria-current', 'step'); else s.removeAttribute('aria-current');
      });
      show(steps[active].getAttribute('data-screen'));
      var a = steps[0].querySelector('.step-node').getBoundingClientRect();
      var z = steps[steps.length - 1].querySelector('.step-node').getBoundingClientRect();
      var p = (mid - (a.top + a.height / 2)) / (z.top - a.top);
      list.style.setProperty('--p', Math.max(0, Math.min(1, p)).toFixed(3));
    }
    var queued = false;
    function onScroll() { if (queued) return; queued = true; window.requestAnimationFrame(function () { update(); queued = false; }); }
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', onScroll);
    update();
    return { render: render };
  })();

  /* ------------------------------------------------------------------ *
   * Try it: one examination result, four places it appears             *
   * Wording is the app's own (lib/l10n/*.arb, the certificate PDF).      *
   * ------------------------------------------------------------------ */
  var RESULTS = {
    safe: {
      option: ['Safe to operate', 'آمن للتشغيل'], optionTone: 'green',
      review: ['Safe to operate', 'آمنة للتشغيل'], reviewTone: 'is-ok', reviewIcon: 'check', defect: false,
      pdf: 'Satisfactory',
      badge: ['Safe', 'آمنة'], badgeTone: 'green'
    },
    cond: {
      option: ['Safe with conditions (fix in 14 days)', 'آمن بشروط (الإصلاح خلال 14 يومًا)'], optionTone: 'amber',
      review: ['Safe with conditions', 'آمنة بشروط'], reviewTone: 'is-blue', reviewIcon: 'alert', defect: true,
      pdf: 'Satisfactory with conditions',
      badge: ['With conditions', 'بشروط'], badgeTone: 'amber'
    },
    bad: {
      option: ['Not safe — out of service', 'غير آمن — خارج الخدمة'], optionTone: 'red',
      review: ['Not safe — out of service', 'غير آمنة — خارج الخدمة'], reviewTone: 'is-red', reviewIcon: 'alert', defect: true,
      pdf: 'Not satisfactory',
      badge: ['Not safe', 'غير آمنة'], badgeTone: 'red'
    }
  };
  var trailHost = $('[data-result-trail]'), trailNow = 'cond';

  function renderTrail() {
    if (!trailHost) return;
    var r = RESULTS[trailNow];
    function row(i, icon, label, sub, inner) {
      return '<div class="trail-row" style="--i:' + i + '"><span class="trail-node">' + ico(icon) + '</span><div class="trail-body">' +
        '<p class="trail-label">' + label + '<span>' + sub + '</span></p>' + inner + '</div></div>';
    }
    var defect = L('Broken wires found in the hoist rope near the hook block.', 'أسلاك مقطوعة في حبل الرفع بالقرب من كتلة الخطاف.');
    trailHost.innerHTML =
      row(0, 'clip', L('The inspector picks it', 'المفتش يختارها'), L('Last step of the report: Conclusion', 'الخطوة الأخيرة من التقرير: الخلاصة'),
        '<div class="ui-card"><div class="trail-pick"><span class="ui-radio is-on"></span><span class="ui-chip is-' + r.optionTone + '">' + L(r.option[0], r.option[1]) + '</span></div></div>') +
      row(1, 'pen', L('The technical manager sees it', 'المدير الفني يراها'), L('On the review screen, with the defect description if there is one', 'في شاشة المراجعة، مع وصف العيب إن وُجد'),
        '<div class="ui-card"><div class="ui-verdict ' + r.reviewTone + '">' + ico(r.reviewIcon) + '<span>' + L(r.review[0], r.review[1]) + '</span></div>' +
        (r.defect ? '<p class="ui-note">' + defect + '</p>' : '') + '</div>') +
      row(2, 'pdf', L('The certificate PDF states it', 'ملف الشهادة PDF يذكرها'), L('The conclusion line, issued in English today', 'سطر الخلاصة، ويصدر حاليًا بالإنجليزية'),
        '<div class="ui-card trail-pdf" dir="ltr">' + ico('pdf') + '<div><small>Conclusion</small><b>' + r.pdf + '</b></div></div>') +
      row(3, 'send', L('The sent list shows it', 'قائمة الشهادات المرسلة تعرضها'), L('A badge on each signed certificate', 'شارة على كل شهادة موقّعة'),
        '<div class="ui-card trail-sent"><div><b>' + L('Delta Steel Works', 'دلتا للصلب') + '</b><small>' + L('Overhead crane — 10 t', 'رافعة علوية — 10 طن') + '</small></div>' +
        '<span class="ui-chip is-' + r.badgeTone + '">' + L(r.badge[0], r.badge[1]) + '</span></div>');
    var status = $('[data-result-status]');
    if (status) status.textContent = L('Showing: ', 'المعروض: ') + L(r.review[0], r.review[1]);
  }
  $$('[data-result]').forEach(function (b) {
    b.addEventListener('click', function () {
      trailNow = b.getAttribute('data-result');
      $$('[data-result]').forEach(function (o) { o.setAttribute('aria-pressed', String(o === b)); });
      renderTrail();
    });
  });

  /* ------------------------------------------------------------------ *
   * Contact / demo form                                                 *
   * ------------------------------------------------------------------ */
  var form = $('#contact-form'), mode = 'demo';
  var cfg = (window.INSPECTA_CONFIG && window.INSPECTA_CONFIG.contact) || {};

  function updateFormText() {
    if (!form) return;
    $('[data-form-title]', form).textContent = mode === 'demo' ? L('Request a demo', 'طلب عرض تجريبي') : L('Contact our team', 'تواصل مع فريقنا');
    $('[data-submit-label]', form).textContent = mode === 'demo' ? L('Send request', 'أرسل الطلب') : L('Send message', 'أرسل الرسالة');
    var opt = $('[data-message-opt]', form);
    opt.hidden = mode === 'contact';
    var msg = $('#f-message');
    msg.placeholder = mode === 'demo'
      ? L('For example: the equipment you examine and how many inspectors you have', 'مثال: أنواع المعدات التي تفحصونها وعدد المفتشين')
      : L('How can we help?', 'كيف يمكننا مساعدتك؟');
    $$('.field.is-invalid', form).forEach(validateField); // refresh error language
  }
  function setMode(m) {
    mode = m;
    $$('[data-mode]').forEach(function (b) { b.setAttribute('aria-pressed', String(b.getAttribute('data-mode') === m)); });
    updateFormText();
  }
  $$('[data-mode]').forEach(function (b) { b.addEventListener('click', function () { setMode(b.getAttribute('data-mode')); focusForm(); }); });
  $$('[data-open-form]').forEach(function (a) {
    a.addEventListener('click', function () { setMode(a.getAttribute('data-open-form')); focusForm(); });
  });
  function focusForm() {
    if (form.getAttribute('data-state') === 'success') return;
    window.setTimeout(function () { var n = $('#f-name'); if (n) n.focus({ preventScroll: true }); }, 450);
  }

  var emailRe = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
  var rules = {
    name: function (v) { return v.trim().length < 2 ? L('Enter your full name.', 'أدخل اسمك الكامل.') : ''; },
    email: function (v) { return !v.trim() ? L('Enter your work email.', 'أدخل بريدك الإلكتروني للعمل.') : (!emailRe.test(v.trim()) ? L('Enter a valid email address.', 'أدخل بريدًا إلكترونيًا صالحًا.') : ''); },
    company: function (v) { return v.trim().length < 2 ? L('Enter your company or organization.', 'أدخل اسم شركتك أو جهتك.') : ''; },
    message: function (v) { return mode === 'contact' && v.trim().length < 10 ? L('Tell us how we can help (at least a sentence).', 'أخبرنا كيف يمكننا مساعدتك (جملة على الأقل).') : ''; }
  };
  function validateField(field) {
    var input = $('input, textarea', field);
    if (!input || !rules[input.name]) return true;
    var err = rules[input.name](input.value), p = $('.field-error', field);
    field.classList.toggle('is-invalid', !!err);
    p.textContent = err;
    if (err) { input.setAttribute('aria-invalid', 'true'); input.setAttribute('aria-describedby', p.id); }
    else { input.removeAttribute('aria-invalid'); input.removeAttribute('aria-describedby'); }
    return !err;
  }
  $$('.field', form).forEach(function (f) {
    var input = $('input, textarea', f);
    if (input) input.addEventListener('blur', function () { if (input.value || f.classList.contains('is-invalid')) validateField(f); });
    if (input) input.addEventListener('input', function () { if (f.classList.contains('is-invalid')) validateField(f); });
  });

  function setStatus(kind, text) {
    var s = $('[data-form-status]', form);
    s.className = 'form-status' + (kind ? ' is-' + kind : '');
    s.textContent = text || '';
  }
  function fsString(v) { return { stringValue: String(v) }; }

  form.addEventListener('submit', function (e) {
    e.preventDefault();
    if (form.getAttribute('data-state') === 'loading') return;
    setStatus('', '');
    var ok = true, firstBad = null;
    $$('.field', form).forEach(function (f) { if (!validateField(f)) { ok = false; if (!firstBad) firstBad = $('input, textarea', f); } });
    if (!ok) { firstBad.focus(); return; }

    var data = {
      name: $('#f-name').value.trim(), email: $('#f-email').value.trim(), company: $('#f-company').value.trim(),
      role: $('#f-role').value, message: $('#f-message').value.trim(), type: mode
    };
    var done = function () {
      form.setAttribute('data-state', 'success');
      setStatus('success', mode === 'demo'
        ? L('Thank you. Your demo request has been received, and we will get back to you to arrange a walkthrough.', 'شكرًا لك. استلمنا طلب العرض التجريبي وسنتواصل معك لترتيب العرض.')
        : L('Thank you. Your message has been received, and we will get back to you.', 'شكرًا لك. استلمنا رسالتك وسنتواصل معك.'));
      $('[data-form-status]', form).focus();
    };

    if ($('[name=website]', form).value) { done(); return; } // honeypot: pretend success
    var last = parseInt(store.get('inspecta-last-submit') || '0', 10);
    if (Date.now() - last < 30000) { setStatus('error', L('Please wait a moment before sending another request.', 'يرجى الانتظار قليلًا قبل إرسال طلب آخر.')); return; }
    if (!cfg.projectId || !cfg.collection) { setStatus('error', L('The contact form is not connected yet.', 'نموذج التواصل غير متصل بعد.')); return; }

    form.setAttribute('data-state', 'loading');
    $('[data-submit]', form).disabled = true;
    var ctrl = typeof AbortController !== 'undefined' ? new AbortController() : null;
    var timer = window.setTimeout(function () { if (ctrl) ctrl.abort(); }, 15000);
    var url = 'https://firestore.googleapis.com/v1/projects/' + encodeURIComponent(cfg.projectId) + '/databases/(default)/documents/' + encodeURIComponent(cfg.collection);
    var fields = {
      type: fsString(data.type), name: fsString(data.name), email: fsString(data.email), company: fsString(data.company),
      role: fsString(data.role), message: fsString(data.message), lang: fsString(I.lang), source: fsString('website'),
      createdAt: { timestampValue: new Date().toISOString() }
    };
    fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ fields: fields }), signal: ctrl ? ctrl.signal : undefined })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
      .then(function () { store.set('inspecta-last-submit', String(Date.now())); done(); })
      .catch(function () {
        form.removeAttribute('data-state');
        setStatus('error', L('We could not send your request. Please check your connection and try again in a moment.', 'تعذّر إرسال طلبك. يرجى التحقق من اتصالك والمحاولة مرة أخرى بعد قليل.'));
      })
      .then(function () { window.clearTimeout(timer); $('[data-submit]', form).disabled = false; });
  });

  /* ------------------------------------------------------------------ *
   * Go                                                                  *
   * ------------------------------------------------------------------ */
  $$('[data-set-lang]').forEach(function (b) {
    b.addEventListener('click', function () {
      var next = b.getAttribute('data-set-lang');
      if (next !== I.lang) applyLang(next, true);
    });
  });
  applyLang(initialLang(), false);
})();
