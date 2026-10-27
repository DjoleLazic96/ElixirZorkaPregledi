@echo off
REM ============================================================================
REM Zaustavlja privatni Postgres klaster (port 55434).
REM ============================================================================
setlocal
set "PGBIN=C:\Program Files\PostgreSQL\16\bin"
set "DATA=%~dp0..\.local-pg-data"

"%PGBIN%\pg_ctl.exe" -D "%DATA%" stop -m fast
endlocal
