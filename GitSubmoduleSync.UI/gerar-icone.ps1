# Gera o ícone do GitSubmoduleSync (setas de sincronização em volta de um mini diagrama de branches).
# Uso: .\gerar-icone.ps1            -> regrava favicon.ico nesta pasta
#      .\gerar-icone.ps1 -Saida X   -> grava em outra pasta (inclui logo.png e previews)
param([string]$Saida = $PSScriptRoot)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Saida | Out-Null

$azul  = [System.Drawing.Color]::FromArgb(255, 17, 112, 198)
$verde = [System.Drawing.Color]::FromArgb(255, 53, 152, 98)

function Novo-Pen($cor, $largura) {
  $p = [System.Drawing.Pen]::new($cor, [float]$largura)
  $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
  return $p
}

function Ponto([double]$x, [double]$y) { [System.Drawing.PointF]::new([float]$x, [float]$y) }

# Arco do círculo de sincronização com ponta de seta (chevron) no fim do percurso.
# Ângulos no padrão do GDI+: 0° = direita, sentido horário positivo.
function Desenhar-Seta($g, $pen, [double]$r, [double]$inicio, [double]$varredura, [double]$braco) {
  $g.DrawArc($pen, [float](512 - $r), [float](512 - $r), [float](2 * $r), [float](2 * $r), [float]$inicio, [float]$varredura)
  $fim = ($inicio + $varredura) * [math]::PI / 180
  $px = 512 + $r * [math]::Cos($fim); $py = 512 + $r * [math]::Sin($fim)
  $sentido = [math]::Sign($varredura)
  # tangente no sentido do percurso
  $tx = -[math]::Sin($fim) * $sentido; $ty = [math]::Cos($fim) * $sentido
  foreach ($giro in 40, -40) {
    $a = $giro * [math]::PI / 180
    # vetor oposto à tangente, girado
    $bx = -($tx * [math]::Cos($a) - $ty * [math]::Sin($a)); $by = -($tx * [math]::Sin($a) + $ty * [math]::Cos($a))
    $g.DrawLine($pen, (Ponto $px $py), (Ponto ($px + $bx * $braco) ($py + $by * $braco)))
  }
}

# Desenha no espaço 1024x1024. $ousado = variante para tamanhos pequenos (traços mais grossos, nós sólidos)
function Desenhar([int]$n, [bool]$ousado) {
  $bmp = [System.Drawing.Bitmap]::new($n, $n, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.ScaleTransform($n / 1024.0, $n / 1024.0)

  if ($ousado) { $ws = 96;  $r = 385; $braco = 105; $wv = 70; $rn = 78; $furo = 0;  $esc = 0.78 }
  else         { $ws = 60;  $r = 385; $braco = 115; $wv = 44; $rn = 76; $furo = 30; $esc = 0.82 }

  # setas: esquerda sobe e aponta para a direita no topo; direita desce e aponta para a esquerda embaixo
  $ps = Novo-Pen $azul $ws
  Desenhar-Seta $g $ps $r 100 165 $braco
  Desenhar-Seta $g $ps $r 280 165 $braco
  $ps.Dispose()

  # diagrama de branches (verde): nó no topo, haste, barra com cantos arredondados, dois nós embaixo
  function T([double]$x, [double]$y) { Ponto (512 + ($x - 512) * $esc) (512 + ($y - 512) * $esc) }
  $yTopo = 315; $yBarra = 500; $yBase = 680; $xE = 362; $xD = 662; $rc = 45
  $pv = Novo-Pen $verde $wv
  $g.DrawLine($pv, (T 512 $yTopo), (T 512 $yBarra))
  $cam = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $cam.AddLine((T $xE $yBase), (T $xE ($yBarra + $rc)))
  $a = T $xE $yBarra; $d = 2 * $rc * $esc
  $cam.AddArc([float]$a.X, [float]$a.Y, [float]$d, [float]$d, 180, 90)
  $b = T ($xD - 2 * $rc) $yBarra
  $cam.AddArc([float]$b.X, [float]$b.Y, [float]$d, [float]$d, 270, 90)
  $cam.AddLine((T $xD ($yBarra + $rc)), (T $xD $yBase))
  $g.DrawPath($pv, $cam); $cam.Dispose(); $pv.Dispose()

  # nós: círculo cheio verde; na versão grande, com furo transparente no meio
  $pincel = [System.Drawing.SolidBrush]::new($verde)
  foreach ($c in @(@(512, $yTopo), @($xE, $yBase), @($xD, $yBase))) {
    $p = T $c[0] $c[1]; $rr = $rn * $esc
    $g.FillEllipse($pincel, [float]($p.X - $rr), [float]($p.Y - $rr), [float](2 * $rr), [float](2 * $rr))
    if ($furo -gt 0) {
      $g.CompositingMode = 'SourceCopy'
      $f = $furo * $esc
      $g.FillEllipse([System.Drawing.Brushes]::Transparent, [float]($p.X - $f), [float]($p.Y - $f), [float](2 * $f), [float](2 * $f))
      $g.CompositingMode = 'SourceOver'
    }
  }
  $pincel.Dispose()

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
