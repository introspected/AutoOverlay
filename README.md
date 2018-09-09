# AutoOverlay AviSynth plugin

### Requirements
- AviSynth 2.6 or higher, AviSynth+. The latest x64 build is recommended: https://github.com/pinterf/AviSynthPlus/releases
- AvsFilterNet plugin https://github.com/mysteryx93/AvsFilterNet/ (included, previous versions do not work correctly)
- .NET framework 4.0 or higher

### Installation
- Copy x86 and/or x64 versions of AvsFilterNet.dll and AutoOverlayNative.dll to the plugins folder.
- Copy AutoOverlay_netautoload.dll to the plugin folder, the same file used for both x86 and x64.

### Description
The plugin is designed for auto-aligned optimal overlay of one video clip onto another.  
The auto-align within OverlayEngine is performed by testing different coordinates (X and Y of the top left corner), resolutions, aspect ratio and rotation angles of the overlay frame in order to find the best combination of these parameters. The function of comparing the areas of two frames is the root-mean-square error (RMSE) on which PSNR is based - but is inversely proportional. The result of this function within the plugin is called simply DIFF since during development other functions were tested, too. The aim of auto-align is to minimize diff value.  
To increase performance, auto-align is divided into several stages of scaling and tests of different OverlayConfigs  which particularly include ranges of acceptable align values. For every OverlayConfig on the first stage all possible combinations are tested in low resolution. On the next, a comparison based on the best align settings from the previous stage. Finally, if the required accuracy is achieved with current OverlayConfig, the remaining are not tested.  
After auto-align it is possible to overlay one frame onto another in different ways with configurable OverlayRender.

### Load plugin in script
    LoadPlugin("%plugin folder%\AvsFilterNet.dll")
    LoadNetPlugin("%plugin folder %\AutoOverlay_netautoload.dll")
AviSynth+ supports plugin auto loading, if .NET plugin filename includes suffix _netautoload. So it includes by default. Check the proper filename in LoadNetPlugin clause.

## Sample
    portrait=ImageSource("Lenna.portrait.jpg").ConvertToYV24()
    landscape=ImageSource("Lenna.landscape.jpg").ConvertToYV24()
    OverlayEngine(portrait, landscape)
    OverlayRender(portrait, landscape, colorAdjust=2, mode=2, gradient=20, \
        width=500, height=500, upsize="Spline64Resize", downsize="Spline64Resize")
    
<details> 
    <summary><b>Portrait image + landscape image -> script output</b></summary>
    <p>
        <img src="https://github.com/introspected/AutoOverlay/blob/master/sample/Lenna.portrait.jpg"/>
        <img src="https://github.com/introspected/AutoOverlay/blob/master/sample/Lenna.landscape.jpg"/>
        <img src="https://github.com/introspected/AutoOverlay/blob/master/sample/Lenna.AutoOverlay.png"/>
    </p>
</details>

## Filters
### OverlayConfig
    OverlayConfig(float minOverlayArea, float minSourceArea, float aspectRatio1, float aspectRatio2, 
                  float angle1, float angle2, int minSampleArea, int requiredSampleArea, float maxSampleDiff, 
                  int subpixel, float scaleBase, int branches, float branchMaxDiff, float acceptableDiff, 
                  int correction, int minX, int maxX, int minY, int maxY, int minArea, int maxArea, 
                  bool fixedAspectRatio, bool debug)
This filter describes configuration for OverlayEngine: how to search optimal align settings. It defines the bounds of align values such as coordinates of the top left corner of overlaid image relative source image, width and height of scaled overlaid image and its rotation angle. Configuration contains OverlayEngine settings for auto-align process as well.  
The filter output is one frame clip that contains given arguments encoded to the image. It is the hack to inject these values to OverlayEngine as independent entity.  
It is possible to add some clips to config chain with regular splice operator: OverlayConfig(…) + OverlayConfig(…). OverlayEngine then will test each config in given order. As soon as acceptable diff value is reached, it will be returned as best align and other configs will not be processed.

