# AutoOverlay AviSynth plugin

### Requirements
- AviSynth+ 3.7+: https://github.com/AviSynth/AviSynthPlus/releases/
- AvsFilterNet plugin https://github.com/Asd-g/AvsFilterNet (included)
- SIMD Library https://github.com/ermig1979/Simd (included)
- warp plugin v0.1b by wonkey_monkey https://forum.doom9.org/showthread.php?t=176031 (included)
- Math.NET Numerics (included)
- .NET framework 4.6.1+
- Windows 7+

Windows XP and previous versions of AviSynth are supported only before v0.2.5.

### Installation
- Copy x86/x64 DLL's to AviSynth plugins folder.
- DLL's may be blocked after downloading. Open file properties to unblock them. 

### Description
The plugin is designed for auto-aligned optimal overlay of one video clip onto another.  
The auto-align within OverlayEngine is performed by testing different coordinates (X and Y of the top left corner), resolutions, aspect ratio and rotation angles of the overlay frame in order to find the best combination of these parameters. The function of comparing the areas of two frames is the root-mean-square error (RMSE) on which PSNR is based - but is inversely proportional. The result of this function within the plugin is called simply DIFF since during development other functions were tested, too. The aim of auto-align is to minimize diff value.  
To increase performance, auto-align is divided into several stages of scaling and tests of different OverlayConfigs which particularly include ranges of acceptable align values. For every OverlayConfig on the first stage all possible combinations are tested in low resolution. On the next, a comparison based on the best align settings from the previous stage. Finally, if the required accuracy is achieved with current OverlayConfig, the remaining are not tested.  
After auto-align it is possible to overlay one frame onto another in different ways with configurable OverlayRender.

### Load plugin in script
    LoadPlugin("%plugin folder%\AvsFilterNet.dll")
    LoadNetPlugin("%plugin folder %\AutoOverlay_netautoload.dll")
AviSynth+ supports plugin auto loading, if .NET plugin filename includes suffix `_netautoload`. So it includes by default. Check the proper filename in LoadNetPlugin clause.

## Sample
    portrait=ImageSource("Lenna.portrait.jpg").ConvertToYV12() # Regular YV12 clip
    landscape=ImageSource("Lenna.landscape.jpg").ConvertToYV12()
    OverlayEngine(portrait, landscape)
    OverlayRender(portrait, landscape, colorAdjust=1, mode=2, gradient=20, width=500, height=500)
    
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
                  float angle1, float angle2, clip warpPoints, int warpSteps, int warpOffset, 
				  int minSampleArea, int requiredSampleArea, float maxSampleDiff, 
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
- **warpPoints** (default empty) – Rect clip series which describes warp transformation source points and max X and Y offsets that will be passed to the warp filter. Example: Rect(0,0,3,3) + Rect(1920,800,3,3) + Rect(1920,0,3,3) + Rect(0,800,3,3) + Rect(960,400,3,3). It describes warp transformations 3px around corners and center of overlay image (1920x800).
- **warpSteps** (default 3) – warp transformation iteration count. Greater is better but slower. 
- **warpOffset** (default 0) – warp transformation step offset from last step (to process in low resolution). Lower is better but slower. 
- **minSampleArea** (default 1500) – minimum area in pixels of downsized source clip at first scale stage. The smaller, the faster, but also the greater risk of getting an incorrect result. Recommended range: 500-3000. 
- **requiredSampleArea** (default 3000) - maximum area in pixels of downscaled source clip at first scale stage. The smaller, the faster, but also the greater risk of getting an incorrect result. Recommended range: 1000-5000. 
- **maxSampleDiff** (default 5) – max allowed DIFF between downscaled source clip at previous and current scale iteration. If it is too high then previous iteration will not be processed.  
- **subpixel** (default 0) – subpixel align accuracy. 0 – one pixel accuracy, 1 – half pixel, 2 – quarter and so on. Zero is recommended if one clip is much smaller then another. 1-3 if both clips have about the same resolution. Negative values are also supported.
- **scaleBase** (default 1.5) – base to calculate downscale coefficient by formula `coef=scaleBase^(1 - (maxStep - currentStep))`.
- **branches** (default 1) - how many best align params from previous scale stage will be analyzed at the next one.  1-10 is recommended. The higher - the longer.
- **branchMaxDiff** (default 0.2) - maximum difference between best branch and current one to accept
- **acceptableDiff** (default 5) – acceptable DIFF value. If it will be achieved with current OverlayConfig the remaining configs in the chain will not be processed.
- **correction** (default 1) – the value of correction the best align params between scale stages. The higher, then more different align params are tested. 
- **minX**, **maxX**, **minY**, **maxY** - ranges of top left corner coordinates, unlimited by default.
- **minArea**, **maxArea** - area range of overlaid image in pixels, unlimited by default.
- **fixedAspectRatio** (default false) - maximum accurate aspect ratio. May be true only if aspectRatio1=aspectRatio2.
- **debug** (default false) – output given arguments, slower.

