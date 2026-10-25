@echo off
REM tv-stack.bat — sobe o stack de suporte (shell 9998, ponte 9997, beacon 9999, HTTP 8888/8899).
REM Rode uma vez pra testar; pra autostart use install-startup.bat como admin.
cd /d "%~dp0"
start "tv-stack" /min python "%~dp0tv-stack.py"
echo tv-stack iniciado (janela minimizada). Shell da TV conecta na 9998.
