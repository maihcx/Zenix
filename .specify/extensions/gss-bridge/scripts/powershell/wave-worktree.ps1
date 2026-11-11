# wave-worktree.ps1 — Manages isolated git worktrees for parallel subagent wave execution.
[CmdletBinding()]
param(
    [ValidateSet("create", "merge", "cleanup", "list")]
    [string]$Action = "list",
    [string]$Name = "",
    [string]$Branch = "",
    [string]$Base = "HEAD",
    [string]$Target = "",
    [switch]$Force,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
. (Join-Path $ScriptDir "common-actor-resolution.ps1")

$repoRoot = Get-BridgeRepoRoot
$specifyDir = Join-Path $repoRoot ".specify"
$worktreeBaseDir = Join-Path $specifyDir "worktrees"

if (-not (Test-Path -LiteralPath $worktreeBaseDir)) {
    New-Item -ItemType Directory -Path $worktreeBaseDir -Force | Out-Null
}

function Invoke-GitCommand {
    param([string[]]$Arguments)
    $prev = $global:ErrorActionPreference
    $global:ErrorActionPreference = "Continue"
    try {
        $out = & git @Arguments 2>&1
        $code = $LASTEXITCODE
        return [pscustomobject]@{
            Output = ($out | Out-String).Trim()
            ExitCode = $code
        }
    } finally {
        $global:ErrorActionPreference = $prev
    }
}

function Get-GssWorktrees {
    param([string]$Root)
    $res = Invoke-GitCommand -Arguments @("worktree", "list", "--porcelain")
    if ($res.ExitCode -ne 0 -or -not $res.Output) { return @() }
    
    $items = @()
    $current = @{}
    
    foreach ($line in ($res.Output -split "`r?`n")) {
        $line = $line.Trim()
        if (-not $line) {
            if ($current.worktree) {
                $items += [pscustomobject]$current
                $current = @{}
            }
            continue
        }
        if ($line -match "^worktree (.+)$") {
            $current["worktree"] = $matches[1]
        } elseif ($line -match "^HEAD (.+)$") {
            $current["head"] = $matches[1]
        } elseif ($line -match "^branch (.+)$") {
            $current["branch"] = $matches[1]
        }
    }
    if ($current.worktree) {
        $items += [pscustomobject]$current
    }
    
    $filtered = @()
    foreach ($it in $items) {
        $wtNorm = $it.worktree.Replace("\", "/").ToLowerInvariant()
        if ($wtNorm -like "*/.specify/worktrees/*") {
            $subName = Split-Path -Leaf $it.worktree
            $filtered += [pscustomobject]@{
                name = $subName
                path = $it.worktree
                branch = if ($it.branch) { $it.branch.Replace("refs/heads/", "") } else { "" }
                head = $it.head
            }
        }
    }
    return $filtered
}

if ($Action -eq "list") {
    $wts = Get-GssWorktrees -Root $repoRoot
    if ($Json) {
        $wts | ConvertTo-Json -Compress
    } else {
        Write-Host "=================================================================" -ForegroundColor Cyan
        Write-Host "GSS-BRIDGE PARALLEL WAVE WORKTREES" -ForegroundColor Cyan
        Write-Host "================================================================="
        if ($wts.Count -eq 0) {
            Write-Host "No active worktrees in .specify/worktrees/" -ForegroundColor Gray
        } else {
            foreach ($w in $wts) {
                Write-Host "- Wave Worktree: $($w.name)" -ForegroundColor Green
                Write-Host "  Path:   $($w.path)"
                Write-Host "  Branch: $($w.branch)"
                Write-Host "  HEAD:   $($w.head)"
            }
        }
        Write-Host "================================================================="
    }
    exit 0
}

if (-not $Name) {
    Write-Error "Error: -Name is required for action '$Action'."
    exit 2
}

$sanitizedName = $Name -replace "[^a-zA-Z0-9_\-]", "-"
$wtPath = Join-Path $worktreeBaseDir $sanitizedName
$branchName = if ($Branch) { $Branch } else { "gss-worktree/$sanitizedName" }

if ($Action -eq "create") {
    if (Test-Path -LiteralPath $wtPath) {
        if ($Force) {
            Invoke-GitCommand -Arguments @("worktree", "remove", "--force", $wtPath) | Out-Null
            Invoke-GitCommand -Arguments @("branch", "-D", $branchName) | Out-Null
        } else {
            Write-Error "Worktree directory '$wtPath' already exists. Use -Force to overwrite."
            exit 1
        }
    }
    
    $bCheck = Invoke-GitCommand -Arguments @("branch", "--list", $branchName)
    if ($bCheck.Output -match [regex]::Escape($branchName)) {
        if ($Force) {
            Invoke-GitCommand -Arguments @("branch", "-D", $branchName) | Out-Null
        } else {
            Write-Error "Branch '$branchName' already exists. Use -Force to replace."
            exit 1
        }
    }
    
    $addRes = Invoke-GitCommand -Arguments @("worktree", "add", "-b", $branchName, $wtPath, $Base)
    if ($addRes.ExitCode -ne 0) {
        Write-Error "Failed to create git worktree at '$wtPath': $($addRes.Output)"
        exit 1
    }
    
    $result = [ordered]@{
        action = "create"
        name = $sanitizedName
        path = $wtPath
        branch = $branchName
        base = $Base
        status = "created"
    }
    
    if ($Json) {
        $result | ConvertTo-Json -Compress
    } else {
        Write-Host "[gss-bridge] Isolated worktree created for wave '$sanitizedName':" -ForegroundColor Green
        Write-Host "  Path:   $wtPath"
        Write-Host "  Branch: $branchName"
        Write-Host "  Base:   $Base"
    }
    exit 0
}

if ($Action -eq "merge") {
    $bCheck = Invoke-GitCommand -Arguments @("branch", "--list", $branchName)
    if (-not ($bCheck.Output -match [regex]::Escape($branchName))) {
        Write-Error "Branch '$branchName' does not exist to merge."
        exit 1
    }
    
    $mergeMsg = "Merge isolated wave worktree: $sanitizedName ($branchName)"
    $mRes = Invoke-GitCommand -Arguments @("merge", "--no-ff", $branchName, "-m", $mergeMsg)
    if ($mRes.ExitCode -ne 0) {
        Write-Error "Merge conflict or failure merging '$branchName': $($mRes.Output)"
        exit 1
    }
    
    $result = [ordered]@{
        action = "merge"
        name = $sanitizedName
        branch = $branchName
        status = "merged"
    }
    
    if ($Json) {
        $result | ConvertTo-Json -Compress
    } else {
        Write-Host "[gss-bridge] Successfully merged wave branch '$branchName'." -ForegroundColor Green
    }
    exit 0
}

if ($Action -eq "cleanup") {
    if (Test-Path -LiteralPath $wtPath) {
        Invoke-GitCommand -Arguments @("worktree", "remove", "--force", $wtPath) | Out-Null
    }
    Invoke-GitCommand -Arguments @("branch", "-D", $branchName) | Out-Null
    
    $result = [ordered]@{
        action = "cleanup"
        name = $sanitizedName
        branch = $branchName
        status = "cleaned"
    }
    
    if ($Json) {
        $result | ConvertTo-Json -Compress
    } else {
        Write-Host "[gss-bridge] Cleaned up worktree and branch for '$sanitizedName'." -ForegroundColor Green
    }
    exit 0
}
