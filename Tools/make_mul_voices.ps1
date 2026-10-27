# 產生九九乘法的語音 (Windows 內建中文語音 Microsoft Hanhan)：
#   Assets/Resources/MulVoice/q_<a>_<b>.wav  題目，例如「三乘以四」
#   Assets/Resources/MulVoice/c_<a>_<b>.wav  口訣，例如「三四十二」
# 想換成自己的錄音，用同樣檔名覆蓋即可。
# 用法：powershell -ExecutionPolicy Bypass -File Tools\make_mul_voices.ps1
param([string]$OutDir = (Join-Path $PSScriptRoot "..\Assets\Resources\MulVoice"))

$digits = @('零','一','二','三','四','五','六','七','八','九')

# 口訣裡的數字：10 → 一十、12 → 十二、24 → 二十四
function Num([int]$p) {
  if ($p -lt 10) { return $digits[$p] }
  $t = [math]::Floor($p / 10); $o = $p % 10
  if ($t -eq 1) { if ($o -eq 0) { return '一十' } else { return '十' + $digits[$o] } }
  $s = $digits[$t] + '十'
  if ($o -ne 0) { $s += $digits[$o] }
  return $s
}

# 九九乘法口訣：二三得六、三四十二 (要和 MulData.Chant 一致)
function Chant([int]$a, [int]$b) {
  $p = $a * $b
  $head = $digits[$a] + $digits[$b]
  if ($p -lt 10) { return $head + '得' + $digits[$p] }
  return $head + (Num $p)
}

Add-Type -AssemblyName System.Speech
New-Item -ItemType Directory -Force $OutDir | Out-Null
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
$s.SelectVoice('Microsoft Hanhan Desktop')
$s.Rate = -1
$n = 0
foreach ($a in 2..9) {
  foreach ($b in 1..9) {
    $s.SetOutputToWaveFile((Join-Path $OutDir "q_${a}_${b}.wav"))
    $s.Speak($digits[$a] + '成以' + $digits[$b])  # 語音引擎會把「乘」念成「勝」，用同音的「成」
    $s.SetOutputToWaveFile((Join-Path $OutDir "c_${a}_${b}.wav"))
    $s.Speak((Chant $a $b))
    $n += 2
  }
}
$s.SetOutputToNull()
$s.Dispose()
"Generated $n files in $OutDir"
"Sample: 3x4 = $(Chant 3 4), 2x5 = $(Chant 2 5), 2x3 = $(Chant 2 3), 4x5 = $(Chant 4 5), 7x8 = $(Chant 7 8)"
