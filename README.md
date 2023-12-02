# AutoOverlay AviSynth plugin

### Requirements
- AviSynth+ 3.6+: https://github.com/AviSynth/AviSynthPlus/releases/
- AvsFilterNet plugin https://github.com/Asd-g/AvsFilterNet (included)
- SIMD Library https://github.com/ermig1979/Simd (included)
- warp plugin v0.1b by wonkey_monkey https://forum.doom9.org/showthread.php?t=176031 (included)
- Math.NET Numerics (included)
- MVTools https://github.com/pinterf/mvtools/releases (for aoInterpolate, not included)
- RGTools https://github.com/pinterf/RgTools/releases (for aoInterpolate, not included)
- .NET framework 4.8+
- Windows 7+

Windows XP and previous versions of AviSynth are supported only before v0.2.5.

### Installation
- Copy all files from x86/x64 folder to the AviSynth plugins folder.
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
    portrait=ImageSource("Lenna.portrait.jpg")
    landscape=ImageSource("Lenna.landscape.jpg")

    OverlayEngine(portrait, landscape)
    OverlayRender(portrait, landscape, colorAdjust=1, outerBounds=Rect(1), innerBounds=Rect(1), \
                  background="blur", edgeGradient="full", gradient=80, width=500, height=500)
    
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
                  int scanDistance, float scanScale, bool stabilize, float stickLevel, float stickDistance, 
                  clip configs, string presize, string resize, string rotate, bool editor, string mode, 
                  int colorAdjust, string sceneFile, bool simd, bool debug)

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
- **scanDistance** (default 0) – maximum allowed shift in pixels between two frames in scene. Use it only if source clips are not stabilized to each other. 
- **scanScale** (default 3) – maximum allowed overlay clip resolution scaling in ppm between two frames in scene.
- **stabilize** (default true) – attempt to stabilize align params at the beginning of scene when there is no enough backward frames including to current scene. If true `panScanDistance` should be 0.
- **stickLevel** (default 0) - max difference between DIFF values of best align params and another one when image may be sticked to borders of source.
- **stickDistance** (default 1) - max distance between borders of source and overlay with best align params when image may be sticked to borders of source.
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
UNPROCESSED – include only unprocessed frames from stat file
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

*Frame processing* section below.
Align - frame auto-align without analyzing other frames
Adjust - auto-align every choosen frame based on current frame (or first frame in scene if scene is not current) with parameters Distance (X and Y deviation), Scale (Width x Height deviation) and Max deviation of joined and intersection area. 
Scan - auto-align based on previous frame in scene started from first one with parameters Distance, Scale and Max deviation.

Frame - current frame processing (in Align mode when scene is fixed all frames will changed)
Single - current frame processing. When scene is fixed and align params will changed current frame will seperated)
Scene - current scene processing (current row in grid)
Clip - whole clip processing (all rows in grid)

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
    OverlayRender(clip engine, clip source, clip overlay, clip sourceMask, clip overlayMask, clip extraClips, 
                  clip innerBounds, clip outerBounds, float overlayBalanceX, float overlayBalanceY, bool fixedSource, 
                  int overlayOrder, string overlayMode, int width, int height, string pixelType, int gradient, int noise, bool dynamicNoise, 
                  int borderControl, float borderMaxDeviation, clip borderOffset, clip srcColorBorderOffset, clip overColorBorderOffset, 
                  bool maskMode, float opacity, float colorAdjust, string colorInterpolation, float colorExclude, 
                  int colorFramesCount, float colorFramesDiff, float colorMaxDeviation, string adjustChannels, string matrix, 
                  string upsize, string downsize, string rotate, bool simd, bool debug, bool invert, bool extrapolation, 
                  string background, clip backgroundClip, int blankColor, float backBalance, int backBlur, 
                  bool fullScreen, string edgeGradient, int bitDepth)
                  
