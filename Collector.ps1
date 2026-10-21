$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$issues = [System.Collections.Generic.List[object]]::new()
function Read-Section([string]$Name, [scriptblock]$Body) {
    try { & $Body } catch { $issues.Add([ordered]@{ area = $Name; message = $_.Exception.Message }) }
}
function Read-Events($Filter, [int]$Maximum) {
    try {
        @(Get-WinEvent -FilterHashtable $Filter -MaxEvents $Maximum -ErrorAction Stop | ForEach-Object {
            [ordered]@{ timeUtc = $_.TimeCreated.ToUniversalTime().ToString('o'); id = $_.Id; provider = $_.ProviderName; message = $_.Message }
        })
    } catch { if ($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*') { throw } }
}
$maximum=[int]$env:WD_MAX;$since=(Get-Date).AddDays(-[int]$env:WD_DAYS)
$totalHistory=$null;$history=@(Read-Section 'update-history' {
    $session=New-Object -ComObject Microsoft.Update.Session
    $searcher=$session.CreateUpdateSearcher()
    $script:totalHistory=$searcher.GetTotalHistoryCount()
    if($script:totalHistory -gt 0) {
        $searcher.QueryHistory(0,[Math]::Min($script:totalHistory,$maximum)) | Where-Object {$_.Date -ge $since} | ForEach-Object {
            @{timeUtc=$_.Date.ToUniversalTime().ToString('o');title=$_.Title;resultCode=[int]$_.ResultCode;operation=[int]$_.Operation;hresult=('0x{0:X8}' -f ($_.HResult -band 0xffffffffL))}
        }
    }
})
$reboot=@{cbs=$null;windowsUpdate=$null;pendingFileRename=$null}
Read-Section 'reboot-indicators' {
    $reboot.cbs=Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'
    $reboot.windowsUpdate=Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
    $sessionPath='HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager'
    $rename=(Get-ItemProperty -LiteralPath $sessionPath).PendingFileRenameOperations
    $reboot.pendingFileRename=@($rename | Where-Object {$_}).Count -gt 0
} | Out-Null
$events=@(Read-Section 'update-events' {Read-Events @{LogName='System';ProviderName='Microsoft-Windows-WindowsUpdateClient';StartTime=$since} 100})
$free=Read-Section 'system-drive' { (Get-PSDrive -Name $env:SystemDrive.TrimEnd(':')).Free }
@{evidence=@{history=$history;historyCapped=($null -ne $totalHistory -and $totalHistory -gt $maximum);rebootIndicators=$reboot;events=$events;systemDriveFreeBytes=$free};issues=@($issues.ToArray())}|ConvertTo-Json -Depth 10 -Compress