### OverlayEngine                  
    OverlayEngine(clip source, clip overlay, string statFile, int backwardFrames, int forwardFrames, 
                  clip sourceMask, clip overlayMask, float maxDiff, float maxDiffIncrease, float maxDeviation, 
                  int panScanDistance, float panScanScale, bool stabilize, clip configs, string presize, 
                  string resize, string rotate, bool editor, string mode, float colorAdjust, 
				  string sceneFile, bool simd, bool debug)

Filter takes two clips: source and overlay. It performs auto-align by resizing, rotation and shifting an overlay clip to find the best diff value. Best align settings are encoded into output frame so it could be read by another filter. The sequence of these align settings frame-by-frame (statistics) could be written to in-memory cache or file for reuse and manual editing via built-in visual editor. 
#### Parameters
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip. Both clips must be in the same color space. Any planar YUV (8-16 bit), RGB24 and RGB48 color spaces are supported.
- **statFile** (default empty) – path to the file with current align values. If empty then in-memory cache is used. Recommended use-case: to test different OverlayConfigs use in-memory cache. After that set statFile and run analysis pass. If the file from previous plugin version was specified it will be saved and then converted to the new version. 
- **backwardFrames** and **forwardFrames** (default 3) – count of previous and forward frames for searching align params of current frame. 
- **sourceMask**, **overlayMask** (default empty) – mask clips for source and overlaid clips. If mask was specified then pixels that correspond to zero values in mask will be excluded from comparison function. In RGB color space all channels are analyzed independent. In YUV only luma channel is analyzed. Masks and clips should be in the same type color space. Note that in planar color space masks should be in full range (`ColorYUV(levels="TV->PC")`). 
- **maxDiff** (default 5) – diff value below which auto-align is marked as correct. It is used for scene detection. 
- **maxDiffIncrease** (default 1) – maximum allowed diff increasing in the frame sequence.
- **maxDeviation** (default 1) – maximum allowed difference in percent between union and intersection area of two align configurations to detect scene change. Lower values prevent scene detection. Higher values may cause wrong scenes but more stable overlay params.
- **panScanDistance** (default 0) – maximum allowed shift in pixels between two frames in scene. Use it only if source clips are not stabilized to each other. 
- **panScanScale** (default 3) – maximum allowed overlay clip resolution scaling in ppm between two frames in scene.
- **stabilize** (default true) – attempt to stabilize align params at the beginning of scene when there is no enough backward frames including to current scene. If true `panScanDistance` should be 0.
- **configs** (default OverlayConfig with default values) – configuration list in the form of clip. Example: `configs=OverlayConfig(subpixel=1, acceptableDiff=10) + OverlayConfig(angle1=-1, angle2=1)`. If during analyses of first configuration it founds align values with diff below 10 then second one with high ranges and low performance will be skipped.
- **presize** (default *BilinearResize*) – resize function for steps with less than one pixel accuracy.
- **resize** (default *BicubicResize*) – resize function for steps with one pixel or subpixel accuracy.
- **rotate** (default *BilinearRotate*) – rotation function. Currently built-in one is used which based on AForge.NET.
- **editor** (default false). If true then visual editor form will be open at the script loading. 
- **mode** (default "default") – engine mode:  
DEFAULT – default mode  
UPDATE – as default but with forced diff update   
ERASE – erase statistics  
READONLY – use only existing align statistics
PROCESSED – include only processed frames from stat file
- **colorAdjust** - not implemented yet
- **sceneFile** - scene file path with keyframes to separate scenes during auto-align process 
- **simd** (default true) - SIMD Library using to increase performance in some cases
- **debug** (default false) - output align values, slower.