This filter preforms rendering of the result clip using align values from the engine. 

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip. Any planar YUV (8-16 bit), RGB24 and RGB48 color spaces are supported.
- **sourceMask** and **overlayMask** (default empty) - mask clips for source and overlaid clips. Unlike masks in overlay engine in this filter they work as avisynth built-in overlay filter masks. If both masks are specified then they are joined to one according align values. The purpose of the masks is maximum hiding of logos and other unwanted parts of the clips at transparent or gradient parts, which could be specified by *gradient* and *noise* parameters. The masks should be in the target clip bit depth. 
- **extraClips** (default empty) - series of OverlayClip to overlay additional clips.
- **innerBounds** (default 0) - clip of type Rect to limit empty areas inside union area of all clips. Values in range from 0 to 1 are coefficients. Values higher than 1 are absolute values in pixels.
- **outerBounds** (default 0) - clip of type Rect to limit empty areas outside union area of all clips. Values in range from 0 to 1 are coefficients. Values higher than 1 are absolute values in pixels.
- **overlayBalanceX** (default 0) - balance between source (-1) and overlaid (1) images on X coordinate in range from 0 to 1.
- **overlayBalanceY** (default 0) - balance between source (-1) and overlaid (1) images on Y coordinate in range from 0 to 1.
- **fixedSource** (default false) - fixed alignment of source clip.
- **overlayOrder** (default 0) - serial number of overlaid clip to overlay it after extra clips.
- **overlayMode** (default blend) – overlay mode for built-in `Overlay` filter. To check auto align result use `difference`.
- **width** и **height** - output width and height. By default are taken from source clip.
- **pixelType** - the color space of output clip. Should be planar or RGB according to aligned clips. Source clip color space is used by default.
- **gradient** (default 0) – length of gradient at the overlay mask to make borders between images smoother. Useful when clips are scientifically differ in colors or align is not very good.
- **noise** (default 0) - length of "noise gradient" at the overlay mask to make borders between images smoother. Useful when clips are in the same color and align is very good. Combine this with gradient to adjust more accurate align. 
- **dynamicNoise** (default true) – use dynamic noise gradient if *noise* > 0.
- **borderControl** (default 0) – backward and forward frame count to analyze which side of overlay mask should be used for current frame according to borderOffset parameter.
- **borderMaxDeviation** (default 0.5) – max deviation of total area between current and adjacent frame to include frame to the scene for border control. 
- **borderOffset** (default empty) - *Rect* type clip to specify offsets from borders (left, top, right, bottom) that will be ignored for gradient mask.
- **srcColorBorderOffset** (default empty) - (not implemented) *Rect* type clip to specify offsets from source clip borders (left, top, right, bottom) that will be ignored for color adjustment.
- **overColorBorderOffset** (default empty) - (not implemented) *Rect* type clip to specify offsets from overlay clip borders (left, top, right, bottom) that will be ignored for color adjustment.
- **maskMode** (defualt false) - if true then all clips are replaced by white masks.
- **opacity** (default 1) – opacity of overlaid image from 0 to 1.
- **colorAdjust** (default -1, disabled) - value between 0 and 1. 0 - color adjusts to source clip. 1 - to overlay clip. 0.5 - average. Only descrete values -1, 0, 1 are supported with additional clips. Color adjustment performs by histogram matching in the intersection area.
- **colorInterpolation** (default linear) - see ColorAdjust.interpolation
- **colorExclude** (default 0) - see ColorAdjust.exclude
- **colorFramesCount** (default 0) - forward and backward frames count that include to the color transition map for color adjustment 
- **colorFramesDiff** (default 1) -  max RMSE of difference between sample and reference histogram for current and adjacent frame for color adjustment 
- **colorMaxDeviation** (default 1) -  max deviation of total area between current and adjacent frame to include frame to the scene for color adjustment 
- **adjustChannels** (default empty) - which channels to adjust. Examples: "yuv", "y", "rgb", "rg".
- **matrix** (default empty). If specified YUV image converts to RGB with given matrix for color adjustment and then back to output color space. 
- **downsize** и **upsize** (default *BicubicResize*) - downsize and upsize functions. It’s recommended to use functions with high quality interpolation. If only one parameter was specified the other will take the same value.
- **rotate** (default *BilinearRotate*) – rotation function.
- **simd** (default *true*) – SIMD Library using to increase performance in some cases.
- **debug** -  output align settings to the top left corner.
- **invert** - swap source and overlay clips.
- **extrapolation** - see ColorAdjust.extrapolation.
- **background** (default blank) - background filling algorithm: blank (blank color filling), blur (resized blur output image), inpaint (not implemented). 
- **backgroundClip** (default empty) - if specified will be used as background. Should have the same resolution as output clip.
- **blankColor** (default `0x008080` for YUV and `0x000000` for RGB) - blank color in HEX format.
- **backBalance** - real value between -1 and 1 to set what clip should be used as blurred background. -1 - source, 1 - overlay clips.
- **backBlur** (default 15) - blur strength when `background` is `blur`.
- **fullScreen** (default false) - background image fills all output clip area instead of just union area of aligned clips.
- **edgeGradient** (default none) - gradient mode at edges of aligned clips: `none` - disabled, `inside` - only inside union area, `full` - everywhere. 
- **bitDepth** (default unused) - target bit depth of output clip and input clips after transformations but before color adjustment to improve it

