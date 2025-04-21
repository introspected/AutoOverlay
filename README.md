# AutoOverlay AviSynth plugin

### Requirements
- AviSynth+ 3.7+: https://github.com/AviSynth/AviSynthPlus/releases/
- AvsFilterNet plugin https://github.com/Asd-g/AvsFilterNet (included)
- SIMD Library https://github.com/ermig1979/Simd (included)
- warp plugin v0.1b by wonkey_monkey https://forum.doom9.org/showthread.php?t=176031 (included)
- Avsresize based on z.lib: https://codeberg.org/StvG/avsresize/releases (for ColorMatchChain, not included)
- ResampleMT by jpsdr: https://github.com/jpsdr/ResampleMT/releases (for better performance, not included)
- MVTools https://github.com/pinterf/mvtools/releases (for aoInterpolate, not included)
- RGTools https://github.com/pinterf/RgTools/releases (for aoInterpolate, not included)
- .NET framework 4.8+
- Windows 7+
- For editing and debugging scripts, it is recommended to use AvsPmod: https://github.com/gispos/AvsPmod

### Installation
- Copy the files from the x86/x64 folders to the AviSynth plugins folders.
- In the properties of the DLL files in Windows Explorer, it may be necessary to "Unblock" the files.
- It is recommended to use the x64 version for better performance.

### Description
The plugin is designed for optimal overlaying of one video clip onto another. 
The alignment of clips relative to each other is performed by the OverlayEngine filter by testing various coordinates of the top-left corner of the overlay, image sizes, aspect ratios, and rotation angles to find the optimal overlay parameters. The function for comparing two image sections of the clips is the mean squared deviation, hereinafter referred to as *diff*. The goal of auto-alignment is to find the minimum *diff* value. 
To improve performance, auto-alignment is divided into several scaling steps for testing different overlay parameters, which are specified by the OverlayConfig filter. In the first step, all possible overlay combinations are tested at a low resolution. In each subsequent step, at a higher resolution, combinations based on the best results from the previous step are tested. OverlayConfig configurations can be combined into chains. If a particular configuration yields a good result, the testing of subsequent configurations is skipped to save time. 
After auto-alignment, one clip can be overlaid onto another in various ways using the OverlayRender filter.

### Loading the Plugin
    LoadPlugin("%plugin folder%\AvsFilterNet.dll")
    LoadNetPlugin("%plugin folder %\AutoOverlay_netautoload.dll")
AviSynth+ supports automatic plugin loading if the .NET plugin file name contains the `_netautoload` suffix, which is present by default.

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
    OverlayConfig(string preset, float minOverlayArea, float minSourceArea, float aspectRatio1, float aspectRatio2, 
                  float angle1, float angle2, float minAngleStep, float maxAngleStep, float angleStepCount, 
                  clip warpPoints, int warpSteps, int warpOffset, int minSampleArea, int requiredSampleArea, 
                  float maxSampleDiff, int subpixel, float scaleBase, int branches, float branchMaxDiff, 
                  float acceptableDiff, float correction, float rotationCorrection, int minX, int maxX, 
                  int minY, int maxY, int minArea, int maxArea, bool fixedAspectRatio, bool debug)
     
The filter describes the auto-alignment configuration for OverlayEngine. It contains the boundary values of overlay parameters such as the coordinates of the top-left corner of the overlaid image relative to the base image, the width and height of the overlaid image, and the rotation angle. The configuration also includes parameters for the operation of OverlayEngine. 
The filter's result is a fake frame in which the parameters are encoded, allowing them to be read by OverlayEngine. 
It is possible to combine multiple configurations into chains using standard clip concatenation: `OverlayConfig(…) + OverlayConfig(…)`. In this case, OverlayEngine will sequentially test each configuration at each step until an acceptable *diff* value is achieved.

#### Parameters
- **preset** (default medium) - preset for the main parameters. The *low*, *medium*, *high*, and *extreme* presets adjust quality and performance. *low* is the fastest and least accurate, while *extreme* is the opposite. The *fixed* preset is intended for cases where one image is clearly inscribed within the boundaries of the base clip.

| Parameter / Preset  |   Low   |  Medium  |   High   |  Extreme  |   Fixed   |
| ------------------- | ------- | -------- | -------- | --------- | --------- |
| Subpixel            | 0       | 1        | 2        | 3         | 0         |
| Branches            | 1       | 2        | 3        | 4         | 1         |
| MinSampleArea       | 1000    | 1500     | 1500     | 2000      | 1500      |
| RequiredSampleArea  | 1000    | 3000     | 4000     | 5000      | 3000      |
| RequiredSampleArea  | 1000    | 3000     | 4000     | 5000      | 3000      |
| RotationCorrection  | 0.5     | 0.5      | 1        | 1         | 0.5       |
| FixedAspectRatio    | false   | false    | false    | false     | true      |
| MinOverlayArea      | default | default  | default  | default   | 100       |

- **minOverlayArea** - the minimum ratio of the used portion to the total area of the overlaid image, in percent. By default, it is calculated so that the overlaid clip can fully cover the base clip (panscan mode). For example, if the base clip resolution is 1920x1080 and the overlaid clip is 1920x800, the parameter value will be 800/1080=74%. 
- **minSourceArea** - the minimum ratio of the used portion to the total area of the base image, in percent. By default, it is calculated so that the base clip can fully include the overlaid clip (panscan mode). For example, if the base clip resolution is 1920x1080 and the overlaid clip is 1440x800, the parameter value will be 1440/1920=75%. 
- **aspectRatio1** and **aspectRatio2** - the range of acceptable aspect ratios for the overlaid image. By default, it is the aspect ratio of the overlaid clip. It can be specified in any order: `aspectRatio1=2.35, aspectRatio2=2.45` is the same as `aspectRatio1=2.45, aspectRatio2=2.35`. By default, it equals the aspect ratio of the overlaid clip if *FixedAspectRatio = true*; otherwise, a tolerance is added as *minDimension + (Correction + rotationShift) × 2) / minDimension - 1*, where *minDimension* is the smaller side of the overlaid clip, and *rotationShift* equals *RotationCorrection* if the angles are non-zero, otherwise 0. 
- **angle1** and **angle2** (default 0) - the range of acceptable rotation angles for the overlaid image. Can be specified in any order. Negative values indicate clockwise rotation, positive values indicate counterclockwise rotation.
- **minAngleStep** (default 0.05) - the minimum rotation step of the image, in degrees.
- **maxAngleStep** (default 1) - the maximum rotation step of the image, in degrees.
- **angleStepCount** (default 2) - the number of rotation steps at each overlay parameter selection step.
- **warpPoints** (default empty) - a sequence of Rect-type clips describing the initial points of warp transformations and possible deviations along the X and Y axes, which will be passed to the warp filter. Example: `Rect(0,0,3,3) + Rect(1920,800,3,3) + Rect(1920,0,3,3) + Rect(0,800,3,3) + Rect(960,400,3,3)`. This describes warp transformations with a maximum range of 3 pixels at the corners and center of an image sized 1920x800.
- **warpSteps** (default 3) - the number of warp transformation iterations. Higher values are better but slower.
- **warpOffset** (default 0) - the magnitude of the warp transformation shift from the last step to the first (at a lower resolution). Lower values are better but slower.
- **minSampleArea** (default 1500) - the minimum area in pixels of the base image at the first step. Smaller values are faster but increase the risk of incorrect results. Recommended range: 500-3000. 
- **requiredSampleArea** (default 3000) - the maximum area in pixels of the base image at the first step. Smaller values are faster but increase the risk of incorrect results. Recommended range: 1000-5000.
- **maxSampleDiff** (default 5) - the maximum allowable *diff* value of the reduced base image between steps. If this value is exceeded, the previous step will not be performed. Used to select the initial image size between *minSampleArea* and *requiredSampleArea* and the corresponding step.
- **subpixel** (default 0) - the level of subpixel overlay precision. 0 - one-pixel precision, 1 - half-pixel, 2 - quarter-pixel, and so on. Zero is recommended if one clip has significantly lower resolution than the other. 1-3 is recommended if both clips have roughly the same resolution. Negative values are also supported, in which case the overlay will be performed with reduced precision but faster. 
- **scaleBase** (default 1.5) - the base for calculating the reduction coefficient using the formula `coef=scaleBase^(1 - (maxStep - currentStep))`. Lower values result in more steps. 
- **branches** (default 1) - the number of best overlay parameters from the previous step to use for the search in the current step. Higher values are better but slower. Essentially, this is the branching depth.    
- **branchMaxDiff** (default 0.2) - the maximum difference at the current step between the *diff* values of the best search parameters and others. Used to discard unpromising search branches.  
- **acceptableDiff** (default 5) - the acceptable *diff* value, after which subsequent configurations in the OverlayConfig chain are not tested.
- **correction** (default 1) - the magnitude of correction for a certain parameter from the previous step to the current step. Higher values test more parameter variations but take longer. 
- **rotationCorrection** (default 0.5) - additional correction if the image rotation angle is non-zero.
- **minX**, **maxX**, **minY**, **maxY** - the allowable range of coordinates for the top-left corner of the overlay, unlimited by default. 
- **minArea**, **maxArea** - the range of allowable area for the overlaid image, in pixels. Unlimited by default.
- **fixedAspectRatio** (default false) - a mode for exact aspect ratio of the overlaid clip, applicable only when *aspectRatio1=aspectRatio2*.
- **debug** (default false) - display of configuration parameters.