#### How it works
The engine searches optimal align params: coordinates of overlaid clip’s top left corner (x=0,y=0 is the left top corner of source clip), rotation angle in degrees, overlaid clip width and height, floating-point crop values from 0 to 1 for subpixel accuracy (left, top, right, bottom) and DIFF value.  
*OverlayConfig* chain provides ranges of acceptable align values. The engine runs each config one-by-one while align values with acceptable diff would not be found. Auto-align process for each configuration includes multiple scaling stages. At the first stage all possible combinations of align values are tested in the lowest resolution. The count of best align value sets passed to the next stage is given by `OverlayConfig.branches` parameter. At the next stages best sets from previous are corrected. Note that, simplified, one pixel in low resolution includes four from high. *OverlayConfig* contains `correction` parameter to set correction limit for all directions and resolutions in pixels.  
Image resizing is performed by functions from *presize* and *resize* parameters. First one is used at the intermediate scale stages, last one at the final. So it’s recommended to use something better than BilinearResize at the end. The functions must have signature as follow: `Resize(clip clip, int target_width, int target_height, float src_left, float src_top, float src_width, float src_height)`. Additional parameters are allowed, that is, the signature corresponds to standard resize functions. It is recommended to use the ResampleMT plugin, which provides the same functions but internally multithreaded.  
Besides auto-align of given frame the engine analyses sequence of neighboring frames is specified by *backwardFrames* and *forwardFrames* parameters according to *maxDiff, maxDiffIncrease, maxDeviation, stabilize, panScanDistance, panScanScale* params as described below:  
1. Request align values of current frame.
2. Return data from statFile or in-memory cache if this frame was auto-aligned already.
3. If previous frames in quantity of *backwardFrames* are already aligned with the same values and their DIFF don’t exceed *maxDiff* value, then the same align values will be tested for the current frame. 
4. If maximum DIFF is increasing within sequence is lower than *maxDiffIncrease*, then the engine analyzes next frames in quantity of *forwardFrames* otherwise current frame will be marked as independent from previous – that means scene change.
5. If current frame was included to the sequence with previous frames then the same align values will be tested on forward frames if *forwardFrames* is greater than zero. If all next frames are good at the same align then current frame will be included to the sequence. 
6. If some of next frames exceed *maxDiff* value or difference between next frame diff and average diff is higher than *maxDiffIncrease* then the engine will work out optimal auto-align values for next frame. If new align values significantly differ from previous then we have scene change otherwise there is pan&scan episode and each frame should be processed independently. The parameter that used for this decision is *maxDeviation*.
7. If current frame is marked as independent after previous operations and *OverlayEngine.stablize=true* then engine tries to find equal align params for the next frames too to start new scene.
8. If *backwardFrames* is equal to zero then each frame is independent. This scenario has very low performance and may cause image shaking.
9. If the sources are not stabilized to each other use *panScanDistance* and *panScanScale* params. 

##### Visual editor
Displayed when *OverlayEngine.editor* is true. It is useful after analysis pass when statFile is specified to check all scenes and manually correct incorrect aligned frames. 
Preview from the left side. Below there is trackbar by total frame count and current frame input field. Grid with equal aligned frame sequences on the right. It is possible to move between scenes by row selection. Below grid there is a control panel.  
*Overlay settings* contains controls to change align values of current scene.

