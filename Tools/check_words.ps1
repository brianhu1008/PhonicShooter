# 檢查例詞在教育部辭典的狀況：有詞的錄音 / 可以用單字錄音接起來 / 不行
param([string[]]$Words)
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$cache = @{}
function Get-Entry($w) {
  if (-not $cache.ContainsKey($w)) {
    try {
      $r = Invoke-WebRequest ("https://www.moedict.tw/a/" + [uri]::EscapeDataString($w) + ".json") -UseBasicParsing -TimeoutSec 20 -ErrorAction Stop
      $cache[$w] = [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) | ConvertFrom-Json
    } catch { $cache[$w] = $null }
  }
  $cache[$w]
}
function Test-Word($w) {
  $e = Get-Entry $w
  if (-not $e) { return "notfound" }
  $h = $e.h | Where-Object { $_.'=' } | Select-Object -First 1
  if ($h) { return "audio" }
  $b = ($e.h | Select-Object -First 1).b
  $syl = $b.Trim() -split '\s+'
  if ($syl.Count -ne $w.Length) { return "concat-fail(讀音 $b)" }
  for ($i = 0; $i -lt $w.Length; $i++) {
    $ce = Get-Entry $w.Substring($i, 1)
    $m = $ce.h | Where-Object { $_.b -eq $syl[$i] -and $_.'=' }
    if (-not $m) { return "concat-fail($($w[$i]) $($syl[$i]) 沒有錄音)" }
  }
  return "concat($b)"
}
foreach ($w in $Words) { "$w`t$(Test-Word $w)" }
