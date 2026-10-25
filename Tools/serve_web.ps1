# 在本機測試網頁版：powershell -ExecutionPolicy Bypass -File Tools\serve_web.ps1
# 然後用瀏覽器打開 http://localhost:8080/
param([int]$Port = 8080, [string]$Root = (Join-Path $PSScriptRoot "..\Build\WebGL"))

$Root = (Resolve-Path $Root).Path
$types = @{
  '.html' = 'text/html; charset=utf-8'; '.js' = 'application/javascript'; '.css' = 'text/css'
  '.wasm' = 'application/wasm'; '.data' = 'application/octet-stream'; '.json' = 'application/json'
  '.png' = 'image/png'; '.ico' = 'image/x-icon'; '.gz' = 'application/octet-stream'
}
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()
Write-Host "Serving $Root at http://localhost:$Port/"
while ($listener.IsListening) {
  $ctx = $listener.GetContext()
  $path = [Uri]::UnescapeDataString($ctx.Request.Url.AbsolutePath.TrimStart('/'))
  if ($path -eq '') { $path = 'index.html' }
  $file = Join-Path $Root $path
  if ((Test-Path $file -PathType Leaf) -and (Resolve-Path $file).Path.StartsWith($Root)) {
    $bytes = [IO.File]::ReadAllBytes($file)
    $ext = [IO.Path]::GetExtension($file).ToLower()
    $ctx.Response.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' }
    $ctx.Response.ContentLength64 = $bytes.Length
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
  } else {
    $ctx.Response.StatusCode = 404
  }
  $ctx.Response.Close()
}
