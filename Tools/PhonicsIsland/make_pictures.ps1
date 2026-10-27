# Renders the word pictures (Windows emoji, via Edge headless) into Assets/Resources/Pics/<word>.png.
# Run again after adding a word:  powershell -ExecutionPolicy Bypass -File Tools\make_pictures.ps1
param([string]$OutDir = (Join-Path $PSScriptRoot '..\Assets\Resources\Pics'))
# word -> emoji codepoints (hex, space separated)
$map = [ordered]@{
  apple='1F34E'; cat='1F431'; bag='1F45C'; egg='1F95A'; bed='1F6CF FE0F'; hen='1F414'; fish='1F41F'; pig='1F437';
  octopus='1F419'; dog='1F436'; box='1F4E6'; umbrella='2602 FE0F'; sun='2600 FE0F'; bug='1F41B';
  ship='1F6A2'; shop='1F3EA'; shell='1F41A'; sheep='1F411'; chick='1F424'; cheese='1F9C0'; chair='1FA91';
  three='0033 FE0F 20E3'; thumb='1F44D'; bath='1F6C1'; teeth='1F9B7';
  tree='1F333'; bee='1F41D'; beach='1F3D6 FE0F'; leaf='1F343'; tea='1F375'; rain='1F327 FE0F'; snail='1F40C';
  train='1F686'; crayon='1F58D FE0F'; crab='1F980'; star='2B50';
  moon='1F319'; spoon='1F944'; boot='1F462'; book='1F4D6'; foot='1F9B6'; wood='1FAB5';
  cow='1F404'; owl='1F989'; house='1F3E0'; mouse='1F42D'; cloud='2601 FE0F'; coin='1FA99'; boy='1F466'; toy='1F9F8';
  fox='1F98A'; bus='1F68C';
  ant='1F41C'; bat='1F987'; hat='1F3A9'; map='1F5FA FE0F'; pen='1F58A FE0F'; net='1F945'; ten='1F51F'; web='1F578 FE0F';
  bell='1F514'; leg='1F9B5'; six='0036 FE0F 20E3'; pin='1F4CC'; milk='1F95B'; gift='1F381'; ring='1F48D'; king='1F934';
  lock='1F512'; sock='1F9E6'; frog='1F438'; rock='1FAA8'; ox='1F402'; cup='1F964'; duck='1F986'; drum='1F941'; nut='1F95C';
  elephant='1F418'; otter='1F9A6'; alligator='1F40A';
  shoe='1F45F'; shirt='1F455'; shark='1F988'; shorts='1FA73'; shower='1F6BF'; brush='1FAA5'; dish='1F37D FE0F';
  cherry='1F352'; chicken='1F414'; chocolate='1F36B'; church='26EA'; chips='1F35F'; peach='1F351'; watch='231A';
  thread='1F9F5'; think='1F914'; earth='1F30D'; feather='1FAB6'; mouth='1F444'; whale='1F40B'; wheel='1F6DE';
  flag='1F6A9'; flower='1F338'; plane='2708 FE0F'; sled='1F6F7'; clock='1F570 FE0F'; clown='1F921'; snake='1F40D';
  spider='1F577 FE0F'; skate='26F8 FE0F'; bread='1F35E'; grapes='1F347'; glasses='1F453'; stop='1F6D1'; swan='1F9A2'; crown='1F451';
  seed='1F331'; queen='1F478'; meat='1F356'; bean='1FAD8'; seal='1F9AD'; eagle='1F985';
  mail='1F4EC'; nail='1F485'; paint='1F3A8'; sail='26F5'; chain='26D3 FE0F'; rainbow='1F308';
  tooth='1F9B7'; goose='1FABF'; broom='1F9F9'; balloon='1F388'; rooster='1F413'; kangaroo='1F998'; scooter='1F6F4'; noodles='1F35C';
  cook='1F9D1 200D 1F373'; hook='1FA9D'; cookie='1F36A'; wool='1F9F6'; football='26BD'; look='1F440';
  tower='1F5FC'; couch='1F6CB FE0F'; fountain='26F2'; oil='1F6E2 FE0F'; toilet='1F6BD'; point='1F449'; oyster='1F9AA';
  cake='1F382'; kite='1FA81'
}
$cell = 192; $cols = 10
$keys = @($map.Keys); $rows = [math]::Ceiling($keys.Count / $cols)
$sb = New-Object System.Text.StringBuilder
[void]$sb.Append('<!doctype html><meta charset="utf-8"><style>html,body{margin:0;background:transparent}div{position:absolute;width:192px;height:192px;display:flex;align-items:center;justify-content:center;font:150px/1 "Segoe UI Emoji"}</style>')
for ($i = 0; $i -lt $keys.Count; $i++) {
  $cps = $map[$keys[$i]].Split(' ') | ForEach-Object { '&#x' + $_ + ';' }
  [void]$sb.Append(('<div style="left:{0}px;top:{1}px">{2}</div>' -f (($i % $cols) * $cell), ([math]::Floor($i / $cols) * $cell), ($cps -join '')))
}
$tmp = Join-Path $env:TEMP 'phonics_pics'
New-Item -ItemType Directory -Force $tmp | Out-Null
$html = Join-Path $tmp 'grid.html'; $png = Join-Path $tmp 'grid.png'
[IO.File]::WriteAllText($html, $sb.ToString(), [Text.Encoding]::UTF8)
& 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe' --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --default-background-color=00000000 "--window-size=$($cols*$cell),$($rows*$cell)" "--screenshot=$png" ("file:///" + $html.Replace('\','/')) | Out-Null
Start-Sleep 1
Add-Type -AssemblyName System.Drawing
$img = [Drawing.Bitmap]::FromFile($png)
New-Item -ItemType Directory -Force $OutDir | Out-Null
for ($i = 0; $i -lt $keys.Count; $i++) {
  $rect = New-Object Drawing.Rectangle (($i % $cols) * $cell), ([math]::Floor($i / $cols) * $cell), $cell, $cell
  $part = $img.Clone($rect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $part.Save((Join-Path $OutDir ($keys[$i] + '.png')), [Drawing.Imaging.ImageFormat]::Png); $part.Dispose()
}
$img.Dispose()
"$($keys.Count) pictures written to $OutDir"
