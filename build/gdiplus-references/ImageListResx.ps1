# Writes .resx files holding an ImageList's ImageStream, a Bitmap and an Icon, as Visual Studio's designer writes
# them on Windows with the .NET Framework, for the Forms layer's resource tests. Run with Windows PowerShell 5.1:
#   powershell -ExecutionPolicy Bypass -File build/gdiplus-references/ImageListResx.ps1 <output folder>
param([string]$Output = "resx")
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
New-Item -ItemType Directory -Force -Path $Output | Out-Null

function New-Image([System.Drawing.Color]$color, [int]$kind) {
    $bitmap = New-Object System.Drawing.Bitmap 16, 16, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.Clear([System.Drawing.Color]::Transparent)
    $brush = New-Object System.Drawing.SolidBrush $color
    switch ($kind) {
        0 { $g.FillRectangle($brush, 2, 2, 12, 12) }
        1 { $g.FillEllipse($brush, 1, 1, 14, 14) }
        default { $g.FillPolygon($brush, [System.Drawing.Point[]]@((New-Object System.Drawing.Point 8, 1), (New-Object System.Drawing.Point 15, 15), (New-Object System.Drawing.Point 1, 15))) }
    }
    $g.Dispose()
    return $bitmap
}

$images = @((New-Image ([System.Drawing.Color]::Red) 0), (New-Image ([System.Drawing.Color]::Lime) 1), (New-Image ([System.Drawing.Color]::Blue) 2), (New-Image ([System.Drawing.Color]::Orange) 0), (New-Image ([System.Drawing.Color]::Purple) 1))
for ($i = 0; $i -lt $images.Length; $i++) { $images[$i].Save((Join-Path $Output "image$i.png"), [System.Drawing.Imaging.ImageFormat]::Png) }

foreach ($depth in @("Depth32Bit", "Depth8Bit", "Depth24Bit")) {
    $list = New-Object System.Windows.Forms.ImageList
    $list.ColorDepth = $depth
    $list.ImageSize = New-Object System.Drawing.Size 16, 16
    $list.TransparentColor = [System.Drawing.Color]::Transparent
    foreach ($image in $images) { [void]$list.Images.Add($image) }
    $writer = New-Object System.Resources.ResXResourceWriter (Join-Path (Resolve-Path $Output) "imagelist-$depth.resx")
    $writer.AddResource("imageList1.ImageStream", $list.ImageStream)
    $writer.AddResource("pictureBox1.Image", $images[1])
    $icon = [System.Drawing.Icon]::FromHandle($images[2].GetHicon())
    $writer.AddResource('$this.Icon', $icon)
    $writer.AddResource("label1.Location", (New-Object System.Drawing.Point 12, 34))
    $writer.AddResource("label1.Text", "Hello")
    $writer.Generate()
    $writer.Close()
    # What the ImageList itself holds once drawn, for the tests to compare with.
    for ($i = 0; $i -lt $list.Images.Count; $i++) {
        $list.Images[$i].Save((Join-Path $Output "$depth-image$i.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
}
Get-ChildItem $Output | ForEach-Object { $_.Name }
