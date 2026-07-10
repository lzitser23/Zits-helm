// Zits theming runtime: the browser side of ZitsThemeService.
//
// C# owns the theme state (the ZitsTheme record) and the persistence policy; this
// module is the thin DOM + localStorage layer Blazor can't reach synchronously.
// It writes the six data-zits-* dimension attributes plus the `.dark` class onto
// <html>, mirrors the selection to localStorage as one JSON blob, and reports OS
// light/dark changes back to .NET. The pre-paint restore lives in the sibling
// classic script zits-theme-init.js, so the first paint already carries the theme.

const STORAGE_KEY = 'zits-theme';
const DIMENSIONS = ['mode', 'base', 'primary', 'radius', 'font', 'style'];
const DEFAULTS = {
  mode: 'system',
  base: 'neutral',
  primary: 'ink',
  radius: 'md',
  font: 'system',
  style: 'standard',
};
const MEDIA = '(prefers-color-scheme: dark)';

// Whether the `.dark` class should be present for a state: explicit 'dark'/'light'
// win; 'system' (or anything unknown) follows the OS preference at apply time.
function resolveDark(state) {
  if (state && state.mode === 'dark') return true;
  if (state && state.mode === 'light') return false;
  return isSystemDark();
}

/**
 * Apply a theme state to the document: toggle `.dark`, write the six data-zits-*
 * attributes onto <html>, and persist the selection as JSON to localStorage.
 * @param {{mode:string,base:string,primary:string,radius:string,font:string,style:string}} state
 */
export function applyTheme(state) {
  const s = Object.assign({}, DEFAULTS, state || {});
  const root = document.documentElement;
  root.classList.toggle('dark', resolveDark(s));
  for (const dim of DIMENSIONS) {
    root.setAttribute(`data-zits-${dim}`, s[dim]);
  }
  try {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        mode: s.mode,
        base: s.base,
        primary: s.primary,
        radius: s.radius,
        font: s.font,
        style: s.style,
      })
    );
  } catch {
    /* storage unavailable (private mode / disabled); the DOM is still themed */
  }
}

/**
 * Read the persisted theme state, merged over the defaults. Returns null when the
 * key is absent or unreadable. A legacy bare 'dark'/'light' string (the pre-engine
 * dark-mode toggle) migrates to a full state with that mode over the defaults;
 * corrupt JSON is tolerated by returning null.
 * @returns {{mode:string,base:string,primary:string,radius:string,font:string,style:string}|null}
 */
export function readTheme() {
  let raw;
  try {
    raw = localStorage.getItem(STORAGE_KEY);
  } catch {
    return null; // storage unavailable
  }
  if (raw == null) return null;

  const trimmed = raw.trim();
  // Legacy: the pre-engine toggle stored the bare word 'dark' or 'light'.
  if (trimmed === 'dark' || trimmed === 'light') {
    return Object.assign({}, DEFAULTS, { mode: trimmed });
  }

  let parsed;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    return null; // corrupt JSON; the caller falls back to Default
  }
  // A quoted legacy string ('"dark"') parses to a primitive; migrate it too.
  if (parsed === 'dark' || parsed === 'light') {
    return Object.assign({}, DEFAULTS, { mode: parsed });
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null;
  return Object.assign({}, DEFAULTS, parsed);
}

/**
 * Remove the persisted selection and all data-zits-* attributes. Pure removal:
 * the service follows up with syncModeClass so the `.dark` class immediately
 * reflects the reset (System) state instead of the previous selection.
 */
export function clearTheme() {
  const root = document.documentElement;
  for (const dim of DIMENSIONS) {
    root.removeAttribute(`data-zits-${dim}`);
  }
  try {
    localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* storage unavailable; nothing to remove */
  }
}

/**
 * Toggle the `.dark` class to match a mode ('light' | 'dark' | 'system'), without
 * touching the data-zits-* attributes or storage. Used after clearTheme so a reset
 * to System recomputes the class from prefers-color-scheme right away.
 * @param {string} mode
 */
export function syncModeClass(mode) {
  document.documentElement.classList.toggle('dark', resolveDark({ mode }));
}

/** @returns {boolean} whether the OS currently prefers a dark color scheme. */
export function isSystemDark() {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia(MEDIA).matches
  );
}

/**
 * Watch OS light/dark changes and forward them to .NET as OnSystemThemeChanged(bool).
 * Returns a handle; C# keeps a reference and calls destroy() on teardown.
 * @param {any} dotNetRef a DotNetObjectReference exposing [JSInvokable] OnSystemThemeChanged
 * @returns {{destroy: () => void}}
 */
export function watchSystem(dotNetRef) {
  const mql = window.matchMedia(MEDIA);
  const onChange = (e) => {
    try {
      dotNetRef.invokeMethodAsync('OnSystemThemeChanged', e.matches);
    } catch {
      /* circuit gone */
    }
  };
  // Safari < 14 only supports the deprecated addListener signature.
  if (typeof mql.addEventListener === 'function') mql.addEventListener('change', onChange);
  else mql.addListener(onChange);

  return {
    destroy() {
      if (typeof mql.removeEventListener === 'function') mql.removeEventListener('change', onChange);
      else mql.removeListener(onChange);
    },
  };
}

/** Copy text to the clipboard when available. Falls back to a hidden textarea. */
export async function copyText(text) {
  if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
    await navigator.clipboard.writeText(text);
    return;
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', '');
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  textarea.style.pointerEvents = 'none';
  document.body.appendChild(textarea);
  textarea.select();
  try {
    document.execCommand('copy');
  } finally {
    textarea.remove();
  }
}