Below there is *AutoOverlay* section.  
*Single frame* button performs auto-align based on the current frame. The result is applied to the full scene. 
*Separated frame* button performs auto-align based on the current frame. If new align values is differ from the scene then current frame is separated.  *Scene* button performs auto-align for each frame in the scene independently. 
*Scene* button process the whole scene. The progress will be displayed at the form caption.  
On the right there are controls to process pan&scan scenes. Useful to realign defective pan&scan scenes. Before using it join pan&scan frame sequence into one scene (one row in grid) and press *Single frame* for the first frame in the sequence. After that set maximum *distance* in pixels of shifting and resizing limit in pro mil for each next frame according to previous one. 

Below there is *Display settings*. It includes the same settings as OverlayRender filter: output resolution, overlay mode, gradient and noise length, opacity in percent, *preview* and *display info* checkboxes. If preview is disabled then source clip is displayed.  
*Max deviation* needs to regulate how frames joins into scenes in grid. 

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
    OverlayRender(clip engine, clip source, clip overlay, clip sourceMask, clip overlayMask, string overlayMode, 
                  int width, int height, string pixelType, int gradient, int noise, bool dynamicNoise, 
				  int borderControl, float borderMaxDeviation, clip borderOffset, 
				  clip srcColorBorderOffset, clip overColorBorderOffset, int mode, float opacity, 
                  float colorAdjust, int colorFramesCount, float colorFramesDiff, float colorMaxDeviation, 
				  string adjustChannels, string matrix, string upsize, string downsize, string rotate, 
                  bool simd, bool debug, bool invert, bool extrapolation, int blankColor, float background, 
				  int backBlur, int bitDepth)
                  
This filter preforms rendering of the result clip using align values from the engine. 

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip. Any planar YUV (8-16 bit), RGB24 and RGB48 color spaces are supported.
- **sourceMask** and **overlayMask** (default empty) - mask clips for source and overlaid clips. Unlike masks in overlay engine in this filter they work as avisynth built-in overlay filter masks. If both masks are specified then they are joined to one according align values. The purpose of the masks is maximum hiding of logos and other unwanted parts of the clips at transparent or gradient parts, which could be specified by *gradient* and *noise* parameters. 
- **overlayMode** (default blend) – overlay mode for built-in `Overlay` filter
- **width** и **height** - output width and height. By default are taken from source clip.
- **pixelType** - not implemented yet. The color space of source clip will be used for output. 
- **gradient** (default 0) – length of gradient at the overlay mask to make borders between images smoother. Useful when clips are scientifically differ in colors or align is not very good.
- **noise** (default 0) - length of "noise gradient" at the overlay mask to make borders between images smoother. Useful when clips are in the same color and align is very good. Combine this with gradient to adjust more accurate align. 
- **dynamicNoise** (default true) – use dynamic noise gradient if *noise* > 0.
- **borderControl** (default 0) – backward and forward frame count to analyze which side of overlay mask should be used for current frame according to borderOffset parameter.
- **borderMaxDeviation** (default 0.5) – max deviation of total area between current and adjacent frame to include frame to the scene for border control. 
- **borderOffset** (default empty) - *Rect* type clip to specify offsets from borders (left, top, right, bottom) that will be ignored for gradient mask.
- **srcColorBorderOffset** (default empty) - *Rect* type clip to specify offsets from source clip borders (left, top, right, bottom) that will be ignored for color adjustment.
- **overColorBorderOffset** (default empty) - *Rect* type clip to specify offsets from overlay clip borders (left, top, right, bottom) that will be ignored for color adjustment.
- **mode** (default 1) – overlay and cropping mode:  
1 - crop around the edges of the source image.  
2 - combination of both images with cropping to the all four edges of the output clip with "ambilight" at the corners.  
3 - combination of both images without any cropping.  
4 - as 3 but with "ambilight" at the corners.  
5 - as 3 but with "ambilight" at all free space.  
6 – as 3 but with pure white clips. Useful to combine the result clip with another one.  
- **opacity** (default 1) – opacity of overlaid image from 0 to 1.
- **colorAdjust** (default -1, disabled) - value between 0 and 1. 0 - color adjusts to source clip. 1 - to overlay clip. 0.5 - average. Color adjustment performs by histogram matching in the intersection area.
- **colorFramesCount** (default 0) - forward and backward frames count that include to the color transition map for color adjustment 
- **colorFramesDiff** (default 1) -  max RMSE of difference between sample and reference histogram for current and adjacent frame for color adjustment 
- **colorMaxDeviation** (default 1) -  max deviation of total area between current and adjacent frame to include frame to the scene for color adjustment 
- **adjustChannels** (default empty) - which channels to adjust. Examples: "yuv", "y", "rgb", "rg".
- **matrix** (default empty). If specified YUV image converts to RGB with given matrix for color adjustment and then back to YV24. 
- **downsize** и **upsize** (default *BicubicResize*) - downsize and upsize functions. It’s recommended to use functions with high quality interpolation. 
- **rotate** (default *BilinearRotate*) – rotation function.
- **simd** (default *true*) – SIMD Library using to increase performance in some cases.
- **debug** -  output align settings to the top left corner.
- **invert** - swap source and overlay clips.
- **extrapolation** - same as ColorAdjust.extrapolation.
- **blankColor** (default black) - blank color in hex format `0xFF8080` to fill empty areas of image in 3 and 4 modes.
- **background** (default 0) - value between -1 and 1 to set what clip will be used as blurred background in 2,4,5 modes. -1 - source, 1 - overlay clips.
- **backBlur** (default 15) - blur intensity in 2,4,5 modes.
- **bitDepth** (default unused) - target bit depth of output clip and input clips after transformations but before color adjustment to improve it