### OverlayEngine                  
    OverlayEngine(clip source, clip overlay, clip sourceMask, clip overlayMask, string statFile, 
                  string preset, int sceneBuffer, int shakeBuffer, bool stabilize, bool scan, bool correction,
                  float frameDiffTolerance, float frameAreaTolerance, float sceneDiffTolerance, float sceneAreaTolerance,
                  float frameDiffBias, float maxDiff, bool legacyMode, int backwardFrames, int forwardFrames, 
                  float maxDiffIncrease, int scanDistance, float scanScale, float stickLevel, float stickDistance,
                  clip configs, string presize, string resize, string rotate, bool editor, string mode,
                  int colorAdjust, string sceneFile, bool SIMD, bool debug)

The filter takes two clips as input: the base clip and the overlaid clip, and performs an auto-alignment procedure by resizing, rotating, and shifting the overlaid clip to find the minimum *diff* value. The optimal overlay parameters are encoded into the output frame so they can be read by other filters. The sequence of such frame-by-frame overlay parameters (statistics) can be accumulated in memory or in a file for reuse without needing to repeat the costly auto-alignment procedure. The statistics file can be analyzed and edited in the built-in graphical editor. Statistics are accumulated as the clip is navigated in any direction, provided the movement is frame-by-frame sequential; otherwise, neighboring frames will not be considered.

#### Parameters
- **source** (required) - the first, base clip.
- **overlay** (required) - the second, overlaid clip. Both clips must be in the same color space type (YUV or RGB) and bit depth.
- **sourceMask**, **overlayMask** (default empty) - masks for the base and overlaid clips. If a mask is specified, pixels in the clip corresponding to a value of 0 in the mask are ignored in the *diff* calculation. This is useful, for example, to exclude a logo from the *diff* calculation. In RGB clips, channels are analyzed separately. In YUV, only the luma channel is analyzed. The mask must be in full range (`ColorYUV(levels="TV->PC")`).
- **statFile** (default empty) - the path to the file with overlay parameter statistics. If not specified, statistics are accumulated only in memory within a single session. Recommended usage scenario: for initial manual parameter tuning, do not use a statistics file; use it for a test run to gather statistics, analyze, and adjust them in the editor.
- **preset** (default medium) - preset for the filter's main parameters, affecting the balance of quality and performance.

| Parameter / Preset  |   Low   |  Medium  |   High   |
| ------------------- | ------- | -------- | -------- |
| SceneBuffer         | 10      | 15       | 20       |
| ShakeBuffer         | 1       | 2        | 3        |
| FrameDiffTolerance  | 10      | 7        | 5        |
| SceneDiffTolerance  | 75      | 50       | 25       |
| MaxDiff             | 20      | 15       | 10       |

- **sceneBuffer** (default 15) - the number of neighboring previous and subsequent frames (i.e., a total of x2) that will be analyzed for inclusion in the interval.
- **shakeBuffer** (default 2) - the number of neighboring frames used for the initial determination of overlay parameters for the interval.
- **stabilize** (default true) - an attempt to fully stabilize the overlay parameters of neighboring frames. If the clips are not the result of cropping from a single original mastering, this parameter should be set to *false*.
- **scan** (default true) - an attempt to detect scanning between neighboring frames, i.e., smooth changes in overlay parameters without outliers. Applied if full stabilization fails. It is recommended to disable this if it is known in advance that such episodes are absent in the sources. If both *stabilize* and *scan* are disabled, each frame will be aligned from scratch, which is resource-intensive and may introduce outliers.
- **correction** (default true) - an attempt to retrospectively correct the overlay parameters of previously processed frames after processing subsequent frames.
- **frameDiffTolerance** (default 4) - the allowable *diff* deviation in percent during frame stabilization.
- **frameAreaTolerance** (default 0.2) - the allowable deviation of the intersection area in percent during frame stabilization.
- **sceneDiffTolerance** (default 4) - the allowable *diff* deviation in percent between neighboring frames.
- **sceneAreaTolerance** (default 0.5) - the allowable deviation of the intersection area in percent between neighboring frames.
- **frameDiffBias** (default 1.5) - a value added to *diff* to smooth differences during comparison.
- **maxDiff** (default 5) - the *diff* value above which a frame cannot be included in a scene.
- **legacyMode** (default false) - if *true*, the old scene detection algorithm will be used with the parameters *backwardFrames*, *forwardFrames*, *maxDiffIncrease*, *maxDeviation*, *scanDistance*, and *scanScale* instead of *sceneBuffer*, *shakeBuffer*, etc.
- **backwardFrames** and **forwardFrames** (default 3) - the number of previous and subsequent frames analyzed in a single scene for stabilization and speeding up the overlay parameter search.
- **maxDiffIncrease** (default 1) - the maximum allowable *diff* excess of the current frame over the average value in a sequence (scene).
- **maxDeviation** (default 1) - the maximum allowable difference in percent between the union and intersection of two alignment configurations for scene detection. Higher values may lead to erroneous merging of multiple scenes into one but provide better stabilization within a scene.
- **scanDistance** (default 0) - the maximum allowable shift of the overlaid image between neighboring frames in a scene. Used if the sources are not stabilized relative to each other.
- **scanScale** (default 3) - the maximum allowable size change in permille of the overlaid image between neighboring frames in a scene.
- **stickLevel** (default 0) - the maximum allowable difference between *diff* values for the best overlay parameters and those that would cause the overlaid image to stick to the base clip's boundaries.
- **stickDistance** (default 1) - the maximum allowable distance between the edges of the overlaid image for the best overlay parameters and those that would cause the overlaid image to stick to the base clip's boundaries.
- **configs** (default OverlayConfig with default values) - a list of configurations as a clip. Example: `configs=OverlayConfig(subpixel=1, acceptableDiff=10) + OverlayConfig(angle1=-1, angle2=1)`. If, during auto-alignment, the first configuration yields a *diff* value less than 10 after its run, the next configuration with "heavier" parameters (rotation) will be skipped.
- **presize** (default *BilinearResize*) - the image resizing function for the initial scaling steps.
- **resize** (default *Spline36Resize* or *Spline36ResizeMT* if available) - the image resizing function for the final scaling steps.
- **rotate** (default *BilinearRotate*) - the image rotation function. Currently, the default implementation is from the AForge.NET library.
- **editor** (default false) - if *true*, the visual editor will launch when the script is loaded.
- **mode** (default "default") - the mode of working with statistics:  
  - DEFAULT - default mode
  - UPDATE - same as the previous mode, but the current frame’s *diff* is always recalculated
  - ENHANCE - refine the overlay parameters for each specific frame based on previously collected statistics, losing stabilization
  - ERASE - erase statistics (used to clear information about specific frames in conjunction with the *Trim* function)
  - READONLY - use but do not update the statistics file
  - PROCESSED - include only already processed frames
  - UNPROCESSED - include only unprocessed frames