### ColorAdjust
    ColorAdjust(clip sample, clip reference, clip sampleMask, clip referenceMask, bool greyMask,
                float intensity, int seed, int adjacentFramesCount, float adjacentFramesDiff, 
	            bool limitedRange, string channels, float dither, float exclude, string interpolation, 
				bool extrapolation, bool dynamicNoise, bool simd, int threads, bool debug)
    
Color matching. Input clip, sample clip and reference clip must be in the same type of color space (YUV or RGB). Any planar YUV (8-16 bit), RGB24 and RGB48 color spaces are supported. Input clip and sample clip must have the same bit depth (usually sample is the cropped or resized input clip). The bit depth of output clip will changed to the reference one. The filter provides perfect matching only if sample and reference are represent the same area of frame while the input (first argument) may have different framing. This filter is used inside OverlayRender but it is also useful as standalone. 

#### Parameters
- **clip** (required) - clip for color adjustment
- **sample** (required) - the sample clip (usually the first clip or cropped) 
- **reference** (required) - reference clip (usually the same time and area from different master)
- **sampleMask** and **referenceMask** (default empty) - 8 bit planar mask clips to exclude some parts from sample or reference clips. Only Y plane is used. Used only pixels with 255 value (white color).
- **greyMask** (default true) - one mask (luma channel) for all planes or not.
- **intensity** (default 1) - intensity of color adjustment.
- **seed** (default is constant) - seed for dynamic noise if there is multiple using of filter for rendering one frame
- **adjacentFramesCount** (default 0) - forward and backward frames count that include to the color transition map
- **adjacentFramesDiff** (default 1) - max RMSE of difference between sample and reference histogram for current and adjacent frame
- **limitedRange** (default true) - TV or PC range for YUV clips
- **channels** (default yuv or rgb) - planes to process for YUV clips or channels for RGB. Any combination of y,u,v and r,g,b supported (ex: y, uv, r, br).
- **dither** (default 0.95) - dither level from 0 (disable) to 1 (aggressive). 
- **exclude** (default 0) - value to exclude rare colors by formula: *current_color_pixel_count / total_pixel_count < exclude*.
- **interpolation** (default linear) - interpolation mode for Math.NET Numerics (spline, akima, linear, none).
- **extrapolation** (default false, experimental) - adjust the colors of input clip out of bounds colors of sample clip.
- **dynamicNoise** (default true) - dynamic noise if color mapping is the same between frames.
- **simd** (default true) - SIMD Library using to increase performance in some cases.
- **threads** (.NET default) - maximum thread count

### ComplexityOverlay
    ComplexityOverlay(clip source, clip overlay, string channels, int steps, float preference, bool mask, 
                      float smooth, int threads, bool debug)
    