### ColorAdjust
    ColorAdjust(clip sample, clip reference, clip sampleMask, clip referenceMask, float intensity, 
				int adjacentFramesCount, float adjacentFramesDiff, 
	            bool limitedRange, string channels, float dither, float exclude, string interpolation, 
				bool extrapolation, bool dynamicNoise, bool simd, bool debug)
    
Color matching. Input clip, sample clip and reference clip must be in the same type of color space (YUV or RGB). Any planar YUV (8-16 bit), RGB24 and RGB48 color spaces are supported. Input clip and sample clip must have the same bit depth (usually sample is the cropped or resized input clip). The bit depth of output clip will changed to the reference one. The filter provides perfect matching only if sample and reference are represent the same area of frame while the input (first argument) may have different framing. This filter is used inside OverlayRender but it is also useful as standalone. 

#### Parameters
- **clip** (required) - clip for color adjustment
- **sample** (required) - the sample clip (usually the first clip or cropped) 
- **reference** (required) - reference clip (usually the same time and area from different master)
- **sampleMask** and **referenceMask** (default empty) - 8 bit planar mask clips to exclude some parts from sample or reference clips. Only Y plane is used. Used only pixels with 255 value (white color). 
- **intensity** (default 1) - intensity of color adjustment.
- **adjacentFramesCount** (default 0) - forward and backward frames count that include to the color transition map
- **adjacentFramesDiff** (default 1) - max RMSE of difference between sample and reference histogram for current and adjacent frame
- **limitedRange** (default true) - TV or PC range for YUV clips
- **channels** (default yuv or rgb) - planes to process for YUV clips or channels for RGB. Any combination of y,u,v and r,g,b supported (ex: y, uv, r, br).
- **dither** (default 0.95) - dither level from 0 (disable) to 1 (aggressive). 
- **exclude** (default 0) - value to exclude rare colors by formula: *current_color_pixel_count / total_pixel_count < exclude*.
- **interpolation** (default linear) - interpolation mode for Math.NET Numerics (spline, akima, linear).
- **extrapolation** (default false, experimental) - adjust the colors of input clip out of bounds colors of sample clip.
- **dynamicNoise** (default true) - dynamic noise if color mapping is the same between frames.
- **simd** (default true) - SIMD Library using to increase performance in some cases.

### ComplexityOverlay
    ComplexityOverlay(clip source, clip overlay, string channels, int steps, float preference, bool mask, float smooth, bool debug)
    
Independent filter to combine most complexity parts of two source clips. It is useful to get result clip with best parts of the image from two low quality sources. The clips must have the same framing, color grading, resolutions and color spaces. 