#### Parameters
- **minOverlayArea** - minimum intersection part of overlay image in percent. By default: max value if overlay image will be completely contain source image (pan&scan). For example, if source clip is 1920x1080 and overlaid one is 1920x800 then value will be 800/1080 = 74%.
- **minSourceArea** - minimum intersection part of source image in percent. By default: max value if source image will be completely contain overlay image (pan&scan). For example, if source clip is 1440x1080 and overlaid one is 1920x1080 then value will be 1440/1920 = 75%.
- **aspectRatio1** and **aspectRatio2** - range of acceptable aspect ratio of overlaid image. By default – overlay clip aspect ratio. Can be specified in any order: `aspectRatio1=2.35, aspectRatio2=2.45` is the same as `aspectRatio1=2.45, aspectRatio2=2.35`.
- **angle1** и **angle2** (default 0) - range of acceptable rotation angle if overlaid image. Can be specified in any order. Negative values – clockwise rotation, positive – counter.
- **minSampleArea** (default 1000) – minimum area in pixels of downsized source clip at first scale stage. The smaller, the faster, but also the greater risk of getting an incorrect result. Recommended range: 500-3000. 
- **requiredSampleArea** (default 1000) - maximum area in pixels of downscaled source clip at first scale stage. The smaller, the faster, but also the greater risk of getting an incorrect result. Recommended range: 1000-5000. 
- **maxSampleDiff** (default 5) – max allowed DIFF between downscaled source clip at previous and current scale iteration. If it is too high then previous iteration will not be processed.  
- **subpixel** (default 0) – subpixel align accuracy. 0 – one pixel accuracy, 1 – half pixel, 2 – quarter and so on. Zero is recommended if one clip is much smaller then another. 1-3 if both clips have about the same resolution. 
- **scaleBase** (default 1.5) – base to calculate downscale coefficient by formula `coef=scaleBase^(1 - (maxStep - currentStep))`.
- **branches** (default 1) - how many best align params from previous scale stage will be analyzed at the next one.  1-10 is recommended. The higher - the longer.
- **branchMaxDiff** - maximum difference between best branch and current one to accept
- **acceptableDiff** (default 5) – acceptable DIFF value. If it will be achieved with current OverlayConfig the remaining configs in the chain will not be processed.
- **correction** (default 1) – the value of correction the best align params between scale stages. The higher, then more different align params are tested. 
- **minX**, **maxX**, **minY**, **maxY** - ranges of top left corner coordinates, unlimited by default.
- **minArea**, **maxArea** - area range of overlaid image in pixels, unlimited by default.
- **fixedAspectRatio** (default false) - maximum accurate aspect ratio. May be true only if aspectRatio1=aspectRatio2.
- **debug** (default false) – output given arguments, slower.

### OverlayEngine
    OverlayEngine(clip, clip, string statFile, int backwardFrames, int forwardFrames, 
                  clip sourceMask, clip overlayMask, float maxDiff, float maxDiffIncrease, 
                  float maxDeviation, bool stabilize, clip configs, string downsize, string upsize, 
                  string rotate, bool editor, string mode, bool debug)
Filter takes two clips: source and overlay. It performs auto-align by resizing, rotation and shifting an overlay clip to find the best diff value. Best align settings are encoded into output frame so it could be read by another filter. The sequence of these align settings frame-by-frame (statistics) could be written to in-memory cache or file for reuse and manual editing via built-in visual editor. 
#### Parameters
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip. Both clips must be in the same color space without chroma subsampling. Y8, YV24 and RGB24 are supported.
- **statFile** (default empty) – path to the file with current align values. If empty then in-memory cache is used. Recommended use-case: to test different OverlayConfigs use in-memory cache. After that set statFile and run analysis pass.
- **backwardFrames** and **forwardFrames** (default 3) – count of previous and forward frames for searching align params of current frame. 
- **sourceMask**, **overlayMask** (default empty) – mask clips for source and overlaid clips. If mask was specified then pixels that correspond to zero values in mask will be excluded from comparison function. In RGB color space all channels are analyzed independent. In YUV only luma channel is analyzed. Masks and clips should be in the same type color space (YUV or RGB24). Note that in planar color space masks should be in full range (`ColorYUV(levels="TV->PC")`). 
- **maxDiff** (default 5) – diff value below which auto-align is marked as correct.
- **maxDiffIncrease** (default 1) – maximum allowed diff increasing in the frame sequence.
- **maxDeviation** (default 0.5) – max allowed rest area of clip in percent subtracting intersection to detect scene change.
- **stabilize** (default true) – attempt to stabilize align values at the beginning of scene when there is no enough backward frames including to current scene.
- **configs** (default OverlayConfig with default values) – configuration list in the form of clip. Example: `configs=OverlayConfig(subpixel=1, acceptableDiff=10) + OverlayConfig(angle1=-1, angle2=1)`. If during analyses of first configuration it founds align values with diff below 10 then second one with high ranges and low performance will be skipped.
- **downsize** and **upsize** (default *BicubicResize*) – functions that used for downsizing and upsizing of overlaid clip during auto-align.
- **rotate** (default *BilinearRotate*) – rotation function. Currently built-in one is used which based on AForge.NET.
- **editor** (default false). If true then visual editor form will be open at the script loading. 
- **mode** (default "default") – engine mode:  
DEFAULT – default mode  
UPDATE – as default but with forced diff update   
ERASE – erase statistics  
READONLY – use only existing align statistics 
- **debug** (default false) - output align values, slower.

