# Penetration test report

| | |
|---|---|
| **Date** | 24 October 2026 |
| **Environment** | Staging (the `-dev-` App Service and the staging Static Web App) |
| **Tool** | OWASP ZAP (`zaproxy/zap-stable`, Docker) |
| **Tester** | Dillon Rinkwest |

## 1. Scope

| Target | URL |
|---|---|
| API (ASP.NET Core) | `https://xbcad-api-dev-bwdwgmdbesd3ctft.southafricanorth-01.azurewebsites.net` |
| Web client (Blazor WASM) | `https://brave-dune-0169ab50f.5.azurestaticapps.net` |

Production was **not** tested. Testing used a seeded staging administrator account and fake data only. No denial-of-service or load testing was performed, in line with Microsoft's penetration testing rules of engagement for Azure.

## 2. Method

| # | Test | Type | Evidence |
|---|---|---|---|
| 1 | API scan, no credentials. The 74 endpoints were imported from the OpenAPI spec exported from a local run. | Active | `api-scan.html` |
| 2 | API scan with an admin JWT sent as an `Authorization: Bearer` header | Active, authenticated | `api-scan-auth.html` |
| 3 | Web client baseline (spider plus passive rules) | Passive | `web-baseline.html`, `zap.yaml` |
| 4 | Account lockout: a failed login on staging returned "5 failed attempts lock an account for 15 minutes" | Manual | Screenshot (add to POE) |

The OpenAPI spec is switched off on staging and production (`OpenApi:Enabled`, [Program.cs](../QULOOBUL%20_MOE'MIENEEN_Backend/QULOOBUL%20_MOE'MIENEEN_Backend/Program.cs)), so it was exported from a local Development run and the scan was pointed at staging with ZAP's host override.

## 3. Results

| Scan | High | Medium | Low | Informational |
|---|---|---|---|---|
| API, unauthenticated | 0 | 0 | 3 | n/a (112 rules passed) |
| API, authenticated (admin) | 0 | 0 | 3 | 21 |
| Web client baseline | 0 | 1 | 0 | 2 |

No High findings. The scanner's SQL injection, XSS, path traversal, command injection, XXE and SSRF rules all passed against the endpoints reached.

## 4. Findings

| ID | Finding | Where | Risk | Verdict | Status |
|---|---|---|---|---|---|
| F1 | `Strict-Transport-Security` header not set | API | Low | **Valid** | **Fixed and verified**: absent in the first scan, gone from the after scan (`api-scan-auth-after.html`) |
| F2 | Server error (503) and unexpected content type on `/latest/meta-data/`, `/computeMetadata/v1/`, `/opc/v1/instance/`, `/opc/v2/instance/`, `/metadata/instance` | API | Low | False positive | No action |
| F3 | CSP `style-src 'unsafe-inline'` | Web client | Medium | Accepted risk | Documented |
| F4 | Re-examine Cache-control directives | API, web client | Informational | **Valid** (API) | **Fixed** for the API |
| F5 | Suspicious comments in `dictation.js` | Web client | Informational | False positive | No action |
| F6 | `Cross-Origin-Resource-Policy` header missing (`/health`, `/api/Auth/logout`, `/api/Auth/forgot-password`) | API | Low | **Valid** | **Fixed**, pending redeploy and rescan |

### F1: HSTS not set (fixed)

Azure redirects HTTP to HTTPS, but the API did not tell browsers to always use HTTPS. Fix: `app.UseHsts()` outside Development, so staging and production send the header. Development is excluded so localhost is not pinned to HTTPS.

### F2: Cloud metadata probes (false positive)

ZAP's "Cloud Metadata Potentially Exposed" rule requests well-known metadata paths (the addresses cloud VMs use for instance credentials). Azure's front end rejected them with `503 IP Literal Host Name`, and none of those routes exist in the application. The rejection is the safe outcome: the API cannot be tricked into returning cloud metadata. The 503 and content-type alerts are noise from those rejected probes.

### F3: `style-src 'unsafe-inline'` (accepted risk)

The web client's CSP is built in [`.github/scripts/web-config.sh`](../.github/scripts/web-config.sh). MudBlazor writes inline `style` attributes at runtime, so removing `'unsafe-inline'` from `style-src` would break the UI.

Mitigation: script execution is not affected. `script-src` allows only `'self'`, `'wasm-unsafe-eval'` and the SHA-256 hash of Blazor's import map. There is no `'unsafe-inline'` for scripts. Remaining controls are `object-src 'none'`, `base-uri 'self'`, `form-action 'self'` and `frame-ancestors 'none'`. Inline styles can restyle a page but cannot run code, so the residual risk is low. Revisit if MudBlazor adds nonce-based styles.

### F4: Cache-Control (fixed for the API)

API responses carry authenticated JSON. Fix: `Cache-Control: no-store` is now set on every API response, so shared caches and proxies do not keep it.

### F5: Suspicious comments (accepted)

A single match in `/js/dictation.js`: ZAP's pattern `\bFROM\b` hit the word "from" in a source-citation comment (`// Reference: Microsoft, "Call .NET methods from JavaScript ..."`). It is a harmless reference, not a leaked secret or internal detail. The client is public by design, and the only config value it ships is the public API URL.

### F6: Cross-Origin-Resource-Policy missing (fixed)

Surfaced by the after scan once the earlier warnings cleared. Without the header, other sites can embed API responses in no-cors requests (a Spectre-style side-channel concern). Fix: `Cross-Origin-Resource-Policy: same-origin` on every API response. The web client lives on a different origin but calls the API with CORS `fetch`, which this header does not restrict, so the app should be unaffected. Confirm after deploy by signing in on the staging web app and checking the browser console for blocked requests.

### After-scan summary (`api-scan-auth-after.html`)

0 failures, 119 passes, 3 warning groups: the F2 metadata probes (two rules) and F6. HSTS (F1) no longer appears.

## 5. Controls observed working

- Login lockout after 5 failed attempts for 15 minutes (brute-force protection).
- Strict API headers: `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` and `X-Content-Type-Options: nosniff`.
- Rate limiting on the anonymous authentication routes.
- No server or framework version headers leaked (`X-Powered-By`, `X-AspNet-Version` rules passed).
- No cookie issues, since the API authenticates with bearer tokens.
- OpenAPI contract not exposed outside Development.

## 6. Limitations

- The authenticated scan used an **admin** token only. It does not prove a regular user is blocked from admin endpoints.
- Automated scanning cannot detect broken access control between users (changing IDs) or business-logic flaws.
- The admin token expires about an hour after login; the scan finished before expiry.
- Staging is not a perfect copy of production. Only passive, non-destructive checks should be run against production.

## 7. Outstanding work

| Item | Owner | Status |
|---|---|---|
| Deploy F1 and F4 to staging (merge to `develop`) and rerun the authenticated scan as `api-scan-auth-after.html` | | Open |
| Manual access-control tests: user A against user B's records; a normal user against admin endpoints | | Open |
| SignalR hub `/hubs/notifications` without a token | | Open |
| Repeat the authenticated scan with a non-admin token | | Open |
| Add the lockout screenshot to the POE | | Open |
