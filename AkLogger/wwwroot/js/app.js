// Theme switch, OS dark-mode watch, menu placement, scroll-area fades, and keyboard handling (Esc, arrows in a menu).
window.ak = {
  setTheme(theme) {
    const root = document.documentElement;
    if (theme === "light" || theme === "dark") root.dataset.theme = theme;
    else delete root.dataset.theme;
    try { localStorage.setItem("ak.theme", theme); } catch (e) { }   // js/theme.js reads it before the next start's first frame
  },

  // Returns whether the OS is in dark mode now, and tells .NET (MainLayout.SystemDarkChanged) when it changes.
  watchDark(dotnet) {
    const q = matchMedia("(prefers-color-scheme: dark)");
    q.addEventListener("change", (e) => dotnet.invokeMethodAsync("SystemDarkChanged", e.matches));
    return q.matches;
  },

  // Puts a menu under its activator (flipping above / left when it would leave the window).
  // position: fixed, so scrolling containers can't clip it.
  place(anchor, pop) {
    if (!anchor || !pop) return;
    const r = anchor.getBoundingClientRect();
    const w = pop.offsetWidth, h = pop.offsetHeight;
    let x = r.left, y = r.bottom + 6;
    if (x + w > innerWidth - 8) x = Math.max(8, r.right - w);
    if (y + h > innerHeight - 8) y = Math.max(8, r.top - h - 6);
    pop.style.left = `${x}px`;
    pop.style.top = `${y}px`;
    pop.classList.add("placed");
    (pop.querySelector(".menu-filter") ?? pop.querySelector(".menu-item.cur, .menu-item"))?.focus();   // pickers: type to filter
  },
};

document.addEventListener("keydown", (e) => {
  // Arrow keys move between items of an open menu.
  const pop = document.querySelector(".menu-pop");
  if (pop && (e.key === "ArrowDown" || e.key === "ArrowUp")) {
    const items = [...pop.querySelectorAll(".menu-item")], i = items.indexOf(document.activeElement);
    items[(i + (e.key === "ArrowDown" ? 1 : -1) + items.length) % items.length]?.focus();
    e.preventDefault();
    return;
  }
  if (e.key !== "Escape") return;
  if (pop) { document.querySelector(".menu-backdrop")?.click(); return; }                     // close the open menu
  const cancel = document.querySelector(".dialog .dialog-cancel");
  if (cancel) { cancel.click(); return; }                                                      // cancel the open dialog
  if (e.target.closest?.("input, textarea, [contenteditable]")) { e.target.blur(); return; }  // Esc first leaves the field
});

// Edge fades on scroll areas (.scx): on only while content continues past the right / bottom edge.
// Checked on scroll, resize, and whenever Blazor changes the DOM (one check per frame at most).
let _fadeQueued = false;
function _updateFades() {
  _fadeQueued = false;
  for (const box of document.querySelectorAll(".scx")) {
    const s = box.firstElementChild;
    box.classList.toggle("more-right", !!s && s.scrollLeft + s.clientWidth < s.scrollWidth - 1);
    box.classList.toggle("more-bottom", !!s && s.scrollTop + s.clientHeight < s.scrollHeight - 1);
  }
}
function _queueFades() { if (!_fadeQueued) { _fadeQueued = true; requestAnimationFrame(_updateFades); } }
document.addEventListener("scroll", _queueFades, { capture: true, passive: true });   // passive: never holds up the scrolling
addEventListener("resize", _queueFades);
new MutationObserver(_queueFades).observe(document.documentElement, { childList: true, subtree: true });

// The framework's error bar (index.html): Dismiss hides it. (No inline script: the CSP allows 'self' only.)
document.addEventListener("click", (ev) => { if (ev.target.closest && ev.target.closest("#blazor-error-ui .dismiss")) document.getElementById("blazor-error-ui").style.display = "none"; });
