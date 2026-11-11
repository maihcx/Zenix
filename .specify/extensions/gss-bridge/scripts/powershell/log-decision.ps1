# log-decision.ps1 — Cross-agent reasoning and architectural decision journal.
param(
    [string]$Decision = "",
    [string]$Rationale = "",
    [string]$Alternatives = "",
    [string]$Topic = "general",
    [string]$Feature = "",
    [string]$Actor = "",
    [string]$Query = "",
    [int]$Recent = 0,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
. (Join-Path $ScriptDir "common-actor-resolution.ps1")

$repoRoot = Get-BridgeRepoRoot
$specifyDir = Join-Path $repoRoot ".specify"
if (-not (Test-Path -LiteralPath $specifyDir)) {
    New-Item -ItemType Directory -Path $specifyDir -Force | Out-Null
}

$journalPath = Join-Path $specifyDir "agent-decisions.jsonl"
$resolvedActor = Resolve-BridgeActor -Argument $Actor -RepoRoot $repoRoot

if (-not [string]::IsNullOrWhiteSpace($Decision)) {
    $now = [DateTimeOffset]::UtcNow
    $rnd = Get-Random -Minimum 1000 -Maximum 9999
    $id = "dec-" + $now.ToUnixTimeSeconds() + "-" + $rnd
    $timestamp = $now.ToString("yyyy-MM-ddTHH:mm:ssZ")

    $record = [ordered]@{
        id = $id
        timestamp = $timestamp
        actor = $resolvedActor
        feature = $Feature
        topic = $Topic
        decision = $Decision
        rationale = $Rationale
        alternatives = $Alternatives
    }

    $jsonLine = $record | ConvertTo-Json -Compress
    Add-Content -LiteralPath $journalPath -Value $jsonLine -Encoding UTF8

    if ($Json) {
        $record | ConvertTo-Json -Compress
    } else {
        Write-Host "[gss-bridge] Decision logged: $id" -ForegroundColor Green
        Write-Host "  Actor:        $resolvedActor"
        Write-Host "  Topic:        $Topic"
        if ($Feature) { Write-Host "  Feature:      $Feature" }
        Write-Host "  Decision:     $Decision"
        if ($Rationale) { Write-Host "  Rationale:    $Rationale" }
        if ($Alternatives) { Write-Host "  Alternatives: $Alternatives" }
    }
    exit 0
}

# Query / List mode
$results = @()
if (Test-Path -LiteralPath $journalPath) {
    $lines = Get-Content -LiteralPath $journalPath -Encoding UTF8
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try {
            $obj = $line | ConvertFrom-Json
            $results += $obj
        } catch { }
    }
}

if ($Feature) {
    $results = @($results | Where-Object { $_.feature -eq $Feature })
}
if ($Topic -and $Topic -ne "general") {
    $results = @($results | Where-Object { $_.topic -eq $Topic })
}
if ($Query) {
    $q = $Query.ToLowerInvariant()
    $results = @($results | Where-Object {
        ($_.decision -and $_.decision.ToLowerInvariant().Contains($q)) -or
        ($_.rationale -and $_.rationale.ToLowerInvariant().Contains($q)) -or
        ($_.alternatives -and $_.alternatives.ToLowerInvariant().Contains($q)) -or
        ($_.topic -and $_.topic.ToLowerInvariant().Contains($q)) -or
        ($_.feature -and $_.feature.ToLowerInvariant().Contains($q)) -or
        ($_.actor -and $_.actor.ToLowerInvariant().Contains($q))
    })
}

if ($Recent -gt 0 -and $results.Count -gt $Recent) {
    $startIndex = $results.Count - $Recent
    $results = @($results[$startIndex..($results.Count - 1)])
}

if ($Json) {
    $results | ConvertTo-Json -Compress
} else {
    Write-Host "[gss-bridge] Retrieved $($results.Count) decision record(s):" -ForegroundColor Cyan
    foreach ($r in $results) {
        $fStr = if ($r.feature) { " [$($r.feature)]" } else { "" }
        Write-Host "- [$($r.timestamp)] ($($r.actor) | $($r.topic))$fStr" -ForegroundColor Yellow
        Write-Host "  Decision: $($r.decision)"
        if ($r.rationale) { Write-Host "  Rationale: $($r.rationale)" }
        if ($r.alternatives) { Write-Host "  Alternatives: $($r.alternatives)" }
    }
}