- **colorAdjust** - color correction of clips during overlay for more accurate alignment: 0 - towards the base clip, 1 - towards the overlaid clip.
- **sceneFile** - the path to a file with keyframes for separating scenes during the image auto-alignment process.
- **simd** (default true) - use of the SIMD Library to improve performance in some cases.
- **debug** (default false) - display of overlay parameters, reduces performance.

#### Operating Principle
*OverlayEngine* searches for optimal overlay parameters: the coordinates of the top-left corner of the overlaid clip relative to the base clip, the rotation angle in degrees, the width and height of the overlaid image, and the crop values of the overlaid image edges for subpixel positioning.  
The *OverlayConfig* chain, provided as a clip, defines the boundaries of acceptable values and the algorithm for finding the optimal parameters. The engine processes each configuration sequentially until overlay parameters with an acceptable *diff* are found. The auto-alignment process for each configuration consists of several steps. In the first step, all possible combinations of overlay parameters are tested at a low resolution. A certain number of the best combinations, specified by the *OverlayConfig.branches* parameter, are passed to the next step. At each subsequent step, the overlay parameters are refined at a higher resolution, with the search area defined by the *correction* parameter.  
Image scaling is performed by the functions specified in the *presize* and *resize* parameters. The former is used in the preliminary auto-alignment steps, while the latter is used in the final steps when working at full resolution. For the final steps, it is recommended to use a filter with good interpolation. The scaling function must have the following signature: `Resize(clip clip, int target_width, int target_height, float src_left, float src_top, float src_width, float src_height)`. Additional parameters are allowed. This signature is used in standard AviSynth functions. It is highly recommended to use the ResampleMT plugin, which provides the same results as built-in filters but operates significantly faster due to parallel computations.

##### Visual Editor
Launched if *OverlayEngine.editor=true*.  
On the left is a frame preview. At the bottom, there is a trackbar for the number of frames and a field to input the current frame. On the right is a table displaying frames with identical overlay parameters grouped into episodes. You can switch between episodes. Below the grid is a control panel.  
*Overlay settings* - overlay parameters for the current episode.

Below is the *Frame processing* section:  
- *Align* - auto-overlay "from scratch" without considering other frames.  
- *Adjust* - correction of each scene relative to the current frame (or the first frame of the scene if the current frame is not part of the scene) considering *Distance* (X and Y deviation), *Scale* (deviation of the product of Width and Height), and *Max deviation* (deviation of the intersection area from the union area of frames).  
- *Scan* - sequential scanning mode relative to the previous frame, starting from the first frame of the scene, considering *Distance*, *Scale*, and *Max deviation*.  

Buttons:
- *Frame* - processing of the current frame (in the case of *Align*, if the scene is *Fixed*, the parameters of the entire scene will change).  
- *Single* - processing of the current frame; if the scene is *Fixed* and the overlay parameters change, the frame will be separated into a new scene.  
- *Scene* - processing of the current scene (the current row in the table).  
- *Clip* - processing of all scenes in the table.  

Modified and unsaved episodes are highlighted in yellow in the grid. The *Save* button saves changes. *Reset* discards changes and reloads the data. *Reload* reloads the characteristics for the current frame, applying them to the entire episode.  
*Separate* - isolates the frame. *Join prev* - merges with the frames of the previous episode. *Join next* - merges with the frames of the next episode. *Join to* - merges frames up to and including the specified frame.

**Hotkeys**:  
- *Ctrl + S* - save  
- *Ctrl + R* - reload  
- *D* - enable/disable difference  
- *P* - enable/disable preview  
- *Ctrl + arrow keys* - move overlay image  
- *Ctrl + add/subtract* - scale overlay image  
- *A, Z* - next/previous frame  

### OverlayRender
    OverlayRender(clip engine, clip source, clip overlay, clip sourceMask, clip overlayMask, 
                  clip sourceCrop, clip overlayCrop, string sourceChromaLocation, string overlayChromaLocation, 
                  clip extraClips, string preset, clip innerBounds, clip outerBounds, 
                  float overlayBalanceX, float overlayBalanceY, bool fixedSource, int overlayOrder, 
                  float stabilizationDiffTolerance, float stabilizationAreaTolerance, int stabilizationLength, 
                  string overlayMode, int width, int height, string pixelType, int gradient, int noise, 
                  int borderControl, float borderMaxDeviation, clip borderOffset, 
                  clip srcColorBorderOffset, rectangle overColorBorderOffset, bool maskMode, float opacity, 
                  float colorAdjust, int colorBuckets, float colorDither, int colorExclude, 
                  int colorFramesCount, float colorFramesDiff, float colorMaxDeviation, 
                  bool colorBufferedExtrapolation, float gradientColor, clip colorMatchTarget, 
                  string adjustChannels, string matrix, string sourceMatrix, string overlayMatrix,
                  string upsize, string downsize, string chromaResize, string rotate, bool preview, 
                  bool debug, bool invert, string background, clip backgroundClip, int blankColor, 
                  float backBalance, int backBlur, bool fullScreen, string edgeGradient, int bitDepth)
                  
The filter renders the result of combining two or more clips with specific settings.

#### Parameters
- **engine** (required) - a clip of type *OverlayEngine* that provides the overlay parameters.
- **source** (required) - the first, base clip.
- **overlay** (required) - the second clip overlaid onto the first. 
- **sourceMask** and **overlayMask** (default empty) - masks for the base and overlaid clips. Unlike in OverlayEngine, the meaning of these masks is the same as in the standard *Overlay* filter. Masks adjust the overlay intensity of the clips relative to each other. Masks must have the same bit depth as the relative clip.
- **sourceCrop** and **overlayCrop** (default 0) - Rect-type clips that allow accounting for cropping differences between the statistics (OverlayEngine) and the *source* and *overlay* clips. Positive values indicate cropping, negative values indicate the opposite.
- **sourceChromaLocation** and **overlayChromaLocation** - offset of UV channels relative to the luma channel. Possible values: left, center, top_left, top, bottom, bottom_left. Defaults to the frame property *_ChromaLocation*, or left if not specified. The value of *sourceChromaLocation* is used for the output clip.
- **extraClips** (default empty) - a clip composed of concatenated OverlayClip-type clips describing additional clips for overlay.
- **preset** (default not set) - presets are used for batch pre-setting of other parameters (if not explicitly specified).

