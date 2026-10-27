# =============================================================================
# TPM pregledi — inicijalizacija privatnog Postgres klastera za dev (Elixir blueprint 05 §11).
# =============================================================================
# Pokreni JEDNOM posle klona, kroz omotač: scripts\dev-db-init.cmd
# Pravi:
#   - data dir: <repo>\.local-pg-data\
#   - korisnika tpm-pregledi (lozinka tpm-pregledi-dev-only — dev-only, namerno javna; sluša samo localhost)
#   - bazu tpm-pregledi na portu 55434
# Posle: scripts\dev-db-start.cmd / dev-db-stop.cmd
# Bezbedno je pokrenuti ponovo: preskače initdb i kreiranje baze ako već postoje.
# =============================================================================
$ErrorActionPreference = 'Stop'

$PgBin   = 'C:\Program Files\PostgreSQL\16\bin'
$AppRoot = Split-Path -Parent $PSScriptRoot
$DataDir = Join-Path $AppRoot '.local-pg-data'
$Port    = 55434
$User    = 'tpm_pregledi'
$Pass    = 'tpm-pregledi-dev-only'
$Db      = 'tpm_pregledi'

if (-not (Test-Path "$PgBin\initdb.exe")) {
    throw "Nema PostgreSQL 16 binarija u '$PgBin'. Instaliraj PostgreSQL 16 (dovoljni su binariji, servis nije potreban)."
}

if (Test-Path (Join-Path $DataDir 'PG_VERSION')) {
    Write-Host "Klaster već postoji u '$DataDir'. Preskačem initdb."
} else {
    $PwFile = Join-Path $env:TEMP "$Db-pgpass.txt"
    try {
        Set-Content -Path $PwFile -Value $Pass -Encoding ASCII -NoNewline
        # sr_RS.UTF-8 da collation liči na prod; fallback en_US.UTF-8 (takođe UTF-8, Š/Ć/Č/Ž/Đ ispravno).
        & "$PgBin\initdb.exe" -D "$DataDir" -U $User --pwfile="$PwFile" -E UTF8 --locale='sr_RS.UTF-8'
        if ($LASTEXITCODE -ne 0) {
            Write-Warning 'initdb sa sr_RS.UTF-8 nije uspeo; fallback en_US.UTF-8…'
            & "$PgBin\initdb.exe" -D "$DataDir" -U $User --pwfile="$PwFile" -E UTF8 --locale='en_US.UTF-8'
            if ($LASTEXITCODE -ne 0) { throw 'initdb nije uspeo ni sa fallback locale-om.' }
        }
    }
    finally {
        if (Test-Path $PwFile) { Remove-Item -Path $PwFile -Force -ErrorAction SilentlyContinue }
    }
    # Port — upis BEZ BOM-a (Postgres na Windows-u pada ako je conf sa UTF-8 BOM-om).
    $Conf = Join-Path $DataDir 'postgresql.conf'
    $content = [IO.File]::ReadAllText($Conf) -replace '(?m)^#?port = \d+', "port = $Port"
    [IO.File]::WriteAllText($Conf, $content, (New-Object System.Text.UTF8Encoding $false))
}

& "$PSScriptRoot\dev-db-start.cmd"
& "$PgBin\pg_isready.exe" -h 127.0.0.1 -p $Port -t 15 | Out-Null

# Kreiraj bazu samo ako ne postoji. Ne preusmeravamo stderr psql-a: u PowerShell 5.1
# `2>$null` nad native exe-om uz ErrorActionPreference=Stop baca izuzetak.
$env:PGPASSWORD = $Pass
# [string] cast: psql bez rezultata vraća $null, a $null.Trim() bi uz ErrorActionPreference=Stop oborio skriptu.
[string]$postoji = (& "$PgBin\psql.exe" -h 127.0.0.1 -p $Port -U $User -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$Db'")
if ($postoji.Trim() -eq '1') {
    Write-Host "Baza '$Db' već postoji."
} else {
    & "$PgBin\psql.exe" -h 127.0.0.1 -p $Port -U $User -d postgres -c "CREATE DATABASE $Db OWNER $User;"
}

Write-Host ''
Write-Host 'Klaster spreman.'
Write-Host "  port: $Port   user: $User   pass: $Pass   db: $Db"
Write-Host "  data: $DataDir"
Write-Host 'Sledeće: dotnet run --project backend\src\TpmPregledi.Api   (migracije + seed automatski)'
