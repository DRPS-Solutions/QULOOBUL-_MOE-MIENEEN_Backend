// A file, not an inline <script>: the deployed CSP only allows the import map inline.
// Reference: MDN Web Docs (2026) <a>: The Anchor element - download attribute. Available at: https://developer.mozilla.org/en-US/docs/Web/HTML/Element/a#download (Accessed: 4 October 2026).
window.downloadFile = (name, href) => Object.assign(document.createElement("a"), { href, download: name }).click();
