# cleanup-rdp.ps1 — RODAR COMO ADMINISTRADOR
# Reverte o setup do CIFS-root: remove share de injecao, desativa tvrdp,
# desliga RDP e regras de firewall.

$ErrorActionPreference = 'Continue'

Write-Host '== Shares =='
net share 'x$(nc 192.168.3.200 9998)' /delete 2>&1
Write-Host '(shares C..H deixadas intactas - eram acesso legitimo do Remote PC)'

Write-Host '== Usuario tvrdp =='
Remove-LocalGroupMember -Group 'Remote Desktop Users' -Member 'tvrdp' -ErrorAction SilentlyContinue
Disable-LocalUser -Name 'tvrdp' -ErrorAction SilentlyContinue
# Para remover de vez: descomente a linha abaixo
# Remove-LocalUser -Name 'tvrdp'

Write-Host '== RDP =='
Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server' -Name fDenyTSConnections -Value 1
Disable-NetFirewallRule -DisplayGroup 'Remote Desktop' -ErrorAction SilentlyContinue

Write-Host '== Listeners python (bridge/listener/http/relay) =='
Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
    Where-Object { $_.CommandLine -match 'bridge\.py|listener\.py|http\.server|wled-relay' } |
    ForEach-Object { Write-Host "kill $($_.ProcessId): $($_.CommandLine)"; Stop-Process -Id $_.ProcessId -Force }

Write-Host '== Estado final =='
net share | Select-String -NotMatch 'ADMIN\$|\$$|IPC\$' | Select-Object -First 12
Get-LocalUser -Name 'tvrdp','rpf16' | Select-Object Name,Enabled
(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Terminal Server').fDenyTSConnections
Write-Host 'PRONTO. Restaure a senha do rpf16 via Ctrl+Alt+Del > Alterar senha.'
