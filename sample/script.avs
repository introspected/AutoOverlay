portrait=ImageSource("E:\DEV\AutoOverlay\sample\Lenna.portrait.jpg").ConvertToYV24()
landscape=ImageSource("E:\DEV\AutoOverlay\sample\Lenna.landscape.jpg").ConvertToYV24()

OverlayEngine(portrait, landscape)
OverlayRender(portrait, landscape, colorAdjust=2, mode=2, gradient=20, width=500, height=500, upsize="Spline64Resize", downsize="Spline64Resize")