#### How it works
The engine works out optimal align values: coordinates of overlaid clip’s top left corner (x=0,y=0 is the left top corner of source clip), rotation angle in degrees, overlaid width and height, floating-point crop values from 0 to 1 for subpixel accuracy (left, top, right, bottom) and DIFF value.  
*OverlayConfig* chain provides ranges of acceptable align values. The engine runs each config one-by-one while align values with acceptable diff would not be found. Auto-align process for each configuration includes multiple scaling stages. At the first stage all possible combinations of align values are tested in the lowest resolution. The count of best align value sets passed to the next stage is given by `OverlayConfig.branches` parameter. At the next stages best sets from previous are corrected. Note that, simplified, one pixel in low resolution includes four from high. *OverlayConfig* contains `correction` parameter to set correction limit for all directions and sizes in pixels.  
Image resizing is performed by functions from *downsize* and *upsize* parameters. First one is commonly used at the intermediate scale stages, last one at the final. So it’s recommended to use something better than BilinearResize at the end. The functions must have signature as follow: `Resize(clip clip, int target_width, int target_height, float src_left, float src_top, float src_width, float src_height)`. Additional parameters are allowed, that is, the signature corresponds to standard resize functions. It is recommended to use the ResampleMT plugin, which provides the same functions but internally multithreaded.  
Besides auto-align of given frame the engine analyses sequence of neighboring frames is specified by *backwardFrames* and *forwardFrames* parameters according to *maxDiff, maxDiffIncrease, maxDeviation, stabilize* params as described below:  
- Request align values of current frame.
- Return data from statFile or in-memory cache if this frame was auto-aligned already.
- If previous frames in quantity of *backwardFrames* are already aligned with the same values and their DIFF don’t exceed *maxDiff* value, then the same align values will be tested for the current frame. 
- If maximum DIFF is increasing within sequence is lower than *maxDiffIncrease*, then the engine analyzes next frames in quantity of *forwardFrames* otherwise current frame will be marked as independent from previous – that means scene change.
- If current frame was included to the sequence with previous frames then the same align values will be tested on forward frames if *forwardFrames* is greater than zero. If all next frames are good at the same align then current frame will be included to the sequence. 
- If some of next frames exceed *maxDiff* value or difference between next frame diff and average diff is higher than *maxDiffIncrease* then the engine will work out optimal auto-align values for next frame. If new align values significantly differ from previous then we have scene change otherwise there is pan&scan episode and each frame should be processed independently. The parameter that used for this decision is *maxDeviation*.
- If current frame is marked as independent after previous operations and *OverlayEngine.stablize=true* then engine tries to find equal align params for the next frames too to start new scene.
- If *backwardFrames* is equal to zero then each frame is independent. This scenario has very low performance and may cause image shaking. Use this only if you have two different original master copies from which the sources were made. 

