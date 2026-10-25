@echo off
REM install-startup.bat — RODAR COMO ADMINISTRADOR (uma vez).
REM 1) Registra tv-stack.py no logon (shell:startup).
REM 2) Agenda re-arm da senha tvrdp = payload a cada boot (precisa admin).
setlocal
set KIT=%~dp0
set KIT=%KIT:~0,-1%

REM --- 1) shortcut no shell:startup (roda como usuario normal) ---
set STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup
powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut('%STARTUP%\tv-stack.lnk');$s.TargetPath='cmd.exe';$s.Arguments='/c start /min python \"%KIT%\tv-stack.py\"';$s.WorkingDirectory='%KIT%';$s.Save()"
echo [OK] tv-stack no startup do usuario.

REM --- 2) scheduled task admin: re-arma tvrdp a cada logon ---
schtasks /create /tn "tvrdp-rearm" /sc ONLOGON /rl HIGHEST /f ^
  /tr "powershell -NoProfile -ExecutionPolicy Bypass -File \"%KIT%\arm-rdp.ps1\""
echo [OK] task tvrdp-rearm criada.

REM --- 3) arma agora tambem ---
powershell -NoProfile -ExecutionPolicy Bypass -File "%KIT%\arm-rdp.ps1"
start /min python "%KIT%\tv-stack.py"
echo.
echo PRONTO. A cada logon: stack + senha tvrdp armados automaticamente.
pause
