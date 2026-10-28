(() => {
  const $ = (id) => document.getElementById(id);
  const themeToggle = $("themeToggle");
  let savedTheme;
  try {
    savedTheme = localStorage.getItem("session-lab-theme");
  } catch {
    // The browser may block local storage; the toggle still works for this page.
  }
  function setTheme(theme) {
    document.documentElement.dataset.theme = theme;
    if (!themeToggle) return;
    const dark = theme === "dark";
    themeToggle.textContent = dark ? "Switch to light mode" : "Switch to dark mode";
    themeToggle.setAttribute("aria-pressed", String(dark));
  }
  setTheme(savedTheme === "dark" || savedTheme === "light" ? savedTheme : "light");
  themeToggle?.addEventListener("click", () => {
    const theme = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
    setTheme(theme);
    try {
      localStorage.setItem("session-lab-theme", theme);
    } catch {}
  });
  function highlightSection() {
    const selected = location.pathname === "/guide"
      ? "/guide" + (location.hash || "#learning")
      : location.pathname === "/architecture" ? "/architecture" : "/#session-lab";
    for (const link of document.querySelectorAll(".student-nav a")) {
      if (link.getAttribute("href") === selected) link.setAttribute("aria-current", "location");
      else link.removeAttribute("aria-current");
    }
  }
  highlightSection();
  window.addEventListener("hashchange", highlightSection);
})();

// Small browser helpers called from Home.razor through IJSRuntime.
window.sessionLab = {
  getLastSession() {
    try {
      return sessionStorage.getItem("session-lab-last-session");
    } catch {
      return null;
    }
  },
  setLastSession(id) {
    try {
      sessionStorage.setItem("session-lab-last-session", id);
    } catch {}
  },
  scrollToBottom(element) {
    if (element) element.scrollTop = element.scrollHeight;
  },
};
