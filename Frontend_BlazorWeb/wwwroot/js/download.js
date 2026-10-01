// A file, not an inline <script>: the deployed CSP only allows the import map inline.
window.downloadFile = (name, href) => Object.assign(document.createElement("a"), { href, download: name }).click();