##### Visual editor
Displayed when *OverlayEngine.editor*=true. It is useful after analysis pass when statFile is specified to check all scenes and manually correct incorrect aligned frames.  
Preview from the left side. Below there is trackbar by total frame count and current frame input field. Grid with equal aligned frame sequences on the right. It is possible to move between scenes by row selection. Below grid there is a control panel.  
*Overlay settings* contains controls to change align values of current scene. *Crop* fields in pro mil px from 0 to 1000.  
Below there is *AutoOverlay* section.  
*Single frame* button performs auto-align based on the current frame. The result is applied to the full scene. *Separated frame* button performs auto-align based on the current frame. If new align values is differ from the scene then current frame is separated.  *Scene* button performs auto-align for each frame in the scene independently.  
On the right there are controls to process pan&scan scenes. Before using it join pan&scan frame sequence into one scene (one row in grid) and press *Single frame* for the first frame in the sequence. After that set maximum *distance* in pixels of shifting and resizing limit in pro mil for each next frame according to previous one. The click to *Pan&scan* button to process the whole scene. The progress will be displayed at the form caption.  
Below there is *Display settings*. It includes the same settings as OverlayRender filter: output resolution, overlay mode, gradient and noise length, opacity in percent, *preview* and *display info* checkboxes. If preview is disabled then source clip is displayed.  
At the bottom of control panel there are button panel.  
The scenes that were changed by user are highlighted with yellow color in grid. *Save* button will apply changes to statFile. *Reset* will reload all data from file. *Reload* will reload data only for current frame and apply it to while scene.  
*Separate* will exclude the current frame from scene. *Join prev* will join previous scene to current and overwrite align values. *Join next* will join next scene to current and overwrite align values. *Join to* will join current scene to specified frame and overwrite align values.  

**Hotkeys**:
* Ctrl + S - save
* Ctrl + R - reload
* D - enable/disable difference
* P - enable/disable preview
* Ctrl + arrow keys - move overlay image
* Ctrl + add/subtract - scale overlay image
* A, Z - next/previous frame

### OverlayRender
    OverlayRender(clip, clip, clip, clip sourceMask, clip overlayMask, bool lumaOnly, int width, int height, 
                  int gradient, int noise, bool dynamicNoise, int mode, float opacity, int colorAdjust, 
                  string matrix, string upsize, string downsize, string rotate, bool debug)
This filter preforms rendering of the result clip using align values from the engine. 

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip. Both clips must be in the same color space without chroma subsampling. Y8, YV24 and RGB24 are supported.
- **sourceMask**, **overlayMask** (default empty) - mask clips for source and overlaid clips. Unlike masks in overlay engine in this filter they work as avisynth built-in overlay filter masks. If both masks are specified then they are joined to one according align values. The purpose of the masks is maximum hiding of logos and other unwanted parts of the clips at transparent or gradient parts, which could be specified by *gradient* and *noise* parameters. 
- **lumaOnly** (default false) – overlay only luma channel when *mode*=1.
- **width** и **height** - output width and height. By default are taken from source clip.
- **gradient** (default 0) – length of gradient at the overlay mask to make borders between images smoother. Useful when clips are scientifically differ in colors or align is not very good.
- **noise** (default 0) - length of "noise gradient" at the overlay mask to make borders between images smoother. Useful when clips are in the same color and align is very good.
- **dynamicNoise** (default true) – use dynamic noise gradient if *noise* > 0.
- **mode** (default 1) – overlay and cropping mode:  
1 - crop around the edges of the source image.  
2 - combination of both images with cropping to the all four edges of the output clip with "ambilight" at the corners.  
3 - combination of both images without any cropping.  
4 - as 3 but with "ambilight" at the corners.  
5 - as 3 but with "ambilight" at all free space.  
6 – as 3 but with pure white clips. Useful to combine the result clip with another one.  
7 - as 1 but clips overlaid in difference mode. Useful to check align.
- **opacity** (default 1) – opacity of overlaid image from 0 to 1.
- **colorAdjust** (default 0) *unstable* - color adjustment mode:  
0 - disabled  
1 – overlaid clip will be adjusted to source  
2 - source clip will be adjusted to overlaid  
3 - intermediate clip
Color adjustment performs by histogram matching in the intersection area. For the YUV images adjusts only luma channel. For the RGB images adjusts all 3 channels independently.
- **matrix** (default empty). If specified YUV image converts to RGB with given matrix for color adjustment. 
- **downsize** и **upsize** (default *BicubicResize*) - downsize and upsize functions. It’s recommended to use functions with high quality interpolation. 
- **rotate** (default *BilinearRotate*) – rotation function.
- **debug** -  output align settings to the top left corner.

### ColorAdjust
    ColorAdjust(clip, clip, clip, clip sampleMask, clip referenceMask, bool limitedRange, string channels, float dither)
Color matching. Input clip, sample clip and reference clip must be in the same type of color space (YUV or RGB) and matrix. HDR support. Input clip and sample clip must have the same bit depth (usually sample is the cropped or resized input clip). The bit depth of output clip will changed to the reference one. The filter provides perfect mathing only if sample and reference are represent the same area of frame while the input (first argument) may have different framing. This filter is used inside OverlayRender but it is also useful as standalone. 

