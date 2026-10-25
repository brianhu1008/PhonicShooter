# 備用：用 Windows 內建中文語音 (Microsoft Hanhan, zh-TW) 產生電腦語音發音檔。
# 遊戲現在用的是真人錄音 (Tools\download_recordings.ps1)，所以這裡預設輸出到 Tools\tts_audio，
# 不會覆蓋遊戲裡的錄音。真的要改用電腦語音，再把檔案複製到 Assets\Resources\Audio。
# 用法：在專案資料夾執行  powershell -ExecutionPolicy Bypass -File Tools\make_voices.ps1
param([string]$OutDir = (Join-Path $PSScriptRoot "tts_audio"))
Add-Type -AssemblyName System.Speech
$items = [ordered]@{
  b='玻'; p='坡'; m='摸'; f='佛'
  ba='八'; bo='波'; bai='白'; bei='杯'; bao='包'; ban='班'; ben='本'; bang='幫'; bi='鼻'; bu='布'
  pa='怕'; po='婆'; pai='拍'; pao='跑'; pan='盤'; pen='盆'; pang='胖'; peng='朋'; pi='皮'; pu='葡'
  ma='媽'; mo='摸'; mai='買'; mei='美'; mao='貓'; man='慢'; men='門'; mang='忙'; meng='夢'; mi='米'; mu='木'
  fa='發'; fei='飛'; fan='飯'; fen='分'; fang='房'; feng='風'; fu='父'
}
New-Item -ItemType Directory -Force $OutDir | Out-Null
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$s.SelectVoice('Microsoft Hanhan Desktop')
$s.Rate = -2
foreach ($k in $items.Keys) {
  $s.SetOutputToWaveFile((Join-Path $OutDir "$k.wav"))
  $s.Speak($items[$k])
}
$s.SetOutputToNull()
$s.Dispose()
"Generated $((Get-ChildItem $OutDir -Filter *.wav).Count) files in $OutDir"