#### Parameters
- **source** and **overlay** - source clips
- **channels** (default yuv or rgb) - planes to process for YUV clips or channels for RGB. Any combination of y,u,v and r,g,b supported (ex: y, uv, r, br).
- **steps** (default 1) - number of steps to analyze clips.
- **preference** (default 0) - if greater than 0 overlay clip will be more preferred, otherwise source clip. Recommended range: -1 to 1. 
- **mask** (default false) - mask output instead of combined clip.
- **smooth** (default 0) - smooth overlay mask to prevent much sharpening. Recommended range: 0 to 1.

### OverlayCompare
    OverlayCompare(clip engine, clip source, clip overlay, string sourceText, string overlayText, int sourceColor, 
                   int overlayColor, int borderSize, float opacity, int width, int height, bool debug)
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
    StaticOverlayRender(clip source, clip overlay, int x, int y, float angle, int overlayWidth, int overlayHeight, 
                        float cropLeft, float cropTop, float cropRight, float cropBottom, float diff, 
                        clip sourceMask, clip overlayMask, string overlayMode, int width, int height, 
                        string pixelType, int gradient, int noise, bool dynamicNoise, clip borderOffset, 
                        clip srcColorBorderOffset, clip overColorBorderOffset, int mode, float opacity,
                        float colorAdjust, string adjustChannels, string matrix, string upsize, string downsize, 
                        string rotate, bool simd, bool debug, bool invert, bool extrapolation, int blankColor, float background, int backBlur)

As OverlayRender but with fixed align settings without OverlayEngine.

#### Parameters
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **x** (required) - x coordinate.
- **y** (required) - y coordinate.
- **angle** (default 0) - rotation angle.
- **overlayWidth** (overlay clip width by default) - width of overlay clip after resize.
- **overlayHeight** (overlay clip height by default) - height of overlay clip after resize.
- **cropLeft**, **cropTop**, **cropRight**, **cropBottom** (default 0) - crop overlay clip before resize for subpixel alignment.
- **diff** (default 0) - DIFF value for debug output. 
Other parameters are same as for *OverlayRender* filter.

### CustomOverlayRender
    CustomOverlayRender(clip engine, clip source, clip overlay, string function, int width, int height, bool debug)
This filter allows to override default overlay algorithms by user using overlay settings from OverlayEngine. 

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **function** (required) - user function name. The function must have the following parameters: `clip engine, clip source, clip overlay, int x, int y, float angle, int overlayWidth, int overlayHeight, float cropLeft, float cropTop, float cropRight, float cropBottom, float diff)`
- **width** (source clip width by default) - output clip width.
- **height** (source clip height by default) - output clip height.
- **debug** (default false) - debug mode.

## Rect
    Rect(int left, int top, int right, int bottom, bool debug)
    
Support filter to use as argument on other clips. It represents a rectangle. 
    
#### Parameters
Left, top, right, bottom integer values. 

### ColorRangeMask
    ColorRangeMask(clip, int low, int high)
Support filter which provides mask clip by color range: white if pixel value is between low and high arguments. For YUV clips only luma channel is used. For RGB clips all channels are processed independently. Output is the clip in the same color space. Limited range is not supported. 

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
                int left, int top, int right, int bottom, 
                bool noise, bool gradient, int seed)
Support filter which provides mask clip for overlay with gradient or noise at borders.

#### Parameters
- **template** (default empty) - if specified width, height and color space will be used from template clip for output.
- **width** - output clip width if template is not specified. 
- **height** - output clip height if template is not specified.
- **left**, **top**, **right**, **bottom** - border size.
- **noise** - noise generation on borders.
- **gradient** - gradient borders.
- **seed** - seed for noise generation.

### ExtractScenes
	ExtractScenes(string statFile, string sceneFile, int sceneMinLength, float maxDiffIncrease)
Filter to extract and save scene key frames to text file based on stat file of aligning target clip to the target.Trim(1,0) clip. 