| Parameter / Preset  |  FitSource  |  FitScreen  | FitScreenBlur | FitScreenMask |  FullFrame  | FullFrameBlur | FullFrameMask |  Difference  |
| ------------------- | ----------- | ----------- | ------------- | ------------- | ----------- | ------------- | ------------- | ------------ |
| FixedSource         | true        | false       | false         | false         | false       | false         | false         | true         |
| OverlayBalance      | -1          | 0           | 0             | 0             | 0           | 0             | 0             | -1           |
| InnerBounds         | 0           | 1           | 1             | 1             | 1           | 1             | 1             | 0            |
| OuterBounds         | 0           | 0           | 0             | 0             | 1           | 1             | 1             | 0            |
| Gradient            | 50          | 50          | 50            | 0             | 50          | 50            | 0             | 0            |
| EdgeGradient        | none        | none        | inside        | none          | none        | inside        | none          | none         |
| Background          | blank       | blank       | blur          | blank         | blank       | blur          | blank         | blank        |
| MaskMode            | false       | false       | false         | true          | false       | false         | true          | false        |
| OverlayMode         | blend       | blend       | blend         | blend         | blend       | blend         | blend         | difference   |
| Debug               | false       | false       | false         | false         | false       | false         | false         | true         |

- **innerBounds** (default 0) - a Rect-type clip limiting the length of gaps within the union of clips. Values from 0 to 1 are interpreted as a coefficient, while values above 1 are treated as an absolute pixel value relative to the combined area.
- **outerBounds** (default 0) - a Rect-type clip limiting the length of fields relative to the resulting clip. Values from 0 to 1 are interpreted as a coefficient, while values above 1 are treated as an absolute pixel value.
- **overlayBalanceX** (default 0) - horizontal centering of the image relative to the base clip (-1) or overlaid clip (1) in the range from -1 to 1.
- **overlayBalanceY** (default 0) - vertical centering of the image relative to the base clip (-1) or overlaid clip (1) in the range from -1 to 1.
- **fixedSource** (default false) - fixed centering of the resulting clip relative to the base clip.
- **overlayOrder** (default 0) - the layer number for the overlaid clip. Allows overlaying the clip after additional clips.
- **stabilizationDiffTolerance** (default 200) - the allowable *diff* difference between neighboring frames during scene rendering stabilization.
- **stabilizationAreaTolerance** (default 1.5) - the allowable difference in percent between the intersection areas of neighboring frames during scene rendering stabilization.
- **stabilizationLength** (default 0, max 20) - the number of neighboring frames on each side for scene rendering stabilization. Stabilization is disabled by default. It makes sense to enable this if *innerBounds > 0* and the clips are not originally stabilized relative to each other (i.e., statistics were collected with *stabilize = false*). Enabling it will result in a slight cropping of the output frame, but the image will be stabilized. The *sceneFile* from OverlayEngine is also taken into account.
- **overlayMode** (default `blend`) - the overlay mode for the built-in *Overlay* filter. For evaluating the overlay result, it is recommended to use `difference`.
- **width** and **height** - the width and height of the output image. Defaults to the base clip’s dimensions.
- **pixelType** - the color space of the resulting clip, must match the color space type of the overlaid clips (YUV or RGB). Defaults to the base clip’s color space.
- **gradient** (default 0) - the length of a transparent gradient in pixels at the edges of the overlaid area. Makes the transition between images smoother.
- **noise** (default 0) - adds noise to the intersection boundaries to make transitions less noticeable. If greater than 0 then *gradient* applies only at the edges.
- **borderControl** (default 0) - the number of neighboring frames on both sides analyzed to determine which sides of the overlay mask should be included for the current frame, considering the *borderOffset* parameter.
- **borderMaxDeviation** (default 0.5) - the maximum deviation of the total area between the current and neighboring frames for use in a frame sequence when creating the overlay mask.
- **borderOffset** (default empty) - a Rect-type clip specifying "empty" image borders (left, top, right, bottom) that will be ignored when calculating the gradient mask.
- **srcColorBorderOffset** (default empty) - (not implemented) a Rect-type clip for determining "empty" borders of the base clip (left, top, right, bottom) that will be ignored during color correction.
- **overColorBorderOffset** (default empty) - (not implemented) a Rect-type clip for determining "empty" borders of the overlaid clip (left, top, right, bottom) that will be ignored during color correction.
- **maskMode** (default false) - if *true*, replaces all clips with a white mask.
- **opacity** (default 1) - the opacity level of the overlaid image, from 0 to 1.
- **colorAdjust** (default -1, disabled) - a float value between 0 and 1. 0 - towards the base clip’s color, 1 - towards the overlaid clip’s color, 0.5 - averaged color. With additional clips, only values -1, 0, and 1 are supported. Color correction is based on comparing histograms of the intersection area.
- **colorBuckets** (default 2000) - see *ColorMatch.length*.
- **colorDither** (default 0.95) - see *ColorMatch.dither*.
- **colorExclude** (default 0) - see *ColorMatch.exclude*.
- **colorFramesCount** (default 0) - the number of neighboring frames on both sides whose information is included in building the color correspondence map for color correction.
- **colorFramesDiff** (default 1) - the maximum mean squared deviation of color difference histograms between the sample and reference from the current frame to neighboring frames for color correction.
- **colorMaxDeviation** (default 0.5) - the maximum deviation of the total area between the current and neighboring frames for use in a frame sequence during color correction.
- **colorBufferedExtrapolation** - see *ColorMatch.bufferedExtrapolation*.
- **gradientColor** - see *ColorMatch.gradient*.
- **colorMatchTarget** - used for complex color correction scenarios within the *ColorMatchChain* filter.
- **adjustChannels** (default empty) - the channels in which to adjust color. Examples: "yuv", "y", "rgb", "rg".
- **matrix** (default empty) - if specified, the YUV image is converted to RGB using the specified matrix during processing.
- **sourceMatrix** and **overlayMatrix** (default empty) - options to override the matrix for the base or overlaid clip, used in conjunction with the *matrix* parameter.
- **downsize** and **upsize** (default *Spline36Resize* or *Spline36ResizeMT* if available) - functions for downsizing and upsizing images. If only one parameter is specified, the other takes the same value.
- **chromaResize** - UV channel resampling function in the same format as *downsize* and *upsize*, defaults to the value of *downsize*.
- **rotate** (default *BilinearRotate*) - the function for rotating the overlaid image.
- **preview** - output preview.
- **debug** - output overlay parameters and preview.
- **invert** - swap the base and overlaid clips, "inverting" the overlay parameters.
- **background** (default blank) - background fill method: *blank* (solid fill), *blur* (stretched image with fill), *inpaint* (not implemented).
- **backgroundClip** (default empty) - if specified, this clip is used as the background and must have the same resolution as the resulting clip.
- **blankColor** (default `0x008080` for YUV and `0x000000` for RGB) - the color in HEX format for filling empty areas.
- **backBalance** - a float value from -1 to 1 specifying the source of the blurred background if *background* is set to *blur*. -1 - base clip, 1 - overlaid clip.
- **backBlur** (default 15) - the blur strength if *background* is set to *blur*.
- **fullScreen** (default false) - the background image fills the entire image area, not just the combined image area.
- **edgeGradient** (default none) - gradient at image edges. *none* - disabled, *inside* - only within the combined area, *full* - everywhere.
- **bitDepth** (default unused) - the bit depth of the output image.

### ColorMatch
    ColorMatch(clip, clip reference, clip sample, clip sampleMask, clip referenceMask, bool greyMask, 
               float intensity, int length, float dither, string channels, 
               int frameBuffer, float frameDiff, bool bufferedExtrapolation, bool limitedRange, 
               int exclude, float gradient, int frame, int seed, string plane, string cacheId)
			   
Automatic color correction by matching color histograms. The input clip, *sample*, and *reference* clips must be in the same color space type (YUV or RGB). The input clip and *sample* clip must have the same bit depth. The input clip’s bit depth will change to match the *reference* clip’s bit depth. The filter yields good results only if the *sample* and *reference* clips have similar frame content. The filter is used within *OverlayRender*, cropping the *sample* and *reference* clips based on overlay parameters, but it can also be used independently, for example, to convert HDR to SDR or vice versa for clips with identical framing.

