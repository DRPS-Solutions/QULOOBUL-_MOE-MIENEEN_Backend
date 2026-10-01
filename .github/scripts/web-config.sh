#!/usr/bin/env bash
# Prepares the published Blazor client for Static Web Apps:
#  - points wwwroot/appsettings.json at the deployed API;
#  - writes staticwebapp.config.json with the SPA fallback and the Content-Security-Policy header.
# The CSP allows the one inline script (Blazor's generated import map) by hash, since its contents
# change every build; that is why this runs after publish instead of being a committed file.
# Usage: web-config.sh <published wwwroot> <api base url, e.g. https://app.azurewebsites.net>
set -euo pipefail
root="$1"
api="${2%/}"
ws="wss://${api#https://}"

printf '{\n  "ApiBaseUrl": "%s/"\n}\n' "$api" > "$root/appsettings.json"
rm -f "$root/appsettings.json.br" "$root/appsettings.json.gz"   # stale precompressed copies

# Browsers hash the script text after HTML newline normalisation (CRLF and CR become LF).
hash=$(perl -0ne 'if (/<script type="importmap">(.*?)<\/script>/s) { (my $m = $1) =~ s/\r\n?/\n/g; print $m }' "$root/index.html" \
    | openssl dgst -sha256 -binary | base64)
csp="default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'sha256-$hash'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; connect-src 'self' $api $ws https://*.blob.core.windows.net https://*.service.signalr.net wss://*.service.signalr.net; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'"

cat > "$root/staticwebapp.config.json" <<JSON
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/_framework/*", "/_content/*", "/css/*", "/lib/*", "/images/*", "*.{png,ico,json,js,css,wasm,dat,br,gz}"]
  },
  "globalHeaders": {
    "Content-Security-Policy": "$csp",
    "X-Content-Type-Options": "nosniff",
    "Referrer-Policy": "same-origin"
  }
}
JSON
echo "CSP: $csp"