Independent filter to combine most complexity parts of two source clips. It is useful to get result clip with best parts of the image from two low quality sources. The clips must have the same framing, color grading, resolutions and color spaces. 

#### Parameters
- **source** and **overlay** - source clips
- **channels** (default yuv or rgb) - planes to process for YUV clips or channels for RGB. Any combination of y,u,v and r,g,b supported (ex: y, uv, r, br).
- **steps** (default 1) - number of steps to analyze clips.
- **preference** (default 0) - if greater than 0 overlay clip will be more preferred, otherwise source clip. Recommended range: -1 to 1. 
- **mask** (default false) - mask output instead of combined clip.
- **smooth** (default 0) - smooth overlay mask to prevent much sharpening. Recommended range: 0 to 1.
- **threads** (.NET default) - maximum thread count

### ComplexityOverlayMany
    ComplexityOverlayMany(clip source, clip[] overlays, string channels, int steps, int threads, bool debug)
	
The same as ComplexityOverlay but for multiple clips.

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
    StaticOverlayRender(clip source, clip overlay, float x, float y, float angle, float overlayWidth, float overlayHeight,
                        string warpPoints, float diff, clip sourceMask, clip overlayMask,
                        clip innerBounds, clip outerBounds, float overlayBalanceX, float overlayBalanceY, bool fixedSource,
                        int overlayOrder, string overlayMode, int width, int height, string pixelType, int gradient, int noise, bool dynamicNoise,
                        int borderControl, float borderMaxDeviation, clip borderOffset, clip srcColorBorderOffset, clip overColorBorderOffset,
                        bool maskMode, float opacity, float colorAdjust, string colorInterpolation, float colorExclude,
                        int colorFramesCount, float colorFramesDiff, float colorMaxDeviation, string adjustChannels, string matrix,
                        string upsize, string downsize, string rotate, bool simd, bool debug, bool invert, bool extrapolation,
                        string background, clip backgroundClip, int blankColor, float backBalance, int backBlur,
                        bool fullScreen, string edgeGradient, int bitDepth)

As OverlayRender but with fixed align settings without OverlayEngine.

#### Parameters
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **x** (required) - x coordinate.
- **y** (required) - y coordinate.
- **angle** (default 0) - rotation angle.
- **overlayWidth** (overlay clip width by default) - width of overlay clip after resize.
- **overlayHeight** (overlay clip height by default) - height of overlay clip after resize.
- **diff** (default 0) - DIFF value for debug output. 
Other parameters are same as for *OverlayRender* filter.

### CustomOverlayRender
    CustomOverlayRender(clip engine, clip source, clip overlay, string function, int width, int height, bool debug)
	
This filter allows to override default overlay algorithms by user using overlay settings from OverlayEngine via user function with parameters: `clip engine, clip source, clip overlay, int x, int y, float angle, int overlayWidth, int overlayHeight, float diff)` 

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **function** (required) - user function name. The function must have the following parameters: `clip engine, clip source, clip overlay, int x, int y, float angle, int overlayWidth, int overlayHeight, float cropLeft, float cropTop, float cropRight, float cropBottom, float diff)`
- **width** (source clip width by default) - output clip width.
- **height** (source clip height by default) - output clip height.
- **debug** (default false) - debug mode.

### OverlayClip
    OverlayClip(clip clip, clip mask, float opacity, bool debug)
	
Support filter to specify additional overlaid clip, mask and opacity level.

### Rect
    Rect(float left, float top, float right, float bottom, bool debug)
    
Support filter to use as argument on other clips. It represents a rectangle. If specified only left then all values take the same value. If specified only left and top then right and bottom will be the same.
    
#### Parameters
Left, top, right, bottom integer values. 

### ColorRangeMask
    ColorRangeMask(clip, int low, int high, bool greyMask)
Support filter which provides mask clip by color range: white if pixel value is between low and high arguments. For YUV clips only luma channel is used. For RGB clips all channels are processed independently. Output is the clip in the same color space. Limited range is not supported. 