#### Parameters
- **clip** (required) - the input clip whose color will be corrected.
- **reference** (required) - the reference clip with the same content as the *sample* clip.
- **sample** - the clip compared to the reference clip. If not specified, the input clip will be used without cropping.
- **sampleMask** and **referenceMask** (default empty) - masks for including only pixels with the maximum value in the specified bit depth in processing, i.e., 255, 1023, etc.
- **greyMask** (default true) - mask based only on luma or all channels (used only for YUV).
- **intensity** (default 1) - the intensity of color correction.
- **length** (default 2000, max 1000000) - the histogram length; higher values improve quality but slow performance. The actual length is limited by the bit depth (256 for 8-bit, 1024 for 10-bit, etc.).
- **dither** (default 0.95) - dithering level from 0 (disabled) to 1 (aggressive). Applied only if the actual histogram length equals the bit depth. Adds ordered noise if one source color must be split into multiple colors based on their weights.
- **channels** (default yuv or rgb) - the planes or channels to process. Any combination of y, u, v or r, g, b is allowed (e.g., y, uv, r, br).
- **frameBuffer** (default 0) - the number of neighboring frames on both sides whose information is included in building the color correspondence map.
- **frameDiff** (default 1) - the maximum mean squared deviation of color difference histograms between the sample and reference from the current frame to neighboring frames.
- **bufferedExtrapolation** (default true) - whether to use neighboring frames from *frameBuffer* only for color extrapolation, i.e., for colors absent in the frame intersection area.
- **limitedRange** (default false) - TV range. Generally, there is no need to enable this even if the source clips are in TV range.
- **exclude** (default 0, max 100) - The minimum number of pixels of a single color used in color correction. Helps avoid random outliers. Not applied in dithering mode.
- **gradient** (default 0, max 1000000) - if greater than zero, activates gradient color correction mode, where four histograms are generated per frame, emphasizing different corners of the image. This allows uneven color correction across the image. Suitable primarily for clips with different original mastering. Higher values increase the effect.
- **frame** (default -1) - calculate the LUT based on a specific frame rather than the current one.
- **seed** (default is constant) - seed for dithering if the filter is used multiple times for rendering a single frame. Typically, there is no need to change it.
- **plane** and **cacheId** - used internally by *OverlayRender*.

### ColorMatchChain
    ColorMatchChain(clip, clip reference, string sampleSpace, string referenceSpace, clip chain, 
                    string preset, clip sample, clip sampleMask, clip referenceMask, bool greyMask, 
                    clip engine, clip sourceCrop, clip overlayCrop, bool invert, int iterations, 
                    string space, string format, string resize, int length, float dither, 
                    float gradient, int frameBuffer, float frameDiff, float frameMaxDeviation, 
                    bool bufferedExtrapolation, int exclude, int frame, bool matrixConversionHQ,
                    string inputChromaLocation, string outputChromaLocation)
					
Multi-step automatic color correction with support for statistics from *OverlayEngine*. Allows flexible color correction of clips before combining them via *OverlayRender*.  
For example, for YUV clips, first convert their color space to RGB HDR, correct the color, then convert to YUV HDR and correct the color again.  
Color space conversions can be performed using built-in AviSynth filters or z.lib. The latter can be used for SDR 709 to HDR 2020 conversions and vice versa.  
The transformation chain is defined using concatenated *ColorMatchStep* filters (see below). Built-in presets generate a ready-made chain.

#### Parameters
- **clip** (required) - the input clip whose color will be corrected.
- **reference** (required) - the reference clip. If the *engine* parameter is not specified, it must have the same content as the *sample* clip; otherwise, it must match the content in *OverlayEngine*.
- **sampleSpace** and **referenceSpace** (required) - the color spaces (matrices) of the source and reference, respectively, in z.lib or AviSynth terms, e.g., *2020ncl:st2084:2020:f* or *PC.2020*.
- **chain** (required) - concatenated *ColorMatchStep*-type clips defining the transformation chain.
- **preset** - preset for the filter’s main parameters to perform common transformation types:  
  - *HdrConversion* - single-step conversion of SDR 709 to HDR PQ 2020 or vice versa, depending on the input clips’ bit depth, with intermediate conversion to RGB 32-bit.  
  - *HdrConversionHQ* - two-step conversion of SDR 709 to HDR PQ 2020 or vice versa, depending on the input clips’ bit depth, with intermediate conversion to RGB 32-bit, then YUV 32-bit.  
  - *RgbYuv32* - two-step conversion of clips in any color space to RGB 32-bit, then YUV 32-bit.  
  - *YuvRgb32* - two-step conversion of clips in any color space to YUV 32-bit, then RGB 32-bit.  
  - *RgbYuv10* - two-step conversion of clips in any color space to RGB 10-bit, then YUV 10-bit.  
  - *YuvRgb10* - two-step conversion of clips in any color space to YUV 10-bit, then RGB 10-bit.
- **sample** - the clip compared to the reference clip. If not specified, the input clip will be used without cropping. When using *engine*, it must be the same clip used to collect statistics as the base.
- **sampleMask** and **referenceMask** (default empty) - masks for including only pixels with the maximum value in the specified bit depth in processing, i.e., 255, 1023, etc.
- **greyMask** (default true) - mask based only on luma or all channels (used only for YUV).
- **engine** - an *OverlayEngine*-type clip where the overlaid clip corresponds to the *reference* clip. For inversion, use *invert=true*.
- **sourceCrop** and **overlayCrop** - see *OverlayRender.sourceCrop* and *OverlayRender.overlayCrop*.
- **iterations** (default 1) - the number of chain transformation repetitions. Higher values may yield better results but reduce performance.
- **space** - the resulting clip’s color space, e.g., *2020ncl:st2084:2020:f* or *PC.2020*. Defaults to the last color space in the transformation chain.
- **format** - the resulting clip’s format, e.g., *YUV420P10*. Defaults to the last format in the transformation chain.
- **resize** (default BilinearResize) - the filter for resizing images when using the *engine* parameter; high quality is not required. If specified, it is also used for UV channel resampling, otherwise spline16 is used.
- **length** (default 2000) - see *ColorMatch.length*.
- **dither** (default 0.95) - see *ColorMatch.dither*.
- **gradient** (default 0) - see *ColorMatch.gradient*.
- **frameBuffer** (default 0) - see *ColorMatch.frameBuffer*.
- **frameDiff** (default 1) - see *ColorMatch.frameDiff*.
- **frameMaxDeviation** (default 0.5) - see *OverlayRender.colorMaxDeviation*.
- **bufferedExtrapolation** (default false) - see *ColorMatch.bufferedExtrapolation*.
- **exclude** (default 0) - see *ColorMatch.exclude*.
- **frame** - see *ColorMatch.frame*.
- **matrixConversionHQ** (default false) - high-quality color matrix conversion in YUV space with conversion to 32-bit.
- **inputChromaLocation** and **outputChromaLocation** - offset of UV channels relative to the luma channel for input and output clips. Possible values: left, center, top_left, top, bottom, bottom_left. Defaults to the frame property *_ChromaLocation*, or left if not specified.

### ColorMatchStep
    ColorMatchStep(string sample, string reference, string space, float intensity, clip merge, 
                   float weight, float chromaWeight, string channels, int length, float dither, 
                   float gradient, int frameBuffer, float frameDiff, int exclude, bool debug)
				   
A helper filter for defining the transformation chain within *ColorMatchChain*.

