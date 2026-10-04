// A file, not an inline <script>: the deployed CSP only allows the import map inline.
// Reference: MDN Web Docs, "<a>: The Anchor element - download attribute." Accessed: Oct. 04, 2026. [Online]. Available: https://developer.mozilla.org/en-US/docs/Web/HTML/Element/a#download
window.downloadFile = (name, href) => Object.assign(document.createElement("a"), { href, download: name }).click();
