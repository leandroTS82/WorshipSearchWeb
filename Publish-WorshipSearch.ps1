# ============================================================
#  Publish-WorshipSearch.ps1
#  Script de publicação do WorshipSearchWeb
# ============================================================

# ─── CONFIGURAÇÃO ────────────────────────────────────────────

# Pasta de destino final do .zip
$OutputPath = "C:\Deploy\Output"

# Arquivos e pastas a excluir do publish (relativos à raiz do publish)
# Suporta wildcards: "*.pdb", "pasta\*", etc.
$ItemsToExclude = @(
    "appsettings.Development.json",
    "*.pdb",
    "scriptsDemonstracao",
    "web.config.bak"
)

# ─── CONFIGURAÇÕES INTERNAS (ajuste se necessário) ───────────

# Pasta raiz do projeto (onde está o .csproj)
$ProjectRoot = $PSScriptRoot

# Nome do projeto (aparece no .zip)
$ProjectName = "WorshipSearchWeb"

# Configuração do build (Release ou Debug)
$BuildConfig = "Release"

# Framework alvo (deve coincidir com o TargetFramework do .csproj)
$TargetFramework = "net8.0"

# Pasta padrão do publish gerada pelo dotnet
$PublishDir = Join-Path $ProjectRoot "bin\$BuildConfig\$TargetFramework\publish"

# ─────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "──────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "  $msg" -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────" -ForegroundColor DarkGray
}

function Write-OK([string]$msg) {
    Write-Host "  ✓ $msg" -ForegroundColor Green
}

function Write-Info([string]$msg) {
    Write-Host "  · $msg" -ForegroundColor Gray
}

function Write-Fail([string]$msg) {
    Write-Host "  ✗ $msg" -ForegroundColor Red
}

# ─── INÍCIO ──────────────────────────────────────────────────

Clear-Host
Write-Host ""
Write-Host "  ╔══════════════════════════════════╗" -ForegroundColor Yellow
Write-Host "  ║   WorshipSearch — Publicação     ║" -ForegroundColor Yellow
Write-Host "  ╚══════════════════════════════════╝" -ForegroundColor Yellow
Write-Host "  Início: $StartTime" -ForegroundColor DarkGray
Write-Host "  Projeto: $ProjectRoot" -ForegroundColor DarkGray
Write-Host "  Destino: $OutputPath" -ForegroundColor DarkGray

Set-Location $ProjectRoot

# ─── 1. GIT PULL ─────────────────────────────────────────────

Write-Step "1/6  Git Pull"
try {
    $gitOutput = git pull 2>&1
    Write-Info $gitOutput
    Write-OK "Repositório atualizado."
} catch {
    Write-Fail "Falha no git pull: $_"
    exit 1
}

# ─── 2. BUILD ────────────────────────────────────────────────

Write-Step "2/6  Build ($BuildConfig)"
try {
    $buildResult = dotnet build -c $BuildConfig --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Build falhou:`n$buildResult"
        exit 1
    }
    Write-OK "Build concluído sem erros."
} catch {
    Write-Fail "Erro durante o build: $_"
    exit 1
}

# ─── 3. PUBLISH ──────────────────────────────────────────────

Write-Step "3/6  Publish"

# Limpa publish anterior se existir
if (Test-Path $PublishDir) {
    Write-Info "Removendo publish anterior em: $PublishDir"
    Remove-Item $PublishDir -Recurse -Force
}

try {
    $publishResult = dotnet publish -c $BuildConfig --nologo --no-build 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Publish falhou:`n$publishResult"
        exit 1
    }
    Write-OK "Publish gerado em: $PublishDir"
} catch {
    Write-Fail "Erro durante o publish: $_"
    exit 1
}

# ─── 4. EXCLUIR ITENS ────────────────────────────────────────

Write-Step "4/6  Excluindo itens desnecessários"

foreach ($item in $ItemsToExclude) {
    $fullPattern = Join-Path $PublishDir $item

    # Resolve wildcards
    $resolved = Get-Item -Path $fullPattern -ErrorAction SilentlyContinue

    if ($null -eq $resolved) {
        Write-Info "Não encontrado (pulando): $item"
        continue
    }

    foreach ($target in $resolved) {
        try {
            Remove-Item $target.FullName -Recurse -Force
            Write-OK "Removido: $($target.Name)"
        } catch {
            Write-Fail "Não foi possível remover $($target.FullName): $_"
        }
    }
}

# ─── 5. CRIAR ZIP ────────────────────────────────────────────

Write-Step "5/6  Criando .zip"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$ZipName   = "${ProjectName}_${Timestamp}.zip"
$ZipTemp   = Join-Path $env:TEMP $ZipName

try {
    Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipTemp -CompressionLevel Optimal -Force
    $ZipSize = (Get-Item $ZipTemp).Length / 1MB
    Write-OK "Zip criado: $ZipName ($([math]::Round($ZipSize,2)) MB)"
} catch {
    Write-Fail "Falha ao criar zip: $_"
    exit 1
}

# ─── 6. MOVER PARA OUTPUT E LIMPAR ───────────────────────────

Write-Step "6/6  Movendo para destino e limpando publish"

# Garante que a pasta de output existe
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Info "Pasta de output criada: $OutputPath"
}

$FinalZip = Join-Path $OutputPath $ZipName
try {
    Move-Item -Path $ZipTemp -Destination $FinalZip -Force
    Write-OK "Zip movido para: $FinalZip"
} catch {
    Write-Fail "Falha ao mover zip: $_"
    exit 1
}

# Limpa a pasta publish
try {
    Remove-Item $PublishDir -Recurse -Force
    Write-OK "Pasta publish limpa."
} catch {
    Write-Fail "Aviso: não foi possível limpar publish: $_"
}

# ─── RESUMO ──────────────────────────────────────────────────

$Elapsed = (Get-Date) - $StartTime

Write-Host ""
Write-Host "  ╔══════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║        Publicação concluída!     ║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════╝" -ForegroundColor Green
Write-Host "  Arquivo : $ZipName" -ForegroundColor White
Write-Host "  Destino : $FinalZip" -ForegroundColor White
Write-Host "  Tempo   : $([math]::Round($Elapsed.TotalSeconds, 1))s" -ForegroundColor White
Write-Host ""