#### Parameters
- **sample** - the target format of the corrected clip, e.g., YV12.
- **reference** - the target format of the reference clip.
- **space** - the target color space in z.lib format or AviSynth matrix.
- **intensity** - overrides *ColorMatchChain.intensity*.
- **merge** - a child *ColorMatchStep*. If specified, the conversion result will be blended with weights from the *weight* and *chromaWeight* parameters.
- **channels** - see *ColorMatch.channels*.
- **length** - overrides *ColorMatchChain.length*.
- **dither** - overrides *ColorMatchChain.dither*.
- **gradient** - overrides *ColorMatchChain.gradient*.
- **frameBuffer** - overrides *ColorMatchChain.frameBuffer*.
- **frameDiff** - overrides *ColorMatchChain.frameDiff*.
- **exclude** - overrides *ColorMatchChain.exclude*.

### ComplexityOverlay
    ComplexityOverlay(clip source, clip overlay, string channels, int steps, float preference, 
                      bool mask, float smooth, bool invert, int threads, bool debug)
    
An independent filter for combining the most complex sections of two clips. Suitable for combining two low-quality sources. Clips must have identical framing, color, resolution, and color spaces.

#### Parameters
- **source** and **overlay** - input clips.
- **channels** (default yuv or rgb) - the planes or channels to process. Any combination of y, u, v or r, g, b is allowed (e.g., y, uv, r, br).
- **steps** (default 1) - the number of steps for forming the overlay mask.
- **preference** (default 0) - if greater than zero, the second clip is preferred; otherwise, the first clip is preferred. Recommended range: -1 to 1.
- **mask** (default false) - output the overlay mask instead of the combination.
- **smooth** (default 0) - blur the overlay mask to reduce sharpness.
- **invert** (default false) - invert the mask, i.e., prefer the least complex image sections.
- **threads** (.NET default) - the maximum number of threads.

### ComplexityOverlayMany
    ComplexityOverlayMany(clip source, clip[] overlays, string channels, int steps, bool invert, int threads, bool debug)
	
Similar to *ComplexityOverlay*, but allows combining an arbitrary number of clips.

### OverlayCompare
    OverlayCompare(clip engine, clip source, clip overlay, string sourceText, string overlayText, int sourceColor, 
                   int overlayColor, int borderSize, float opacity, int width, int height, bool debug)
				   
A filter for visualizing the combination of two clips.

#### Parameters
- **engine** (required) - an *OverlayEngine* clip.
- **source** (required) - the first, base clip.
- **overlay** (required) - the second, overlaid clip.
- **sourceText** (default "Source") - the name of the source clip.
- **overlayText** (default "Source") - the name of the overlay clip.
- **sourceColor** (default 0x0000FF) - the border color of the source clip.
- **overlayColor** (default 0x00FF00) - the border color of the overlay clip.
- **borderSize** (default 2) - the border size.
- **opacity** (default 0.51) - the opacity of the overlay clip.
- **width** (source clip width by default) - the output width.
- **height** (source clip height by default) - the output height.
- **debug** (default false) - print alignment settings.

### StaticOverlayRender
    StaticOverlayRender(clip source, clip overlay, float x, float y, float angle, float overlayWidth, float overlayHeight, 
                        string warpPoints, float diff, clip sourceMask, clip overlayMask, clip sourceCrop, clip overlayCrop,
                        string sourceChromaLocation, string overlayChromaLocation, clip extraClips, string preset, 
                        clip innerBounds, clip outerBounds, space overlayBalance, bool fixedSource, string overlayMode, 
                        int width, int height, string pixelType, int gradient, int noise, clip borderOffset,
                        clip srcColorBorderOffset, clip overColorBorderOffset, bool maskMode, float opacity, 
                        float colorAdjust, int colorBuckets, float colorDither, int colorExclude, int colorFramesCount, 
                        float colorFramesDiff, bool colorBufferedExtrapolation, string adjustChannels, float gradientColor, 
                        string matrix, string sourceMatrix, string overlayMatrix, string upsize, string downsize, 
                        string chromaResize, string rotate, bool preview, bool debug, bool invert, string background, 
                        clip backgroundClip, int blankColor, float backBalance, int backBlur, bool fullScreen, 
                        string edgeGradient, int bitDepth)

Similar to *OverlayRender*, but without *OverlayEngine*; the clip overlay parameters are specified manually.

### CustomOverlayRender
    CustomOverlayRender(clip engine, clip source, clip overlay, string function, int width, int height, bool debug)
	
A filter that allows visualizing the overlay result using a user-defined function with parameters `clip engine, clip source, clip overlay, int x, int y, float angle, int overlayWidth, int overlayHeight, float diff)`.

#### Parameters
- **engine** (required) - an *OverlayEngine* clip.
- **source** (required) - the first, base clip.
- **overlay** (required) - the second, overlaid clip.
- **function** (required) - the name of the user function. The function must have the following parameters:
- **width** (source clip width by default) - the output clip width.
- **height** (source clip height by default) - the output clip height.
- **debug** (default false) - debug mode.

### OverlayClip
    OverlayClip(clip clip, clip mask, clip crop, float opacity, string matrix, bool minor, int color, bool debug)
	
A helper filter that allows specifying an additional clip, mask, and opacity level for *OverlayRender*.

### Rect
    Rect(float left, float top, float right, float bottom, bool debug)
    
A helper filter that allows specifying parameters for the left, top, right, and bottom parts of the image, respectively. If only *left* is specified, its value is applied to all. If *left* and *top* are specified, *right* and *bottom* will equal them, respectively.

### ColorRangeMask
    ColorRangeMask(clip, int low, int high, bool greyMask)
A support filter that provides a mask clip based on a color range: white if the pixel value is between the *low* and *high* arguments. For YUV clips, only the luma channel is used. For RGB clips, all channels are processed independently. The output is a clip in the same color space. Limited range is not supported.

#### Parameters
- **input** (required) - the input clip.
- **low** (default 0) - the lower bound of the color range.
- **high** (default 0) - the higher bound of the color range.
- **greyMask** (default true) - mask based only on luma or all channels.

### BilinearRotate
    BilinearRotate(clip, float)
A support filter for rotation by angle with bilinear interpolation.

#### Parameters
- **input** (required) - the input clip.
- **angle** (required) - the rotation angle.

### OverlayMask
    OverlayMask(clip template, int width, int height, 
                int left, int top, int right, int bottom, 
                bool noise, bool gradient, int seed)
A support filter that provides a mask clip for overlay with a gradient or noise at the borders.

#### Parameters
- **template** (default empty) - if specified, the width, height, and color space will be taken from the template clip for the output.
- **width** - the output clip width if *template* is not specified.
- **height** - the output clip height if *template* is not specified.
- **left**, **top**, **right**, **bottom** - the border size.
- **noise** - noise generation on borders.
- **gradient** - gradient borders.
- **seed** - seed for noise generation.

### ExtractScenes
	ExtractScenes(string statFile, string sceneFile, int sceneMinLength, float maxDiffIncrease)
A filter to extract and save scene keyframes to a text file based on the statistics file of aligning the target clip to the *target.Trim(1,0)* clip.

#### Parameters
- **statFile** - the statistics file path.
- **sceneFile** - the scene file path.
- **sceneMinLength** (default 10) - the minimum scene length.
- **maxDiffIncrease** (default 15) - the scene detection *diff* value.

## User Functions
In addition to the filters listed above, user functions are defined in the *OverlayUtils.avsi* file.  
They are intended to simplify the process of synchronizing two clips and preparing sources for auto-alignment.

### aoShift
    aoShift(clip clp, int pivot, int length)
Shifts frames starting from the *pivot* frame number, removing previous frames or inserting blank ones depending on the direction.  
Positive *length* value - shift right with blank frames inserted before *pivot*.  
Negative *length* value - shift left with frames removed before *pivot*.

### aoDelay
    aoDelay(clip clp, int length)
