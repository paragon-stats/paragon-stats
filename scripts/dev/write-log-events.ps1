<#
.SYNOPSIS
    Writes anonymized Homecoming chat-log events so the live watch path can be
    proved against a moving file rather than a static fixture.

.DESCRIPTION
    Static fixtures cannot exercise the live reader: LogWatcher only attaches to
    files written inside the attach window, and a file that never grows never
    proves incremental tailing. This writes real-shaped events, stamped now, in
    waves, so a watcher discovers the file and then follows it.

    The event mix is taken from the busiest minute in a real account's logs
    (3,986 lines in 60 seconds - roughly 66 lines a second at a full farm):
    pet and self damage dominate, misses are the largest single share, and
    rewards, defeats, activations and communication lines are a thin tail.
    Every name and every value here is invented; only the SHAPE is real. No
    line of player communication is reproduced - the refused lines below are
    synthetic and all carry the token "dropme" so a test can assert in one
    grep that none of them survived.

    Line endings default to CRLF because that is what the game writes. Nothing
    this script produces is ever committed - it writes into a temp directory at
    test time - so the ending is set here explicitly rather than inherited from
    .gitattributes. The point is to test like for like against the real client.
    The committed fixtures are LF, which is why LF was the only ending the live
    path had ever run against.

.PARAMETER Path
    Chatlog file to create and append to. Parent directories are created.

.PARAMETER DelaySeconds
    Pause between waves. Wave one lands before the watcher's first rediscovery
    and later waves land after it, so both attach and follow are exercised.

.PARAMETER LineEnding
    CRLF (the game's own) or LF.

.EXAMPLE
    pwsh -File scripts/dev/write-log-events.ps1 -Path "$env:TEMP/live/accounts/box/Logs/chatlog.txt"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [int] $DelaySeconds = 6,

    [ValidateSet('CRLF', 'LF')]
    [string] $LineEnding = 'CRLF'
)

$ErrorActionPreference = 'Stop'

$terminator = if ($LineEnding -eq 'CRLF') { "`r`n" } else { "`n" }
$encoding = [System.Text.UTF8Encoding]::new($false)
$culture = [System.Globalization.CultureInfo]::InvariantCulture

$parent = Split-Path -Path $Path -Parent
if ($parent -and -not (Test-Path -LiteralPath $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

# Fullwidth brackets, built from code points so this file stays ASCII for
# check-encoding. A confusable bracket must be refused like an ASCII one.
$openWide = [string][char]0xFF3B
$closeWide = [string][char]0xFF3D

# Wave one: the login, then a fight. The first line lands BEFORE the welcome
# banner on purpose - it is the one unattributed line the readout should report.
$waveOne = @(
    'You gain 100 experience.'
    'Welcome to City of Heroes, Fixture Brute!'
    'Entering Council Earth.'
    'You activated the Blazing Aura power.'
)
$waveOne += (1..20 | ForEach-Object {
        'You hit Council Bossman with your Blazing Aura for 25 points of Fire damage.'
    })
$waveOne += (1..8 | ForEach-Object {
        'Council Bossman MISSES! Blazing Aura power had a 75% chance to hit, but rolled a 91.'
    })
$waveOne += @(
    'You have defeated Council Bossman'
    'You gain 10,000 experience and 20,000 influence.'
)

# Wave two: the pet burst that dominates a real farm minute, incoming damage
# nobody folds, and every communication shape that must be refused.
$waveTwo = @()
$waveTwo += (1..30 | ForEach-Object {
        'Fire Imp:  You hit Council Bossman with your Fire Breath for 10 points of Fire damage.'
    })
$waveTwo += (1..5 | ForEach-Object {
        'Council Bossman hits you with their Assault Rifle for 12.5 points of Lethal damage over time.'
    })
$waveTwo += @(
    '[Local] Chatter Person: dropme-local'
    '[Broadcast] Chatter Person: dropme-broadcast'
    '[Tell] :Chatter Person: dropme-tell'
    '[Team] Chatter Person: dropme-team'
    '[Supergroup] Chatter Person: dropme-supergroup'
    '[Looking For Group] Chatter Person: dropme-lfg'
    '[Request] Chatter Person: dropme-request'
    '[Help] Chatter Person: dropme-help'
    '[General] Chatter Person: dropme-general'
    '[NPC] Some Contact: dropme-npc'
    '[Caption] dropme-caption'
    'Using global chat handle dropme-handle'
    'Joined channel dropme-joined'
    'Left channel dropme-left'
    '   [Local] Chatter Person: dropme-leading-whitespace'
    ($openWide + 'Local' + $closeWide + ' Chatter Person: dropme-fullwidth-bracket')
    'You earned 50 architect tickets!'
)

# Wave three: the market, the identity trigger, and the close of the fight.
$waveThree = @(
    'You got 5,000,000 influence from the Consignment House.'
    'You paid 1,250,000 to the Consignment House.'
    'HIT Fixture Brute! Your Health power is autohit.'
)
$waveThree += (1..10 | ForEach-Object {
        'You hit Council Bossman with your Blazing Aura for 25 points of Fire damage.'
    })
$waveThree += @(
    'You have defeated Council Bossman'
    'You gain 5,000 experience, work off 500 debt, and gain 10,000 influence.'
)

$waves = @($waveOne, $waveTwo, $waveThree)
$written = 0

for ($index = 0; $index -lt $waves.Count; $index++) {
    if ($index -gt 0) {
        Start-Sleep -Seconds $DelaySeconds
    }

    $stamp = [System.DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss', $culture)
    $stamped = $waves[$index] | ForEach-Object { $stamp + ' ' + $_ }
    $text = ($stamped -join $terminator) + $terminator

    # A continuation line carries no timestamp of its own. It is communication
    # content by definition and must be refused, so wave two emits one raw.
    if ($index -eq 1) {
        $text += 'dropme-continuation-line-with-no-timestamp' + $terminator
        $written++
    }

    [System.IO.File]::AppendAllText($Path, $text, $encoding)
    $written += $waves[$index].Count
    Write-Output ("wave {0}: {1} lines at {2}" -f ($index + 1), $waves[$index].Count, $stamp)
}

# What the binary must agree with. The runner reads these rather than carrying
# a second copy of the arithmetic that would drift the first time a wave changes.
Write-Output ('EXPECT character=Fixture Brute')
Write-Output ('EXPECT damage=1050')
Write-Output ('EXPECT defeats=2')
Write-Output ('EXPECT xp=15000')
Write-Output ('EXPECT inf=30000')
Write-Output ('EXPECT tickets=50')
Write-Output ('EXPECT activations=1')
Write-Output ('EXPECT market=+5000000/-1250000')
Write-Output ('EXPECT unattributed=1')
Write-Output ("EXPECT lines={0}" -f $written)
