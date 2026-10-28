# Records Phonics Island voice lines with the Windows English voice (same voice as the PC version).
# Input: one line per clip, "key<TAB>base64" (base64 of the format PhonicsIslandGame.TtsLine makes).
# Output: <OutDir>\<key>.wav, 22050 Hz 16-bit mono.
param([string]$ListFile, [string]$OutDir)

Add-Type -AssemblyName System.Speech
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$v = $s.GetInstalledVoices() | Where-Object { $_.Enabled -and $_.VoiceInfo.Culture.Name -like 'en-*' } | Select-Object -First 1
if (-not $v) { Write-Error "No English voice installed"; exit 1 }
$s.SelectVoice($v.VoiceInfo.Name)
$s.Rate = -2
$fmt = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)

New-Item -ItemType Directory -Force $OutDir | Out-Null
$n = 0
foreach ($row in [IO.File]::ReadAllLines($ListFile, [Text.Encoding]::UTF8)) {
  $tab = $row.IndexOf("`t")
  if ($tab -lt 1) { continue }
  $key = $row.Substring(0, $tab)
  $txt = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($row.Substring($tab + 1)))
  $pb = New-Object System.Speech.Synthesis.PromptBuilder
  foreach ($part in $txt.Split([char]1)) {
    if ($part.Length -eq 0) { continue }
    if ($part[0] -eq [char]2) { $p = $part.Substring(1).Split([char]3); $pb.AppendTextWithPronunciation($p[0], $p[1]) }
    else { $pb.AppendText($part) }
  }
  $s.SetOutputToWaveFile((Join-Path $OutDir ($key + ".wav")), $fmt)
  $s.Speak($pb)
  $n++
}
$s.SetOutputToNull()
$s.Dispose()
Write-Output "Recorded $n voice lines"