A common case of *aoShift* for inserting or removing frames at the beginning of the clip.  
Positive *length* value - the number of blank frames to insert.  
Negative *length* value - the number of frames to remove.

### aoDelete
    aoDelete(clip clp, int start, int end)
Deletes a sequence of frames from *start* to *end* inclusive.

### aoReplace
    aoReplace(clip clp, clip replace, int start, int "end")
Replaces a sequence of frames with a similar one from another (synchronized) clip.  
The *end* parameter defaults to *start*, convenient for replacing a single frame.  
An explicitly specified zero in *end* is replaced with the last frame.

### aoOverwrite
    aoOverwrite(clip clp, clip scene, int frame)
Inserts another clip *scene* starting from the *frame* in the current clip with overwriting.

### aoInsert
    aoInsert(clip clp, clip insert, int start, int "end")
Inserts a segment from another clip without overwriting, starting at position *start* in both clips, ending at *end* in the inserted clip.  
The *end* parameter defaults to *start* (inserting a single frame).  
An explicitly specified zero in *end* is replaced with the last frame.

### aoTransition
    aoTransition(clip prev, clip next, int transitionStart, int transitionEnd, 
	             int "reverseTransitionStart", int "reverseTransitionEnd")
Smooth transition between synchronized clips.  
- *transitionStart* - the starting frame of the transition, inclusive.  
- Positive *transitionEnd* value - the last frame of the transition, inclusive.  
- Negative *transitionEnd* value - the transition duration in frames, inclusive.  
- *reverseTransitionStart* - the starting frame of the reverse transition; by default, there is no reverse transition.  
- *reverseTransitionEnd* - the last frame or duration of the reverse transition; by default, it equals the forward transition duration.

### aoTransitionScene
    aoTransitionScene(clip prev, clip next, int start, int end, int "length")
Smooth transition of a specified *length* between synchronized clips from *start* to *end* to replace a segment in the source clip, a specific case of *aoTransition*.

### aoBorders
    aoBorders(clip clp, int left, int top, int "right", int "bottom", 
	          int "refLength", int "segments", float "blur", float "dither")
Corrects color levels near frame borders using the *ColorMatch* filter.  
- *left*, *top*, *right*, *bottom* - determine how many pixels from the borders to process.  
- *right* and *bottom* default to *left* and *top*, respectively.  
- *refLength* (default 1) - sets the width/height of the area immediately adjacent to the corrected area to be used as a color reference.  
- *segments* (default 1) - allows splitting borders into segments and processing each separately with smooth transitions to avoid color correction errors over a larger frame area, especially if the frame contains many objects.  
- *blur* (default 0, max 1.5) - blurs pixels closest to the borders to reduce noise.  
- *dither* - the dithering level for the *ColorMatch* filter.

### aoInvertBorders
    aoInvertBorders(clip clp, int left, int top, int "right", int "bottom")
Inverts colors at frame borders, suitable for creating masks.  
In a YUV clip, only luma is processed.  
*right* and *bottom* default to *left* and *top*, respectively.

### aoInterpolate
    aoInterpolate(clip clp, int length, int "start", int "end", int "removeGrain")
Interpolates a sequence of clip frames from *start* to *end* inclusive, reducing or increasing their number using the *MVTools* plugin.

### aoInterpolateScene
    aoInterpolateScene(clip clp, int inStart, int inEnd, int outStart, int outEnd, int "removeGrain")
Interpolates a sequence of clip frames from *inStart* to *inEnd* inclusive, overwriting the result onto frames from *outStart* to *outEnd* using the *MVTools* plugin.

### aoInterpolateOne
    aoInterpolateOne(clip clp, int frame, bool "insert", int "removeGrain")
Inserts (by default) or replaces a single frame interpolated from neighboring frames using the *MVTools* plugin.

### aoDebug
    aoDebug(clip clp)
A debugging function for other package functions.  
For the *aoReplace* function, all frames except the replaced ones will be removed.

### aoExpand
    aoExpand(clip mask, int pixels, string mode, float "blur")
	
Expands the black mask (*mode=darken*) or white mask (*mode=lighten*).

## Usage Scenarios

### Clip Preparation
1. Before performing any actions to combine or color-correct two clips, they must be synchronized frame-by-frame.  
   AviSynth’s built-in functionality is suitable for this: *Trim*, *DeleteFrame*, *DuplicateFrame*, etc.  
   Additionally, the *aoShift*, *aoDelay*, *aoDelete*, *aoReplace*, *aoOverwrite*, and *aoInsert* functions can be used.  
2. If the video has borders, they must be cropped using the *Crop* function.  
3. Clips must be in the same color space type - YUV or RGB; YUV is recommended.  
4. If there are overexposures or darkening at the borders, you can try restoring brightness using *aoBorders*.  
5. Clips can be prepared in separate *.avs* files, which should then be imported into the main script using the *Import* function.  
6. If the source videos undergo heavy filtering that significantly impacts performance, it is recommended to encode them in a lossless format before processing in *OverlayEngine*.

### Combining Clips with Auto-Alignment
1. First, decide which clip to use as the base. Typically, this is the source with greater frame content.
2. For automatic combination, both clips must have the same bit depth, e.g., 8-bit or 10-bit. If one source is in HDR and the other in SDR, it is recommended to convert the HDR clip to SDR. This is especially convenient if an SDR alternative with the same framing already exists for the HDR clip; then, the *ColorMatchChain* filter with the *HdrConversion* preset can be used.
3. Next, prepare a script template for combining with debug mode enabled to evaluate the result and adjust alignment parameters:
    ```
    OM=Import("OM.avs")
    WS=Import("WS.avs")
    WStest = WS.ColorMatchChain(Import("WS_SDR.avs"), preset = "HdrConversion") # Example of preliminary color correction with bit depth change for use in OverlayEngine instead of the WS clip
    config = OverlayConfig() # Add necessary parameters as needed, e.g., aspectRatio1, aspectRatio2, preset, etc.
    engine = OverlayEngine(OM, WS, configs = config)
    engine.OverlayRender(OM, WS, debug = true, preset = "difference")
4. In AvsPmod, review the result. The intersection area should be as uniformly gray as possible without contours. If necessary, adjust the *OverlayConfig* and *OverlayEngine* parameters.
5. After determining the optimal parameters, specify a statistics file and run a test pass to collect statistics. In AvsPmod: *Video/Tools/Run analysis pass*. Temporarily commenting out *OverlayRender* will improve performance:
    ```
    config = OverlayConfig(preset = "high", aspectRatio1 = 2.38, aspectRatio2 = 2.42) # Selected parameters
    engine = OverlayEngine(OM, WS, configs = config, statFile = "WS.stat")
    engine#.OverlayRender(OM, WS, debug = true, preset = "difference") # OverlayRender commented out with #
