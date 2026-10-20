/* Language switch for the simple static pages (privacy notice). */
(function () {
  'use strict';
  var doc = document, root = doc.documentElement;
  function store(k, v) {
    try { if (v === undefined) return window.localStorage.getItem(k); window.localStorage.setItem(k, v); } catch (e) { return null; }
    return null;
  }
  function apply(lang, persist) {
    root.lang = lang; root.dir = lang === 'ar' ? 'rtl' : 'ltr';
    Array.prototype.forEach.call(doc.querySelectorAll('*'), function (el) {
      var attrs = el.attributes, i, a;
      for (i = attrs.length - 1; i >= 0; i--) {
        a = attrs[i].name;
        if (a === 'data-ar') {
          if (!el.hasAttribute('data-en')) el.setAttribute('data-en', el.innerHTML);
          el.innerHTML = el.getAttribute(lang === 'ar' ? 'data-ar' : 'data-en');
        } else if (a.indexOf('data-ar-') === 0) {
          var n = a.slice(8), o = 'data-en-' + n;
          if (!el.hasAttribute(o)) el.setAttribute(o, el.getAttribute(n) || '');
          el.setAttribute(n, el.getAttribute(lang === 'ar' ? a : o));
        }
      }
    });
    Array.prototype.forEach.call(doc.querySelectorAll('[data-set-lang]'), function (b) {
      b.setAttribute('aria-pressed', String(b.getAttribute('data-set-lang') === lang));
    });
    if (persist) store('inspecta-lang', lang);
  }
  var m = /[?&]lang=(ar|en)/.exec(window.location.search);
  var saved = store('inspecta-lang');
  var start = m ? m[1] : (saved === 'ar' || saved === 'en' ? saved : ((navigator.language || '').toLowerCase().indexOf('ar') === 0 ? 'ar' : 'en'));
  Array.prototype.forEach.call(doc.querySelectorAll('[data-set-lang]'), function (b) {
    b.addEventListener('click', function () { if (b.getAttribute('data-set-lang') !== root.lang) apply(b.getAttribute('data-set-lang'), true); });
  });
  apply(start, false);
})();