#### Parameters
- **statFile** - stat file path
- **sceneFile** - scene file path
- **sceneMinLength** (default 10) - scene minimal length
- **maxDiffIncrease** (default 15) - scene detection DIFF value

## Examples
#### Simple script 
    OM=AviSource("c:\test\OpenMatte.avs") # YV12 clip
    WS=AviSource("c:\test\Widescreen.avs") # YV12 clip
    OverlayEngine(OM, WS, configs = OverlayConfig(subpixel=2)) 
    OverlayRender(OM, WS, debug = true)
#### Analysis pass without render. Aspect ratio range was specified. Set editor=true after analysis pass.
    OM=AviSource("c:\test\OpenMatte.avs") # YV12 clip
    WS=AviSource("c:\test\Widescreen.avs") # YV12 clip
    config=OverlayConfig(aspectRatio1=2.3, aspectRatio2=2.5)
    OverlayEngine(OM, WS, configs = config, statFile="c:\test\Overlay.stat", editor=false)
#### Render after analysis pass
    OM=AviSource("c:\test\OpenMatte.avs") # YV12 clip
    WS=AviSource("c:\test\Widescreen.avs") # YV12 clip
    OverlayEngine(OM, WS, statFile="c:\test\Overlay.stat")
    OverlayRender(OM, WS, debug=true, noise=50, upsize="Spline64Resize")
    ConvertToYV12()

## Changelist
### 17.01.2021 v0.4.0
1. Fix internal clip cache (speed improvement especially with "hard" input clips).
2. ColorAdjust: adjacent frames analyzing for better color matching (AdjacentFramesCount and AdjacentFramesDiff params) + speed improvement.
3. ColorAdjust: fix blank frame processsing. 
4. OverlayRender: new overlay mask algorithm on borders when gradient and noise are used at the same time: more accurate frame intersection. Both 50-100 values are optimal to use.  
5. OverlayRender: BorderControl and BorderMaxDeviation params for adjacent frames analyzing to stablize overlay mask on borders during the scene.
6. OverlayRender: ColorFramesCount, ColorFramesDiff and ColorMaxDeviation params for better color matching during the scene.
7. OverlayRender: experimanetal BitDepth param to change bit depth of output clip and input clip after transformation for better color adjustment. 
8. ExtractScenes: new filter for scene detection by stat file analyzing.
9. OverlayConfig: WarpPoints, WarpSteps and WarpOffset params for "warp" (similar to quadrilateral but more flexible) transformations with warp filter by wonkey_monkey. Only 8 bit clips support. 
10. OverlayEngine: SceneFile param to use a text file with keyframes to separate scenes during adjacent frames analyzing. 
11. Fix exception visualization. 
12. OverlayEditor: asynchronous rendering, new scene processing dialog.
13. OverlayEditor: new "Frame processing" section.
14. OverlayEditor: large stat file speed up.
15. OverlayEditor: default maximum of records in grid changed to 2000.
16. OverlayEditor: more intuitive scene processing. Frames are grouped to scenes with max deviation parameter. 
17. OverlayEditor: new "adjust" scene processing mode. Selected scene frames are adjusted around current frame using Distance and Scale values.
18. OverlayEditor: "Scan" mode works different: from first to last frame of the scene taking warp from selected frame.
19. OverlayEditor: "warp" field with list of points for warp transformation. 
20. OverlayEngine: new stat file format version with warp points (max 16 allowed).

### 29.08.2020 v0.3.1
1. Fix first frame x264 encoding.
2. ColorAdjust: HDR extrapolation fix, dynamicNoise parameter.
3. OverlayRender: extrapolation parameter.

### 28.08.2020 v0.3
1. OverlayEngine: presize and resize instead of upsize and downsize.
2. OverlayEngine: new engine mode PROCESSED.
3. OverlayEngine: pan&scan (unstable sources) support.
4. SIMD Library using and increased performance. 
5. ColorAdjust: new interpolation algorithms via Math.NET Numerics.
6. AviSynth API v8 
7. OverlayEditor: new features. 
8. OverlayRender: new features.
9. ComplexityOverlay: new filter.
