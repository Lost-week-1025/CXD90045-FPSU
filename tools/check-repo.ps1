<#
.SYNOPSIS
    SOY-1F-64 仓库安全检查

.DESCRIPTION
    与 .githooks/pre-commit 规则一致。两种用法：

      1) 手动全仓扫描（不需要 git）
         .\check-repo.ps1                       # 扫脚本所在目录的上一级（仓库根）
         .\check-repo.ps1 -Root D:\repo

      2) 只扫已 staged 的文件（需要 git）
         .\check-repo.ps1 -Staged

    设计原则：只检测「危险形态」，脚本内不含任何真实密钥。

.NOTES
    退出码：0 = 通过；1 = 发现硬失败项
#>
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$Staged,
    [int]$HardMaxBytes = 1048576,
    [int]$WarnMaxBytes = 204800
)

$ErrorActionPreference = 'Stop'

if (-not $Root) {
    $Root = Split-Path $PSScriptRoot -Parent
    if (-not (Test-Path (Join-Path $Root 'README.md'))) { $Root = (Get-Location).Path }
}

$ForbiddenExt = @(
    'dat','fdat','bin','img','dump','raw','dd','so','elf','axf','ko','o','a',
    'dylib','dll','exe','pdb','uxc','uxb','xdb','ltt','ttf','otf','woff','woff2',
    'bmp','png','jpg','jpeg','wav','mp3',
    'zip','7z','rar','tar','tgz','gz','xz','bz2','zst','iso',
    'pem','p12','pfx','der','jks','keystore',
    'gpr','rep','ghidra'
)
$TextExt = @('','md','txt','cs','java','sh','ps1','json','yml','yaml','xml','svg','gitignore','gitattributes')

$fail = New-Object System.Collections.Generic.List[string]
$warn = New-Object System.Collections.Generic.List[string]

function Get-TargetFiles {
    if ($Staged) {
        $g = Get-Command git -ErrorAction SilentlyContinue
        if (-not $g) { throw "-Staged 需要 git，但系统未检测到 git" }
        $out = & git -C $Root diff --cached --name-only --diff-filter=ACMR 2>$null
        return @($out | Where-Object { $_ })
    }
    # 全树扫描：排除 .git
    $items = Get-ChildItem -Path $Root -Recurse -File -Force -ErrorAction SilentlyContinue
    return @($items | Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' })
}

function Read-TargetBytes($item) {
    if ($item -is [System.IO.FileInfo]) { return [System.IO.File]::ReadAllBytes($item.FullName) }
    $full = Join-Path $Root $item
    if (-not (Test-Path $full)) { return $null }
    return [System.IO.File]::ReadAllBytes($full)
}

function Get-RelPath($item) {
    if ($item -is [System.IO.FileInfo]) { return $item.FullName.Substring($Root.Length).TrimStart('\','/') }
    return $item
}

$targets = Get-TargetFiles
Write-Host ""
Write-Host "SOY-1F-64 仓库安全检查" -ForegroundColor Cyan
Write-Host ("  范围: " + $Root + $(if($Staged){" [仅 staged]"}else{" [全树]"}))
Write-Host ("  文件: " + $targets.Count + " 个")
Write-Host ""

foreach ($t in $targets) {
    $rel  = Get-RelPath $t
    $name = Split-Path $rel -Leaf
    $ext  = if ($name -match '\.([^.]+)$') { $Matches[1].ToLower() } else { '' }

    # 1) 扩展名
    if ($ext -and ($ForbiddenExt -contains $ext)) {
        $fail.Add("禁止的扩展名|$rel|.$ext")
    }

    # 2) 文件名 / 备份残留
    if ($name -match '^(keys\.txt|_keys\.txt|keyfile.*|id_rsa.*|id_ed25519.*)$') {
        $fail.Add("禁止的文件名|$rel|")
    }
    if ($name -match '\.(bak|backup|old|orig|rej|save)$' -or $name -match '~$') {
        $fail.Add("备份/补丁残留（可能含未清理内容）|$rel|")
    }

    # 3) 体积
    $bytes = Read-TargetBytes $t
    if ($null -eq $bytes) { continue }
    if ($bytes.Length -gt $HardMaxBytes) {
        $fail.Add("文件过大 >1MB|$rel|$($bytes.Length) 字节")
    } elseif ($bytes.Length -gt $WarnMaxBytes) {
        $warn.Add("文件偏大 >200KB|$rel|$($bytes.Length) 字节")
    }

    # 4) 二进制魔数
    if ($bytes.Length -ge 4) {
        $h4 = ($bytes[0..3] | ForEach-Object { $_.ToString('x2') }) -join ''
        switch -Regex ($h4) {
            '^7f454c46$' { $fail.Add("ELF 可执行文件|$rel|") }
            '^89554655$' { $fail.Add("DAT 固件包|$rel|") }
            '^(75786307|75786107)$' { $fail.Add("Sony UI 资源容器|$rel|") }
            '^4d5a'      { $fail.Add("Windows PE 可执行文件|$rel|") }
        }
    }

    # 5) 文本内容探测
    if ($TextExt -contains $ext -and $bytes.Length -lt $HardMaxBytes) {
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)

        # 5a/5b 硬失败：密钥被硬编码回源码
        if ($text -match 'Hex\s*\(\s*"[0-9A-Fa-f]{16,}"') {
            $fail.Add("源码中出现硬编码十六进制字面量（疑似密钥）|$rel|")
        }
        if ($text -match 'Key\s*\(\s*"[0-9A-Fa-f]{16,}"') {
            $fail.Add("Key() 收到十六进制字面量（疑似密钥）|$rel|")
        }
        # 5c 警告
        if ($text -match '[0-9A-Fa-f]{40,}') {
            $warn.Add("含 40+ 位十六进制串（请人工确认是否为哈希引用）|$rel|")
        }
    }
}

# ---- 汇总 ----
if ($warn.Count -gt 0) {
    Write-Host "──────────────────────────────────────────────────────────" -ForegroundColor Yellow
    Write-Host " 提示（不阻止）" -ForegroundColor Yellow
    Write-Host "──────────────────────────────────────────────────────────" -ForegroundColor Yellow
    foreach ($w in $warn) { Write-Host ("  · " + ($w -replace '\|',' ')) -ForegroundColor Yellow }
    Write-Host ""
}

if ($fail.Count -gt 0) {
    Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Red
    Write-Host " 检查失败：发现不应该进入仓库的内容" -ForegroundColor Red
    Write-Host "══════════════════════════════════════════════════════════" -ForegroundColor Red
    foreach ($f in $fail) { Write-Host ("  X " + ($f -replace '\|',' ')) -ForegroundColor Red }
    Write-Host ""
    Write-Host "本项目只发布「读」的能力，不发布固件、二进制、界面资源或密钥。" -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host ("通过：" + $targets.Count + " 个文件全部合格，无硬失败项。") -ForegroundColor Green
exit 0