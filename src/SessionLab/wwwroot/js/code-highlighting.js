/* Local Prism integration: never evaluates source code or inserts unescaped source HTML. */
(() => {
  const Prism = globalThis.Prism;
  if (!Prism) return;
  Prism.languages.dotenv = {
    comment: { pattern: /^\s*#.*$/m, greedy: true },
    property: { pattern: /(^|\n)\s*(?:export\s+)?[A-Z_][A-Z_0-9]*(?=\s*=)/, lookbehind: true },
    string: { pattern: /([=]\s*)[^\r\n]+/, lookbehind: true },
    operator: /=/,
  };
  function language(code) {
    const declared = (code.className + " " + code.parentElement.className).match(/(?:language|lang)-([\w-]+)/);
    if (declared && Prism.languages[declared[1]]) return declared[1];
    const source = code.textContent.trim();
    if (/^(?:dotnet\s|cd\s|Copy-Item\s|\$[\w]+\s*=)/.test(source)) return "powershell";
    if (/^[\[{]/.test(source)) {
      try { JSON.parse(source); return "json"; } catch { /* May be a C# fragment. */ }
    }
    const label = code.closest("figure")?.querySelector("figcaption")?.textContent || "";
    if (/\.razor\b/.test(label)) return "cshtml";
    if (/\.js\b/.test(label)) return "javascript";
    return "csharp";
  }
  // The live session feed is rendered by Blazor; Prism must not rewrite nodes Blazor owns.
  for (const pre of document.querySelectorAll("pre:not(#session-lab pre)")) {
    const code = pre.querySelector(":scope > code");
    if (!code || !code.textContent.trim()) continue;
    code.className = code.className.replace(/\b(?:language|lang)-[\w-]+\b/g, "").trim() + " language-" + language(code);
    Prism.highlightElement(code);
    code.dataset.syntaxHighlighted = "true";
  }
})();