#### Parameters
- **input** (required) - input clip.
- **low** (default 0) - lower bound of color range.
- **high** (default 0) - higher bound of color range.
- **greyMask** (default true) - one mask (luma channel) for all planes or not.

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

## User functions
In additional to "native" filters there is some useful user functions in the file OverlayUtils.avsi.
They are designed to make two clips synchronization easier and prepare a source for auto-aligning.

### aoShift
    aoShift(clip clp, int pivot, int length)
Shift frames starting from pivot to delete previous frames or insert blank frames depending on direction.
Positive length - to the right inserting blank frames before pivot
Negative length - to the left deleting frames before pivot

### aoDelay
    aoDelay(clip clp, int length)
A special case of 'aoShift' function to insert blank or delete frames at clip beginning.
Positive length - how many blank frames insert 
Negative length - how many frames delete

### aoDelete
    aoDelete(clip clp, int start, int end)
Delete scene from 'start' to 'end' frame inclusive

### aoReplace
    aoReplace(clip clp, clip replace, int start, int "end")
Replacing frame sequence from 'start' to 'end' inclusive from another (synchronized) clip starting from same frame.
'end' equal to 'start' by default (one frame replacing)
explicit zero 'end' means last frame of the clip

### aoOverwrite
    aoOverwrite(clip clp, clip scene, int frame)
Insert whole clip as a 'scene' from specified 'frame' of input clip with overwriting.

### aoInsert
    aoInsert(clip clp, clip insert, int start, int "end")
'Insert' scene from another clip from 'start' to 'end' inclusive without overwiting
'end' equal to 'start' by default (one frame inserting)
explicit zero 'end' means last frame of the clip

### aoTransition
    aoTransition(clip prev, clip next, int transitionStart, int transitionEnd, 
	             int "reverseTransitionStart", int "reverseTransitionEnd")
Smooth transition between synchronized clips.
transitionStart - first frame of transition
Positive 'transitionEnd' - last frame of transition
Negative 'transitionEnd' - transition length
reverseTransitionStart -  first frame of reverse transition, no reverse transition by default
reverseTransitionEnd - last frame or length of reverse transition, equal to direct transition length by default

### aoTransitionScene
    aoTransitionScene(clip prev, clip next, int start, int end, int "length")
Smooth transition of specified 'length' between synchronized clips for scene replacing from 'start' to 'end' frame.

### aoBorders
    aoBorders(clip clp, int left, int top, int "right", int "bottom", 
	          int "refLength", int "segments", float "blur", float "dither")
Fix invalid color level near borders using ColorAdjust filter.
left, top, right, bottom params specified how much lines or columns need to fix
right and bottom are optional and equal to left and right by default
refLength (default 1) describes number of columns or lines after LTRB that will be used as color reference
segments (default 1) allows to split image into blocks and process them separatly with smooth transition to avoid invalid color adjustment when image is complicated
blur (default 0, max 1.5) makes nearest to edge pixels more smooth to avoid noise
dither - dithering level for ColorAdjust filter

### aoInvertBorders
    aoInvertBorders(clip clp, int left, int top, int "right", int "bottom")
Invert color at borders, useful for masks.
If input is YUV only luma will be processed 
right and bottom equal to left and top by default

### aoInterpolate
    aoInterpolate(clip clp, int length, int "start", int "end", int "removeGrain")
Interpolate frame sequence from 'start' to 'end' to the target 'length' with MVTools.

### aoInterpolateScene
    aoInterpolateScene(clip clp, int inStart, int inEnd, int outStart, int outEnd, int "removeGrain")
Interpolate frame sequence from 'inStart' to 'inEnd' to the target scene from 'outStart' to 'outEnd' with MVTools.

### aoInterpolateOne
    aoInterpolateOne(clip clp, int frame, bool "insert", int "removeGrain")
Insert by default or replace with interpolated frame from the nearest with MVTools.

### aoDebug
    aoDebug(clip clp)
Special function for other functions debugging. 
In case of aoReplace it removes all frames except replacements. 