#### Parameters
- **clip** (required) - clip for color adjustment
- **sample** (required) - the sample clip (usually the first clip or cropped) 
- **reference** (required) - reference clip (usually the same time and area from different master)
- **sampleMask** (default empty) - mask clip to exclude some parts from sample (8 bit planar (Y plane only is used) or RGB24)
- **referenceMask** (default empty) - mask clip to exclude some parts from reference (8 bit planar (Y plane only is used) or RGB24)
- **limitedRange** (default true) - TV or PC range for YUV clips
- **channels** (default yuv or rgb) - planes to process for YUV clips or channels for RGB. Any combination of y,u,v and r,g,b supported (ex: y, uv, r, br).
- **dither** (default 0.95) - dither level from 0 (disable) to 1 (aggressive). 

### OverlayCompare
OverlayCompare(clip, clip, clip, string sourceText, string overlayText, int sourceColor, int overlayColor, 
               int borderSize, float opacity, int width, int height, bool debug)
This filter generates comparison clip from source and overlay clips with borders and labels.

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **sourceText** (default "Source") - source clip name.
- **overlayText** (default "Source") - overlay clip name.
- **sourceColor** (default 0x0000FF) - source clip border color.
- **overlayColor** (default 0x00FF00) - overlay clip border color.
- **borderSize** (default 2) - border size.
- **opacity** (default 0.51) - opacity of overlay clip.
- **width** (source clip width by default) - output width.
- **height** (source clip height by default) - output height.
- **debug** (default false) - print align settings. 

### StaticOverlayRender
    StaticOverlayRender(clip, clip, int x, int y, float angle, int overlayWidth, int overlayHeight, 
                        float cropLeft, float cropTop, float cropRight, float cropBottom, float diff, 
                        clip sourceMask, clip overlayMask, bool lumaOnly, int width, int height, 
                        int gradient, int noise, bool dynamicNoise, int mode, float opacity, int colorAdjust, 
                        string matrix, string upsize, string downsize, string rotate, bool debug)
As OverlayRender but with fixed align settings without OverlayEngine.

#### Parameters
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip. Both clips must be in the same color space without chroma subsampling. Y8, YV24 and RGB24 are supported.
- **x** (required) - x coordinate.
- **y** (required) - y coordinate.
- **angle** (default 0) - rotation angle.
- **overlayWidth** (overlay clip width by default) - width of overlay clip after resize.
- **overlayHeight** (overlay clip height by default) - height of overlay clip after resize.
- **cropLeft**, **cropTop**, **cropRight**, **cropBottom** (default 0) - crop overlay clip before resize for subpixel alignment.
- **diff** (default 0) - DIFF value for debug output. 
- **sourceMask**, **overlayMask** (default empty) - mask clips for source and overlaid clips. Unlike masks in overlay engine in this filter they work as avisynth built-in overlay filter masks. If both masks are specified then they are joined to one according align values. The purpose of the masks is maximum hiding of logos and other unwanted parts of the clips at transparent or gradient parts, which could be specified by *gradient* and *noise* parameters. 
- **lumaOnly** (default false) – overlay only luma channel when *mode*=1.
- **width** и **height** - output width and height. By default are taken from source clip.
- **gradient** (default 0) – length of gradient at the overlay mask to make borders between images smoother. Useful when clips are scientifically differ in colors or align is not very good.
- **noise** (default 0) - length of "noise gradient" at the overlay mask to make borders between images smoother. Useful when clips are in the same color and align is very good.
- **dynamicNoise** (default true) – use dynamic noise gradient if *noise* > 0.
- **mode** (default 1) – overlay and cropping mode:  
1 - crop around the edges of the source image.  
2 - combination of both images with cropping to the all four edges of the output clip with "ambilight" at the corners.  
3 - combination of both images without any cropping.  
4 - as 3 but with "ambilight" at the corners.  
5 - as 3 but with "ambilight" at all free space.  
6 – as 3 but with pure white clips. Useful to combine the result clip with another one.  
7 - as 1 but clips overlaid in difference mode. Useful to check align.
- **opacity** (default 1) – opacity of overlaid image from 0 to 1.
- **colorAdjust** (default 0) *unstable* - color adjustment mode:  
0 - disabled  
1 – overlaid clip will be adjusted to source  
2 - source clip will be adjusted to overlaid  
3 - intermediate clip
Color adjustment performs by histogram matching in the intersection area. For the YUV images adjusts only luma channel. For the RGB images adjusts all 3 channels independently.
- **matrix** (default empty). If specified YUV image converts to RGB with given matrix for color adjustment. 
- **downsize** и **upsize** (default *BicubicResize*) - downsize and upsize functions. It’s recommended to use functions with high quality interpolation. 
- **rotate** (default *BilinearRotate*) – rotation function.
- **debug** -  output align settings to the top left corner.

