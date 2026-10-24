# Manual access-control checks against the STAGING API (never point this at production).
# Credentials come from environment variables so they never touch the repo or shell history:
#   $env:ADMIN_EMAIL, $env:ADMIN_PASSWORD, $env:USER_EMAIL, $env:USER_PASSWORD
# Run:  powershell -ExecutionPolicy Bypass -File scripts\access-control-test.ps1 | Tee-Object access-control-results.txt
# Every request is a GET, or a POST that fails closed (empty body / non-existent id): nothing is changed.
param([string]$Api = "https://xbcad-api-dev-bwdwgmdbesd3ctft.southafricanorth-01.azurewebsites.net")

foreach ($v in "ADMIN_EMAIL","ADMIN_PASSWORD","USER_EMAIL","USER_PASSWORD") {
    if (-not (Get-Item "env:$v" -ErrorAction SilentlyContinue)) { throw "Set `$env:$v first." }
}
$results = @()

function Call($method, $path, $token) {
    $a = @("-s","-o","NUL","-w","%{http_code}","-X",$method,"$Api$path","-H","Content-Type: application/json")
    if ($token) { $a += @("-H","Authorization: Bearer $token") }
    if ($method -eq "POST") { $a += @("-d","{}") }
    [int](& curl.exe @a)
}
function Login($email, $password) {
    $f = New-TemporaryFile
    [IO.File]::WriteAllText($f, (@{ email = $email; password = $password } | ConvertTo-Json -Compress))
    $r = & curl.exe -s -X POST "$Api/api/Auth/login" -H "Content-Type: application/json" -d "@$f" | ConvertFrom-Json
    Remove-Item $f
    if (-not $r.token) { throw "Login failed for $email ($($r.message)). Stop here: repeated attempts trigger the lockout." }
    $r
}
function Check($name, $actual, $expected) {
    $ok = $expected -contains $actual
    $script:results += [pscustomobject]@{ Test = $name; Expected = ($expected -join "/"); Got = $actual; Result = $(if ($ok) { "PASS" } else { "FAIL" }) }
}

$admin = Login $env:ADMIN_EMAIL $env:ADMIN_PASSWORD
$user  = Login $env:USER_EMAIL  $env:USER_PASSWORD
"Admin role: $($admin.Role) | Test user role: $($user.Role)"
if ($user.Role -eq $admin.Role) { Write-Warning "Both accounts have the same role; the privilege tests mean nothing." }

# 1. No token: every protected route must refuse
foreach ($p in "/api/Users","/api/Users/me","/api/Tasks","/api/Projects","/api/Files","/api/Notifications","/api/Users/overview") {
    Check "no token  GET $p" (Call GET $p $null) @(401)
}
Check "no token  SignalR negotiate" (Call POST "/hubs/notifications/negotiate?negotiateVersion=1" $null) @(401)
Check "tampered token  GET /api/Users/me" (Call GET "/api/Users/me" ($user.Token.Substring(0, $user.Token.Length - 4) + "AAAA")) @(401)

# 2. Normal user against admin-only routes (policies ManageUsers / GenerateReports).
#    Authorization runs before the action, and the ids do not exist / bodies are empty, so a leak shows as 400/404/200, not damage.
$adminOnly = @(
    @("GET","/api/Users/overview"), @("GET","/api/Users/1/audit"),
    @("POST","/api/Reports"), @("POST","/api/Reports/export"),
    @("POST","/api/Notifications/send"),
    @("POST","/api/Users/999999/deactivate"), @("POST","/api/Users/999999/reactivate"),
    @("POST","/api/Users/999999/reset-password"), @("POST","/api/Users/999999/anonymise"),
    @("DELETE","/api/Users/999999"), @("POST","/api/Users")
)
foreach ($e in $adminOnly) {
    Check "user   $($e[0]) $($e[1])" (Call $e[0] $e[1] $user.Token) @(403)
    Check "admin  $($e[0]) $($e[1]) (not blocked)" (Call $e[0] $e[1] $admin.Token) @(200,201,204,400,404,422)
}

# 3. Cross-user data (IDOR): the test user must not read projects/tasks that are not in their own list
$headers = @{ Authorization = "Bearer $($user.Token)" }
$mine = @((Invoke-RestMethod "$Api/api/Projects" -Headers $headers) | ForEach-Object { $_.projectId })
"Test user is a member of projects: $($mine -join ', ')"
foreach ($id in 1..40) {
    if ($mine -contains $id) { continue }
    $code = Call GET "/api/Projects/$id" $user.Token
    Check "user   GET /api/Projects/$id (not a member)" $code @(403,404)
    # The board is filtered, not refused (TaskService.BoardAsync): a 200 is fine as long as every
    # card on it is one the user may open anyway (their own or assigned). Each card is re-checked
    # through GET /api/Tasks/{id}, which runs the separate EnsureTaskAsync guard.
    $code = Call GET "/api/projects/$id/board" $user.Token
    if ($code -ne 200) { Check "user   GET /api/projects/$id/board (not a member)" $code @(403,404); continue }
    $cards = @(Invoke-RestMethod "$Api/api/projects/$id/board" -Headers $headers)
    Check "user   GET /api/projects/$id/board (not a member, $($cards.Count) card(s) shown)" 200 @(200)
    foreach ($c in $cards) {
        Check "user   board $id card $($c.taskId) is openable" (Call GET "/api/Tasks/$($c.taskId)" $user.Token) @(200)
    }
}

$results | Format-Table -AutoSize | Out-String -Width 200
$fail = @($results | Where-Object Result -eq "FAIL")
"Total: $($results.Count)   Passed: $($results.Count - $fail.Count)   Failed: $($fail.Count)"
if ($fail) { "FAILURES:"; $fail | Format-Table -AutoSize | Out-String -Width 200 }
