param(
    [string]$QuestsPath = "$PSScriptRoot\..\SwixyQuestBook\Data\quests.json",
    [string]$AssetsPath = "E:\Vintagestory\assets"
)

$json = Get-Content -Raw -Encoding UTF8 $QuestsPath | ConvertFrom-Json
$refs = @()
foreach ($cat in $json.categories) {
    $title = $cat.headerTitle
    foreach ($node in $cat.nodes) {
        foreach ($field in @('requiredItems', 'rewardItems')) {
            foreach ($item in $node.$field) {
                if ($item.collectibleCode) {
                    $refs += [pscustomobject]@{
                        Category = $title
                        NodeId   = $node.id
                        Field    = $field
                        Code     = $item.collectibleCode
                        Count    = $item.count
                        Desc     = $node.description
                    }
                }
            }
        }
    }
}

$searchRoots = @(
    Join-Path $AssetsPath 'survival\itemtypes'
    Join-Path $AssetsPath 'survival\blocktypes'
    Join-Path $AssetsPath 'game\itemtypes'
    Join-Path $AssetsPath 'game\blocktypes'
) | Where-Object { Test-Path $_ }

function Test-CodeExists([string]$fullCode) {
    if ([string]::IsNullOrWhiteSpace($fullCode)) { return $true }
    if ($fullCode -notmatch '^([^:]+):(.+)$') { return $false }

    $path = $Matches[2]
    if ($path.Contains('*')) {
        $needle = $path.Replace('*', '')
        foreach ($root in $searchRoots) {
            $hit = Get-ChildItem -Path $root -Recurse -Filter *.json -File |
                Select-String -Pattern ([regex]::Escape($needle)) -SimpleMatch -List -ErrorAction SilentlyContinue
            if ($hit) { return $true }
        }
        return $false
    }

    foreach ($root in $searchRoots) {
        $hit = Get-ChildItem -Path $root -Recurse -Filter *.json -File |
            Select-String -Pattern ([regex]::Escape($path)) -SimpleMatch -List -ErrorAction SilentlyContinue
        if ($hit) { return $true }
    }

    return $false
}

$checked = @{}
$bad = @()
foreach ($r in $refs) {
    $code = $r.Code
    if (-not $checked.ContainsKey($code)) {
        $checked[$code] = Test-CodeExists $code
    }
    if (-not $checked[$code]) {
        $bad += $r
    }
}

Write-Host "Checked $($refs.Count) references, $($checked.Count) unique codes"
Write-Host "Invalid usages: $($bad.Count)"
if ($bad.Count -gt 0) {
    Write-Host ""
    Write-Host "=== Unique invalid codes ==="
    $bad | Select-Object -ExpandProperty Code -Unique | Sort-Object
    Write-Host ""
    Write-Host "=== Invalid usages by quest ==="
    $bad |
        Sort-Object Code, Category, NodeId |
        ForEach-Object {
            "{0} | node {1} | {2} | {3} x{4} | {5}" -f $_.Code, $_.NodeId, $_.Field, $_.Category, $_.Count, $_.Desc
        }
}