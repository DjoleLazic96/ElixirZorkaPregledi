@echo off
REM ============================================================================
REM Omotac za dev-db-init.ps1 — zaobilazi podrazumevanu ExecutionPolicy (Restricted)
REM na Windows klijentima, bez menjanja sistemskih podesavanja.
REM ============================================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev-db-init.ps1"
