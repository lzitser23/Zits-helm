// Pre-paint theme restore. A classic (non-module) script so it runs synchronously
// in <head> before first paint, avoiding a flash of the wrong theme. It mirrors the
// storage contract of zits-theme.js but stays dependency-free and fully guarded: if
// there is no stored selection it does nothing, so whatever theme the host markup
// hardcodes (for example a default class="dark") is respected. Legacy bare
// "dark"/"light" values only restore that class; full JSON values restore every
// data-zits-* dimension. Everything is wrapped in try/catch so broken storage never
// blocks the page from rendering.
(function () {
  try {
    var KEY = 'zits-theme';
    var DIMS = ['mode', 'base', 'primary', 'radius', 'font', 'style'];
    var DEFAULTS = {
      mode: 'system',
      base: 'neutral',
      primary: 'ink',
      radius: 'md',
      font: 'system',
      style: 'standard',
    };

    var raw = null;
    try {
      raw = localStorage.getItem(KEY);
    } catch (e) {
      return; // storage unavailable; leave the host default alone
    }
    if (raw == null) return; // no stored theme; respect the hardcoded markup

    function applyModeOnly(mode) {
      if (mode === 'dark') document.documentElement.classList.add('dark');
      else if (mode === 'light') document.documentElement.classList.remove('dark');
    }

    var state = null;
    var trimmed = ('' + raw).trim();
    if (trimmed === 'dark' || trimmed === 'light') {
      applyModeOnly(trimmed);
      return;
    } else {
      try {
        var parsed = JSON.parse(trimmed);
        if (parsed === 'dark' || parsed === 'light') {
          applyModeOnly(parsed);
          return;
        } else if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) state = parsed;
      } catch (e) {
        return; // corrupt JSON; do nothing
      }
    }
    if (!state) return;

    var s = {};
    for (var k in DEFAULTS) s[k] = DEFAULTS[k];
    for (var k2 in state) if (state[k2] != null) s[k2] = state[k2];

    var dark =
      s.mode === 'dark'
        ? true
        : s.mode === 'light'
          ? false
          : !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);

    var root = document.documentElement;
    if (dark) root.classList.add('dark');
    else root.classList.remove('dark');
    for (var i = 0; i < DIMS.length; i++) {
      root.setAttribute('data-zits-' + DIMS[i], s[DIMS[i]]);
    }
  } catch (e) {
    /* never let theme restore break the page */
  }
})();
