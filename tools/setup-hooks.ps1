<#
.SYNOPSIS
    启用本仓库的 git 钩子
.DESCRIPTION
    git 默认只从 .git/hooks 读钩子，且不随仓库分发。
    本仓库把钩子放在 .githooks/，用 core.hooksPath 指过去。
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$g = Get-Command git -ErrorAction SilentlyContinue
if (-not $g) { Write-Host "未检测到 git，请先安装。" -ForegroundColor Red; exit 1 }

Push-Location $root
try {
    & git config core.hooksPath .githooks
    if ($LASTEXITCODE -ne 0) { throw "git config 失败" }

    # 确保钩子可执行（Linux/macOS 或 WSL 检出的仓库需要）
    $hook = Join-Path $root '.githooks\pre-commit'
    if (Test-Path $hook) {
        try { & git update-index --chmod=+x .githooks/pre-commit 2>$null } catch {}
    }

    Write-Host "已启用：core.hooksPath = .githooks" -ForegroundColor Green
    Write-Host ""
    Write-Host "验证方式：随便改个文件后 git add，然后 git commit。"
    Write-Host "也可手动跑：tools\check-repo.ps1"
} finally { Pop-Location }