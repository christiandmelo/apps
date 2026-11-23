# Gera o ícone do AppsCm (monograma "CM", traço uniforme, cores sólidas).
# Uso: .\gerar-icone.ps1            -> regrava favicon.ico nesta pasta
#      .\gerar-icone.ps1 -Saida X   -> grava em outra pasta (inclui logo.png e previews)
param([string]$Saida = $PSScriptRoot)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Saida | Out-Null

$azul   = [System.Drawing.Color]::FromArgb(255, 17, 112, 198)
$azulClaro = [System.Drawing.Color]::FromArgb(255, 74, 163, 232)

function Novo-Pen($cor, $largura) {
  $p = [System.Drawing.Pen]::new($cor, [float]$largura)
  $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
  return $p
}

# Desenha no espaço 1024x1024. $ousado = traço mais grosso para tamanhos pequenos
function Desenhar([int]$n, [bool]$ousado) {
  $bmp = [System.Drawing.Bitmap]::new($n, $n, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)

  $w = if ($ousado) { 150 } else { 110 }
  $h = 460                         # altura das letras pela linha central
  $topo = 512 - $h / 2; $base = 512 + $h / 2
  $r = $h / 2                      # raio do C
  $cxC = 0; $xM1 = $cxC + $r * 0.707 + $w * 0.75 + 90; $xM2 = $xM1 + 330

  # centraliza horizontalmente o conjunto (do lado externo do C até o lado externo do M)
  $esq = $cxC - $r - $w / 2; $dir = $xM2 + $w / 2
  $dx = (1024 - ($esq + $dir)) / 2
  $g.ScaleTransform($n / 1024.0, $n / 1024.0)
  $g.TranslateTransform([float]$dx, 0)

  # C: arco de 270° aberto para a direita
  $pc = Novo-Pen $azul $w
  $g.DrawArc($pc, [float]($cxC - $r), [float]$topo, [float](2 * $r), [float](2 * $r), 45, 270); $pc.Dispose()

  # M
  $meio = ($xM1 + $xM2) / 2
  $pts = @(@($xM1, $base), @($xM1, $topo), @($meio, 512 + 20), @($xM2, $topo), @($xM2, $base)) |
    ForEach-Object { [System.Drawing.PointF]::new([float]$_[0], [float]$_[1]) }
  $pm = Novo-Pen $azulClaro $w
  $g.DrawLines($pm, [System.Drawing.PointF[]]$pts); $pm.Dispose()

  $g.Dispose()
  return $bmp
}

if ($Saida -ne $PSScriptRoot) {
  $logo = Desenhar 1024 $false
  $logo.Save((Join-Path $Saida 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png); $logo.Dispose()
}

# ICO: <=48 em BMP 32bpp (+ máscara AND), 256 em PNG
$entradas = foreach ($n in 16, 24, 32, 48, 256) {
  $bmp = Desenhar $n ($n -le 32)
  if ($Saida -ne $PSScriptRoot) { $bmp.Save((Join-Path $Saida "preview-$n.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
  $ms = [IO.MemoryStream]::new()
  if ($n -eq 256) { $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png) }
  else {
    $bw = [IO.BinaryWriter]::new($ms)
    $bw.Write([int]40); $bw.Write([int]$n); $bw.Write([int]($n * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
    1..6 | ForEach-Object { $bw.Write([int]0) }
    for ($y = $n - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $n; $x++) {
      $c = $bmp.GetPixel($x, $y); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) } }
    $bw.Write([byte[]]::new([int]([math]::Ceiling($n / 32.0) * 4) * $n))
    $bw.Flush()
  }
  $bmp.Dispose()
  [pscustomobject]@{ N = $n; Dados = $ms.ToArray() }
}
$ico = [IO.MemoryStream]::new(); $wr = [IO.BinaryWriter]::new($ico)
$wr.Write([int16]0); $wr.Write([int16]1); $wr.Write([int16]$entradas.Count)
$offset = 6 + 16 * $entradas.Count
foreach ($e in $entradas) {
  $dim = if ($e.N -ge 256) { 0 } else { $e.N }
  $wr.Write([byte]$dim); $wr.Write([byte]$dim); $wr.Write([byte]0); $wr.Write([byte]0)
  $wr.Write([int16]1); $wr.Write([int16]32); $wr.Write([int]$e.Dados.Length); $wr.Write([int]$offset)
  $offset += $e.Dados.Length
}
foreach ($e in $entradas) { $wr.Write($e.Dados) }
$wr.Flush()
[IO.File]::WriteAllBytes((Join-Path $Saida 'favicon.ico'), $ico.ToArray())
