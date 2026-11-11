# bug-assess.ps1 — Diagnose and triage bugs using Graphify knowledge graph context.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Slug,

    [string]$Symptom = "",
    [string]$StackFile = "",
    [string]$SuspectNode = "",
    [string]$Actor = "",
    [switch]$GenerateRepro,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir 'common-actor-resolution.ps1')
. (Join-Path $ScriptDir 'bridge-state.ps1')

$RepoRoot = Get-BridgeRepoRoot
$SpecifyDir = Join-Path $RepoRoot ".specify"

if (-not (Test-Path -LiteralPath $SpecifyDir)) {
    Write-Error "[gss-bridge] Not inside a Spec-Kit repository (.specify missing)."
    exit 2
}

$ResolvedActor = Resolve-BridgeActor $Actor
$GraphPath = Ensure-GraphifyIndex -RepoRoot $RepoRoot

# Collect symptom text
$symptomText = $Symptom
if ($StackFile -and (Test-Path -LiteralPath $StackFile)) {
    $fileText = Get-Content -LiteralPath $StackFile -Raw
    if ($symptomText) {
        $symptomText = "$symptomText`r`n`r`nStack Trace:`r`n$fileText"
    } else {
        $symptomText = $fileText
    }
}
if ([string]::IsNullOrWhiteSpace($symptomText)) {
    $symptomText = "(No detailed symptom text provided. Triage based on target symbols.)"
}

# Search for suspect nodes in Graphify
$targetNode = $SuspectNode
$blastReport = $null
$mermaidBlock = ""

if (Test-Path -LiteralPath $GraphPath) {
    $graphData = Get-Content -LiteralPath $GraphPath -Raw | ConvertFrom-Json
    $candidateNodes = @()

    if ($targetNode) {
        $candidateNodes += $targetNode
    } else {
        # Scan symptom text against node labels and IDs
        foreach ($n in $graphData.nodes) {
            $nid = [string]$n.id
            $lbl = [string]$n.label
            if ($lbl -and $symptomText.Contains($lbl)) {
                $candidateNodes += $lbl
            } elseif ($nid -and $symptomText.Contains($nid)) {
                $candidateNodes += $nid
            }
        }
    }

    if ($candidateNodes.Count -gt 0) {
        $targetNode = $candidateNodes[0]
        $blastScript = Join-Path $ScriptDir "graphify-blast-radius.ps1"
        try {
            $blastJson = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $blastScript -Node $targetNode -Json | Out-String
            if ($LASTEXITCODE -eq 0) {
                $blastReport = $blastJson | ConvertFrom-Json
                $mermaidBlock = $blastReport.mermaid
            }
        } catch {
            Write-Verbose "Blast radius extraction failed: $($_.Exception.Message)"
        }
    }
}

$bugDir = Join-Path $SpecifyDir "bugs\$Slug"
New-Item -ItemType Directory -Force -Path $bugDir | Out-Null
$assessmentPath = Join-Path $bugDir "assessment.md"

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# Bug Assessment: $Slug")
$lines.Add("")
$lines.Add("> **Status**: ASSESSED")
$lines.Add("> **Triage Agent**: $ResolvedActor")
$lines.Add("> **Assessed At**: $(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ')")
$lines.Add("")
$lines.Add("## 1. Reported Symptom & Context")
$lines.Add('```text')
$lines.Add($symptomText.Trim())
$lines.Add('```')
$lines.Add("")
$bt = [char]96
$lines.Add('## 2. Graphify Root Cause Candidates & Architecture Topology')
if ($targetNode) {
    $lines.Add("- **Primary Suspect Component**: $bt$targetNode$bt")
} else {
    $lines.Add('- **Primary Suspect Component**: (None detected automatically from graph; manual triage required)')
}

if ($blastReport) {
    $lines.Add("- **Source Location**: $bt$($blastReport.source_file)$bt : $($blastReport.source_location)")
    $cStr = if ($blastReport.callers_1hop) { ($blastReport.callers_1hop -join ', ') } else { '(none)' }
    $lines.Add("- **Direct Callers (1-hop)**: $cStr")
    $ceStr = if ($blastReport.callees_1hop) { ($blastReport.callees_1hop -join ', ') } else { '(none)' }
    $lines.Add("- **Direct Callees (1-hop)**: $ceStr")
    $lines.Add("- **Transitive Impact (2-hop)**: $($blastReport.transitive_2hop_count) node(s)")
    $lines.Add("- **Blast Radius Risk Assessment**: **$($blastReport.risk_level)**")
    $lines.Add('')
    $lines.Add('### Architecture Call Flow (Graphify)')
    $lines.Add($mermaidBlock)
} else {
    $lines.Add('- *Graphify context not available or node not found in graphify-out/graph.json.*')
    $lines.Add('- Run `/graphify` to index the workspace if not yet indexed.')
}

$lines.Add('')
$lines.Add('## 3. Mandatory Superpowers TDD Preconditions')
$lines.Add('- [ ] **TDD Failing Test Required**: You MUST write a minimal reproducing test case in the test suite that FAILS (Red) reproducing the symptom before touching any implementation code.')
$lines.Add('- [ ] **Boundary Scoping**: Code modifications MUST be restricted to the immediate suspect component and directly coupled callers/callees identified above.')
$lines.Add('')

if ($GenerateRepro) {
    $synthScript = Join-Path $ScriptDir "synthesize-repro.ps1"
    if (Test-Path -LiteralPath $synthScript) {
        $reproRaw = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $synthScript -Slug $Slug -SuspectNode $targetNode -Symptom $symptomText -Json | Out-String
        $reproObj = $reproRaw | ConvertFrom-Json
        if ($reproObj.repro_test_file) {
            $lines.Add("- [ ] **Auto-Synthesized Repro**: Run $bt$($reproObj.repro_test_file)$bt to verify initial failure (RED).")
        }
    }
}

$lines.Add('## 4. Next Step')
$lines.Add("Execute fix with Superpowers discipline: $bt/speckit-gss-bridge-bug-fix slug=$Slug$bt")

$assessmentContent = ($lines -join [Environment]::NewLine)

[System.IO.File]::WriteAllText($assessmentPath, $assessmentContent, [System.Text.Encoding]::UTF8)

# Update GSS Handoff State
$updateScript = Join-Path $ScriptDir "update-handoff.ps1"
if (Test-Path -LiteralPath $updateScript) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $updateScript `
        -Status "ready" `
        -FeatureDirectory ".specify/bugs/$Slug" `
        -ArtifactOwner $ResolvedActor `
        -Actor $ResolvedActor | Out-Null
}

if ($Json) {
    @{
        status = "assessed"
        slug = $Slug
        assessment_file = ".specify/bugs/$Slug/assessment.md"
        suspect_node = $targetNode
        blast_risk = if ($blastReport) { $blastReport.risk_level } else { "UNKNOWN" }
    } | ConvertTo-Json
} else {
    Write-Output "[gss-bridge] Bug assessment created for '$Slug'."
    Write-Output "  Assessment: .specify/bugs/$Slug/assessment.md"
    Write-Output "  Suspect:    $(if ($targetNode) { $targetNode } else { '(manual identification needed)' })"
    Write-Output "  Blast Risk: $(if ($blastReport) { $blastReport.risk_level } else { 'UNKNOWN' })"
    Write-Output "  Next step:  Run /speckit-gss-bridge-bug-fix slug=$Slug"
}
exit 0