### CustomOverlayRender
    CustomOverlayRender(clip, clip, clip, string function, int width, int height, bool debug)
This filter allows to override default overlay algorithms by user using overlay settings from OverlayEngine. 

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **function** (required) - user function name. The function must have the following parameters: `clip engine, clip source, clip overlay, int x, int y, float angle, int overlayWidth, int overlayHeight, float cropLeft, float cropTop, float cropRight, float cropBottom, float diff)`
- **width** (source clip width by default) - output clip width.
- **height** (source clip height by default) - output clip height.
- **debug** (default false) - debug mode.

### ColorRangeMask
    ColorRangeMask(clip, int low, int high)
Support filter which provides mask clip by color range: white if pixel value is between low and high arguments. For YUV clips only luma channel is used. For RGB clips all channels are proccessed independently. Output is the clip in the same color space. Limited range is not supported. 

#### Parameters
- **input** (required) - input clip.
- **low** (default 0) - lower bound of color range.
- **high** (default 0) - higher bound of color range.

### BilinearRotate
    BilinearRotate(clip, float)
Support filter for rotation by angle with bilinear interpolation.

#### Parameters
- **input** (required) - input clip.
- **angle** (required) - rotation angle.

### OverlayMask
    OverlayMask(clip template, int width, int height, 
                int left, int top, int right, int bottom, bool noise, bool gradient, int seed)
Support filter which provides mask clip for overlay with gradient or noise at borders.

#### Parameters
- **template** (default empty) - if specified width, height and color space will be used from template clip for output.
- **width** - output clip width if template is not specified. 
- **height** - output clip height if template is not specified.
- **left**, **top**, **right**, **bottom** - border size.
- **noise** - noise generation on borders.
- **gradient** - gradient borders.
- **seed** - seed for noise generation.


## Examples
#### Simple script 
    OM=AviSource("c:\test\OpenMatte.avs") # YV12 clip
    WS=AviSource("c:\test\Widescreen.avs") # YV12 clip
    OverlayEngine(OM.ConvertToY8(), WS.ConvertToY8(), configs=OverlayConfig(subpixel=2)) 
    OverlayRender(OM.ConvertToYV24(), WS.ConvertToYV24(), debug=true)
    ConvertToYV12()
#### Analysis pass without render. Aspect ratio range was specified. Set editor=true after analysis pass.
    OM=AviSource("c:\test\OpenMatte.avs") # YV12 clip
    WS=AviSource("c:\test\Widescreen.avs") # YV12 clip
    config=OverlayConfig(aspectRatio1=2.3, aspectRatio2=2.5)
    OverlayEngine(OM.ConvertToY8(), WS.ConvertToY8(), configs=config, statFile="c:\test\Overlay.stat", editor=false)
#### Render after analysis pass
    OM=AviSource("c:\test\OpenMatte.avs") # YV12 clip
    WS=AviSource("c:\test\Widescreen.avs") # YV12 clip
    OverlayEngine(OM.ConvertToY8(), WS.ConvertToY8(), statFile="c:\test\Overlay.stat")
    OverlayRender(OM.ConvertToYV24(), WS.ConvertToYV24(), debug=true, noise=50, upsize="Spline64Resize")
    ConvertToYV12()
#### Color matching
    HDR=AviSource("c:\test\UHD_HDR.avs") # Stacked 2160p YUV420P16 Rec2020 clip
    HDR=HDR.ConvertFromStacked() # HDR video should be unstacked
    HDR=HDR.z_ConvertFormat(pixel_type="YUV420P10",colorspace_op="2020ncl:st2084:2020:l=>709:709:709:l",dither_type="none") # Convert Rec2020 to Rec709 using z.lib
    SDR=AviSource("c:\test\FullHD_SDR.avs") # 1080p YUV420P10 Rec709 clip
    HDR.ColorAdjust(HDR, SDR) # Output is 2160p YV12 Rec709 clip
