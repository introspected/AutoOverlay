portrait=ImageSource("Lenna.portrait.jpg")
landscape=ImageSource("Lenna.landscape.jpg")

OverlayEngine(portrait, landscape)
OverlayRender(portrait, landscape, colorAdjust=1, outerBounds=Rect(1), innerBounds=Rect(1), \
              background="blur", edgeGradient="full", gradient=80, width=500, height=500)