6. After the test run, review and edit the result using the editor by adding the *editor=true* parameter to *OverlayEngine*.
7. Now, prepare the final video by uncommenting *OverlayRender* and adding the necessary parameters:
    ```
    engine = OverlayEngine(OM, WS, configs = config, statFile = "WS.stat")
    engine.OverlayRender(OM, WS, debug = false, preset = "FullFrame", colorAdjust = 0) # Maximize frame content with color correction based on the base clip
8. If the overlaid clip is of lower quality than the base clip, make it transparent in *OverlayRender*, i.e., *opacity=0*.

### Combining Sources with Identical Frame Content for Improved Quality
To do this, use the *ComplexityOverlay* or *ComplexityOverlayMany* filter if there are multiple sources:

    ComplexityOverlay(clip1, clip2, steps = 2, smooth = 0.5)

It is advisable to use *ComplexityOverlay* before applying filters that globally affect the image, such as *ColorMatch*. If filtering is needed, first create an overlay mask in *mask=true* mode, then apply the filters, and overlay the clips using the mask:
    
    mask = ComplexityOverlay(clip1, clip2, steps = 2, smooth = 0.5, mask = true)
    clip2 = clip2.ColorMatch(clip1)
    clip1.Overlay(clip2, mask = mask)

### Automatic Color Correction
1. The *ColorMatch* and *ColorMatchChain* filters are designed for this purpose.
2. If the clips have different dynamic framing, they must first be aligned using *OverlayEngine*.
3. If the source clips are in the same color space and the color has undergone only linear transformations, *ColorMatch* or *OverlayRender* is sufficient:
    
    ```target.ColorMatch(reference)```
4. For complex multi-step color correction, use *ColorMatchChain*:
    
    ```target.ColorMatchChain(reference, preset = "RgbYuv32")```

### HDR <-> SDR Conversion
Use *ColorMatchChain* with the *HdrConversion* or *HdrConversionHQ* preset.
If the framing is dynamic, prepare *OverlayEngine* and specify it in the *engine* parameter.

### Logo Removal
1. If the video has a logo, prepare a mask clip where the logo is strictly black (zero value).
2. It is advisable to blur the edges to ensure smooth transitions during overlay in *OverlayRender*. The blurred edges should extend beyond the logo:
    mask = ImageSource("logo.png", end = 0, pixel_type = "Y8") # Static mask from a single frame
    mask = mask.aoExpand(3, "darken", 1.5) # Optional mask expansion with blur
3. The mask clip can be dynamic and have the same length as the base clip.
4. After creating the mask, use it in all filters that support it for the base or overlaid clip: *OverlayEngine*, *ColorMatch*, *OverlayRender*, etc.

### Clip Comparison
1. To check frame-by-frame that two clips have identical framing/are fully synchronized/do not contain defective frames, use *OverlayEngine* after bringing both clips to the same resolution:
    ```
    clip1 = clip1.BilinearResize(clip2.width, clip2.height)
    OverlayEngine(clip1, clip2, maxDiff = 1024, sceneDiffTolerance = 10000, statFile = "diff.stat")
2. After the test run, reduce *maxDiff* to the minimum level and check which frames exceed it in the editor (checkbox *Defective only*):
    ```OverlayEngine(clip1, clip2, maxDiff = 5, statFile = "diff.stat", editor = true)```

## Changelog
### 21.04.2025 v0.7.7
1. *OverlayEngine*: Fixed auto-alignment when source resolutions differ significantly.
2. The *exclude* parameter for color correction is now an integer, specifying the minimum number of pixels of a single color instead of their proportion in the entire image, as its operation should not depend on resolution.

### 17.04.2025 v0.7.6
1. *ComplexityOverlay* and *ComplexityOverlayMany* bugfixes.

### 16.04.2025 v0.7.5
1. *OverlayEngine*: improved auto-alignment in subpixel mode.
2. *OverlayRender*: added *chromaResize* parameter for explicit selection of UV channel resampling algorithm, defaults to the value of the *downsize* parameter.
3. *OverlayRender*: added *sourceChromaLocation*, *overlayChromaLocation*, and *OverlayClip*.*chromaLocation* parameters for explicit specification of UV channel offset relative to the luma channel. Valid values: left, center, top_left, top, bottom_left, bottom. Defaults to the frame property *_ChromaLocation*, or left if not specified.
4. *ColorMatchChain*: added *inputChromaLocation* and *outputChromaLocation* parameters for explicit specification of UV channel offset relative to the luma channel. Defaults to the frame property *_ChromaLocation*, or left if not specified.
5. *ColorMatchChain*: UV channel resampling now uses the same algorithm specified in the *resize* parameter, or spline16 by default.
6. Filters now propagate input clip frame properties whenever possible.
7. Updated version of the AvsFilterNet library.

### 06.04.2025 v0.7.4
1. *ColorMatchChain*: improved color extrapolation.
2. *OverlayEngine*: fixed prediction when *sceneFile* is present.
3. *OverlayEngine*: fixed the functionality of the *colorAdjust* parameter in certain cases.

### 04.04.2025 v0.7.3
1. *OverlayEngine*: increased default values for the *frameDiffTolerance* parameter.  
2. *OverlayEngine*: improved scene stabilization.  
3. *OverlayRender*: fixed color correction stabilization when using the *colorFramesCount* parameter.  
4. *OverlayRender*: the *noise* parameter has been made integer-based again and is now applied within the frame instead of *gradient* when specified, while *gradient* in this case is applied only to the edges.  
5. *OverlayRender*: fixed gradient overlay on frame edges.  
6. *OverlayRender*: improved support for certain color spaces.  
7. *OverlayRender*: fixed functionality of the *overlayCrop* parameter.  
8. *OverlayRender*: fixed handling of masks.  
9. *ColorMatch**: improved performance when using the *frameBuffer* parameter.

### 25.03.2025 v0.7.2
1. *OverlayEngine*: scan prediction fix.
2. Overall performance increased.

### 24.03.2025 v0.7.1
1. *OverlayEngine*: fixed bugs in auto-alignment and prediction

### 15.03.2025 v0.7
1. All functions of the *ColorAdjust* filter have been moved to the *ColorMatch* filter. The *ColorAdjust* filter has been discontinued.
2. *OverlayEngine*: new prediction and stabilization algorithm with presets.
3. *OverlayRender*: new overlay algorithm using the helper *LayerMask* filter.
4. *OverlayRender*: parameters *StabilizationDiffTolerance*, *StabilizationAreaTolerance*, *StabilizationLength* for image stabilization.
5. *OverlayConfig*: presets.
6. *OverlayConfig*: additional image rotation parameters.
7. *ComplexityOverlay* and *ComplexityOverlayMany* - HDR support and overlay mask inversion parameters.
8. Significantly improved performance in most filters.
9. New *ColorMatchChain* and *ColorMatchStep* filters for multi-step color correction.
10. *OverlayRender*: parameters for correcting clip cropping after applying *OverlayEngine*.
11. *OverlayEditor*: UI improvements, accounting for the latest filter changes.
12. The *simd.dll* library has been discontinued and is now embedded in *AutoOverlayNative.dll*.
13. Support for RGBP and 32-bit HDR in most filters.

### 13.05.2024 v0.6.2
1. *ColorMatch*: new filter for color matching with 32-bit color support.
2. *OverlayEngine*, *OverlayRender*, *OverlayMask*: improved HDR and RGB support.
3. *OverlayRender*: *BitDepth* parameter to explicitly specify the resulting clip’s bit depth.
4. *OverlayRender*: presets.
5. *OverlayRender*: fixed artifacts at image borders with color subsampling.
6. *OverlayRender*: uses the *ColorMatch* filter instead of *ColorAdjust* for 32-bit bit depth.
7. *MathNet.Numerics* updated to version 5.0.0.
8. Fixed resource release.

### 02.12.2023 v0.6.1
1. *OverlayEngine*: warp and colorAdjust bugfix.

### 01.12.2023 v0.6
1. *OverlayRender*: removed the *mode* parameter.
2. *OverlayRender*: added parameters *extraClips*, *innerBounds*, *outerBounds*, *overlayBalanceX*, *overlayBalanceY*, *fixedSource*, *overlayOrder*, *maskMode*, *colorInterpolation*, *colorExclude*, *backgroundClip*, *backBalance*, *fullScreen*, *edgeGradient*.
3. *OverlayRender*: replaced the *background* parameter with *backBalance*, with the *background* parameter’s purpose changed.
4. *OverlayRender*: added support for overlaying an arbitrary number of clips.
5. *OverlayEngine*: renamed *panScanDistance* and *panScanScale* to *scanDistance* and *scanScale*.
6. Added the *ComplexityOverlayMany* filter.