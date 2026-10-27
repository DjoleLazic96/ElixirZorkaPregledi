@echo off
REM ============================================================================
REM Startuje privatni Postgres klaster (port 55434). Prvi put: dev-db-init.cmd.
REM ============================================================================
REM pg_ctl-ov stdout/stderr idu u nul: postgres proces nasledjuje te handle-ove,
REM pa bi svaki skript/alat koji hvata izlaz ove komande visio dok server radi.
REM Server log je u %LOG%.
setlocal
set "PGBIN=C:\Program Files\PostgreSQL\16\bin"
set "DATA=%~dp0..\.local-pg-data"
set "LOG=%~dp0..\.local-pg-data-server.log"

if not exist "%DATA%\PG_VERSION" (
  echo GRESKA: Klaster nije inicijalizovan. Pokreni prvo scripts\dev-db-init.cmd
  exit /b 1
)

"%PGBIN%\pg_ctl.exe" -D "%DATA%" status >nul 2>&1
if %errorlevel%==0 (
  echo Postgres klaster vec radi ^(port 55434^).
  exit /b 0
)

"%PGBIN%\pg_ctl.exe" -D "%DATA%" -l "%LOG%" -w start >nul 2>&1
if %errorlevel% neq 0 (
  echo GRESKA: Postgres nije uspeo da se pokrene. Vidi %LOG%
  exit /b 1
)
echo Postgres klaster pokrenut ^(port 55434^). Log: %LOG%
endlocal
