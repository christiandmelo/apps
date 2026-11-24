param([string]$Saida)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Saida | Out-Null

$azul  = [System.Drawing.Color]::FromArgb(255, 17, 112, 198)
$verde = [System.Drawing.Color]::FromArgb(255, 53, 152, 98)

# Desenha no espaço 1024x1024. $ousado = variante para tamanhos pequenos (traço mais grosso, menos margem)
function Desenhar([int]$n, [bool]$ousado) {
  $bmp = [System.Drawing.Bitmap]::new($n, $n, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'; $g.CompositingQuality = 'HighQuality'
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.ScaleTransform($n / 1024.0, $n / 1024.0)

  if ($ousado) { $w = 150; $ext = 64; $braco = 330; $cw = 140 } else { $w = 55; $ext = 205; $braco = 270; $cw = 48 }
  $r = $w / 2.0
  $a = $ext + $w / 2.0          # linha central da moldura
  $b = 1024 - $a
  $L = $braco - $w              # fim do braço (linha central) para a ponta arredondada terminar em ext+braco

  $pen = [System.Drawing.Pen]::new($azul, $w)
  $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'

  # canto superior esquerdo com arco de raio w/2 na linha central (externo arredondado, interno reto);
  # os outros três cantos são espelhamentos em torno do centro (512,512)
  $base = [System.Drawing.Drawing2D.GraphicsPath]::new()
  $base.AddLine([float]($a + $L), [float]$a, [float]($a + $r), [float]$a)
  $base.AddArc([float]$a, [float]$a, [float]($w), [float]($w), 270, -90)
  $base.AddLine([float]$a, [float]($a + $r), [float]$a, [float]($a + $L))
  foreach ($esp in @(@(1,1), @(-1,1), @(1,-1), @(-1,-1))) {
    $p = $base.Clone()
    $m = [System.Drawing.Drawing2D.Matrix]::new([float]$esp[0], 0, 0, [float]$esp[1], [float](512 - 512*$esp[0]), [float](512 - 512*$esp[1]))
    $p.Transform($m)
    $g.DrawPath($pen, $p); $p.Dispose(); $m.Dispose()
  }
  $base.Dispose()

  # check (linha central estimada da imagem original, escalada em torno do centro)
  $esc = if ($ousado) { 1.55 } else { 1.0 }
  $pts = @(@(420,515), @(478,573), @(605,447)) | ForEach-Object {
    [System.Drawing.PointF]::new([float](512 + ($_[0]-512)*$esc), [float](512 + ($_[1]-510)*$esc))
  }
  $pc = [System.Drawing.Pen]::new($verde, $cw)
  $pc.StartCap = 'Round'; $pc.EndCap = 'Round'; $pc.LineJoin = 'Round'
  $g.DrawLines($pc, [System.Drawing.PointF[]]$pts)

  $g.Dispose(); $pen.Dispose(); $pc.Dispose()
  return $bmp
}

# PNG mestre
$mestre = Desenhar 1024 $false
$mestre.Save((Join-Path $Saida 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$mestre.Dispose()

# Entradas do ICO: <=48 em BMP (32bpp + máscara AND), 256 em PNG
$tamanhos = 16, 24, 32, 48, 256
$entradas = foreach ($n in $tamanhos) {
  $bmp = Desenhar $n ($n -le 32)
  $bmp.Save((Join-Path $Saida "preview-$n.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  if ($n -eq 256) {
    $ms = [IO.MemoryStream]::new(); $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $dados = $ms.ToArray()
  } else {
    $ms = [IO.MemoryStream]::new(); $bw = [IO.BinaryWriter]::new($ms)
    $bw.Write([int]40); $bw.Write([int]$n); $bw.Write([int]($n*2)); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $n - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $n; $x++) {
      $c = $bmp.GetPixel($x, $y); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) } }
    $stride = [int]([math]::Ceiling($n / 32.0) * 4)
    $bw.Write([byte[]]::new($stride * $n))   # máscara AND zerada (alpha manda)
    $bw.Flush(); $dados = $ms.ToArray()
  }
  $bmp.Dispose()
  [pscustomobject]@{ N = $n; Dados = $dados }
}

$ico = [IO.MemoryStream]::new(); $w = [IO.BinaryWriter]::new($ico)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$entradas.Count)
$offset = 6 + 16 * $entradas.Count
foreach ($e in $entradas) {
  $dim = if ($e.N -ge 256) { 0 } else { $e.N }
  $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
  $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$e.Dados.Length); $w.Write([int]$offset)
  $offset += $e.Dados.Length
}
foreach ($e in $entradas) { $w.Write($e.Dados) }
$w.Flush()
[IO.File]::WriteAllBytes((Join-Path $Saida 'favicon.ico'), $ico.ToArray())

# validação: carregar com System.Drawing.Icon
$i = [System.Drawing.Icon]::new((Join-Path $Saida 'favicon.ico'), 16, 16); "ok 16: $($i.Width)"; $i.Dispose()
$i = [System.Drawing.Icon]::new((Join-Path $Saida 'favicon.ico'), 256, 256); "ok 256: $($i.Width)"; $i.Dispose()

# folha de preview em fundo claro e escuro
$folha = [System.Drawing.Bitmap]::new(560, 300)
$g = [System.Drawing.Graphics]::FromImage($folha); $g.InterpolationMode = 'NearestNeighbor'
$g.FillRectangle([System.Drawing.Brushes]::White, 0, 0, 560, 150)
$g.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(32,32,32)), 0, 150, 560, 150)
$x = 10
foreach ($n in 16, 24, 32, 48) {
  $im = [System.Drawing.Image]::FromFile((Join-Path $Saida "preview-$n.png"))
  $g.DrawImage($im, $x, 10, $n, $n); $g.DrawImage($im, $x, 160, $n, $n)       # tamanho real
  $g.DrawImage($im, $x, 60, 80, 80); $g.DrawImage($im, $x, 210, 80, 80)         # ampliado
  $im.Dispose(); $x += 100
}
$im = [System.Drawing.Image]::FromFile((Join-Path $Saida 'preview-256.png'))
$g.DrawImage($im, 420, 10, 130, 130); $g.DrawImage($im, 420, 160, 130, 130); $im.Dispose()
$g.Dispose(); $folha.Save((Join-Path $Saida 'folha-preview.png')); $folha.Dispose()