### aoExpand
    aoExpand(clip mask, int pixels, string mode, float "blur")
	
Expand the black mask (mode=darken) or white mask (mode=lighten)

## Examples
#### Simple script 
    OM=AviSource("OpenMatte.avs")
    WS=AviSource("Widescreen.avs")
    OverlayEngine(OM, WS, configs = OverlayConfig(subpixel=2)) 
    OverlayRender(OM, WS, debug = true)
#### Three clips 
    OM=AviSource("OpenMatte.avs")
    WS=AviSource("Widescreen.avs")
	FS=AviSource("Fullscreen.avs")
    wsEngine=OverlayEngine(OM, WS)
	fsEngine=OverlayEngine(OM, FS)
    wsEngine.OverlayRender(OM, WS, extraClips=fsEngine.OverlayClip(FS))
#### Analysis pass without render
    OM=AviSource("OpenMatte.avs")
    WS=AviSource("Widescreen.avs")
	# Aspect ratio range was specified
    config=OverlayConfig(aspectRatio1=2.3, aspectRatio2=2.5)
	# Set editor=true after analysis pass to edit align params
    OverlayEngine(OM, WS, configs = config, statFile="Overlay.stat", editor=false)
	# Uncomment to render aligned clips
	# OverlayRender(OM, WS, debug=true, noise=50, upsize="Spline64Resize")
	
## Use cases

### Improvement by other clip with different framing and better quality

### Improvement by other clip with same framing and probably quality

### Visible frame area maximization

### Color matching

### HDR to SDR conversion

### SDR to HDR conversion

### Logo removal

### Clip comparing

### Invalid or out of sync frames discovering

### Edge correction

### Scene detection

## Changelist
### 02.12.2023 v0.6.1
1. OverlayEngine: warp and colorAdjust bugfix

### 01.12.2023 v0.6.0
1. OverlayRender: parameter mode deleted.
2. OverlayRender: added parameters extraClips, innerBounds, outerBounds, overlayBalanceX, overlayBalanceY, fixedSource, overlayOrder, maskMode, colorInterpolation, colorExclude, backgroundClip, backBalance, fullScreen, edgeGradient.
3. OverlayRender: parameter background replaced with backBalance, the aim of background parameter was changed.
4. OverlayRender: multiple clips support.
5. OverlayEngine: panScanDistance and panScanScale renamed to scanDistance and scanScale.
6. ComplexityOverlayMany filter was added.

### 17.01.2022 v0.5.2
1. OverlayRender: fix mode=5
2. ExtractScenes: fix parameter type
3. OvrelayEditor: fix warp input parsing
4. OverlayEngine: fix minArea and maxArea using
5. AvsFilterNet: AviSynth 3.7.1 better compatibility

### 29.12.2021 v0.5.1
1. .NET 4.7, latest C# level
2. Color interpolation disabling
3. GreyMask is now optional
4. Threads parameter
5. Better color excluding algoritm
6. RGB internal converting fix
7. Stick level & stick distance params
8. Overlay engine repeat cache fix
9. Overlay editor fixes
10. aoDebug function

### 04.09.2021 v0.5.0
1. User function package
2. Render: fix RGB HDR color mapping
3. OverlayMask: HDR support
4. Render: seed parameter

### 28.08.2021 v0.4.3
1. Editor: maxDiff param editing.
2. Editor: three-state defective frames checkbox.
3. Engine: "unprocessed only" mode.
4. Engine: dummy frame output fix.
5. Core: filter disposing fix.
6. Render: fix rotation rendering.

### 29.05.2021 v0.4.2
1. Fix editor async frame rendering.
2. Fix mask usage with chroma subsampling.

### 28.03.2021 v0.4.1
1. OverlayEngine: fix using sourceMask and overlayMask at the same time. The pixels with mask max value (255 or 65535) are taken to calculate DIFF value.
2. OverlayRender: fix using overlayMask without gradient and noise.
3. Other minor fixes and refactoring.

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
