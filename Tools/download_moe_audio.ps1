# 下載教育部《重編國語辭典修訂本》的錄音 (透過萌典 moedict.tw) 到 Assets/MoeAudio
# 讀取 Assets/Resources/phonic_symbols.txt 和 phonic_syllables.txt (和遊戲用同一份資料)：
#   <id>.ogg        符號 / 拼音的字音 (先核對注音和辭典一致)
#   w_<id>.ogg      例詞的錄音 (辭典有詞的錄音時)
#   wc_<id>_<n>.ogg 例詞沒有錄音時，每個字依辭典的讀音各下載一個，建置時接起來
# 原始錄音念完後還會念部首、筆畫等；建置時 AudioTrimmer 會只留下字音 / 詞音，存到 Assets/Resources/Audio
# 辭典內容授權：CC BY-ND 3.0 TW (中華民國教育部)。僅供個人學習使用。
# 用法：powershell -ExecutionPolicy Bypass -File Tools\download_moe_audio.ps1
param([string]$ProjectDir = (Join-Path $PSScriptRoot ".."))

$res = Join-Path $ProjectDir "Assets\Resources"
$OutDir = Join-Path $ProjectDir "Assets\MoeAudio"
$manifestPath = Join-Path $OutDir "manifest.txt"
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Read-Table($file) {
  Get-Content (Join-Path $res $file) -Encoding UTF8 | Where-Object { $_.Trim() -and -not $_.StartsWith('#') } | ForEach-Object { , ($_.Split('|')) }
}

$cache = @{}
function Get-Entry($w) {
  if (-not $cache.ContainsKey($w)) {
    try {
      $r = Invoke-WebRequest ("https://www.moedict.tw/a/" + [uri]::EscapeDataString($w) + ".json") -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
      $cache[$w] = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) | ConvertFrom-Json
    } catch { $cache[$w] = $null }
  }
  $cache[$w]
}

# 找某個字指定讀音的錄音編號
function Get-CharAudio($ch, $zhuyin) {
  $e = Get-Entry $ch
  if (-not $e) { return $null }
  ($e.h | Where-Object { $_.b -eq $zhuyin -and $_.'=' } | Select-Object -First 1).'='
}

$jobs = [ordered]@{}   # 檔名 → 辭典錄音編號
$problems = @()

foreach ($c in (Read-Table "phonic_symbols.txt")) {
  if ($c[3] -eq '-') { continue }
  $a = Get-CharAudio $c[3] $c[4]
  if ($a) { $jobs[$c[0]] = $a } else { $problems += "$($c[0]) ($($c[3]) $($c[4])): 辭典沒有這個讀音的錄音" }
}

foreach ($c in (Read-Table "phonic_syllables.txt")) {
  $id = $c[0]; $zhuyin = $c[1]; $word = $c[3]
  $ch = if ($c.Count -ge 5 -and $c[4]) { $c[4] } else { $c[2] }
  $a = Get-CharAudio $ch $zhuyin
  if ($a) { $jobs[$id] = $a } else { $problems += "$id ($ch): 要的是 $zhuyin，辭典沒有這個讀音的錄音" }

  # 例詞
  $e = Get-Entry $word
  if (-not $e) { $problems += "$id 例詞「$word」: 辭典沒有收錄"; continue }
  $h = $e.h | Where-Object { $_.'=' } | Select-Object -First 1
  if ($h) { $jobs["w_$id"] = $h.'='; continue }
  $syl = ($e.h | Select-Object -First 1).b.Trim() -split '\s+'
  if ($syl.Count -ne $word.Length) { $problems += "$id 例詞「$word」: 讀音和字數對不上"; continue }
  $parts = @()
  for ($i = 0; $i -lt $word.Length; $i++) {
    $pa = Get-CharAudio $word.Substring($i, 1) $syl[$i]
    if (-not $pa) { $parts = $null; $problems += "$id 例詞「$word」: $($word[$i]) $($syl[$i]) 沒有錄音"; break }
    $parts += $pa
  }
  if ($parts) { for ($i = 0; $i -lt $parts.Count; $i++) { $jobs["wc_${id}_$i"] = $parts[$i] } }
}

# 已下載的檔案 (檔名 → 錄音編號)；編號變了 (例如換了例詞) 就重新下載
$manifest = @{}
if (Test-Path $manifestPath) {
  foreach ($line in Get-Content $manifestPath) { $kv = $line.Split('|'); if ($kv.Count -eq 2) { $manifest[$kv[0]] = $kv[1] } }
}

$downloaded = 0
foreach ($name in $jobs.Keys) {
  $ogg = Join-Path $OutDir "$name.ogg"
  if ((Test-Path $ogg) -and $manifest[$name] -eq $jobs[$name]) { continue }
  $tmp = "$ogg.download"
  try {
    Invoke-WebRequest "https://r2-assets.moedict.tw/audio/a/$($jobs[$name]).ogg" -OutFile $tmp -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
    $head = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($tmp), 0, 4)
    if ($head -ne "OggS") { throw "not ogg" }
    Move-Item $tmp $ogg -Force
    $manifest[$name] = $jobs[$name]
    $downloaded++
  } catch {
    if (Test-Path $tmp) { Remove-Item $tmp }
    $problems += "${name}: 辭典網站上沒有錄音 $($jobs[$name])"
  }
}

# 刪掉已經用不到的舊音檔
foreach ($f in Get-ChildItem $OutDir -Filter *.ogg) {
  if (-not $jobs.Contains($f.BaseName)) {
    Remove-Item $f.FullName
    if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" }
    $manifest.Remove($f.BaseName)
  }
}
($manifest.Keys | Sort-Object | ForEach-Object { "$_|$($manifest[$_])" }) | Set-Content $manifestPath -Encoding ASCII

"Files: $($jobs.Count)  (new downloads: $downloaded)"
if ($problems) { "Problems:"; $problems }
