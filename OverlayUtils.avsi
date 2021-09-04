# AutoOverlay AviSynth plugin v0.5

# Shift frames starting from pivot to delete previous frames or insert blank frames depending on direction
# Positive length - to the right inserting blank frames before pivot
# Negative length - to the left deleting frames before pivot
function aoShift(clip clp, int pivot, int length) {
	clp
	assert(pivot >= 0, "'pivot' should be non-negative")
	assert(pivot <= FrameCount, "'pivot' should be less than or equal to frame count")
	assert(-length < FrameCount, "Abs of negative 'length' should be less than clip length")
	if (length == 0) {
		return clp
	}
	if (pivot <= -length) {
		pivot = 0
	}
	if (pivot + length <= 0) {
		return length > 0 ? BlankClip(clp, length) + clp : Trim( - pivot - length,0)
	}
	if (pivot == FrameCount) {
		return length > 0 ? clp + BlankClip(clp, length) : clp.Trim(0, FrameCount + length - 1)
	}
	prePivot = length > 0 ? pivot - 1 : pivot + length - 1
	Trim(0, prePivot == 0 ? -1 : prePivot)
	if (length > 0) {
        last + BlankClip(clp, length)
	}
	return last + clp.Trim(pivot, 0)
}

# A special case of 'aoShift' function to insert blank or delete frames at clip beginning
# Positive length - how many blank frames insert 
# Negative length - how many frames delete
function aoDelay(clip clp, int length) {
    return aoShift(clp, 0, length)
}

# Delete scene from 'start' to 'end' frame inclusive
function aoDelete(clip clp, int start, int end) {
	clp
	assert(start >= 0 && start < clp.FrameCount, "'start' should be non-negative and less than clip length")
	assert(end >= start && end < clp.FrameCount, "'end' should be greater than or equal to 'start' and less than clip length")
	if (start == 0) {
		return Trim(end + 1,0)
	}
	if (end == FrameCount - 1) {
		return Trim(0, start - 1)
	}
	start = start == 1 ? 0 : start
	return Trim(0, start - 1) + Trim(end + 1,0)
}

# Replacing frame sequence from 'start' to 'end' inclusive from another (synchronized) clip starting from same frame
# 'end' equal to 'start' by default (one frame replacing)
# explicit zero 'end' means last frame of the clip
function aoReplace(clip clp, clip replace, int start, int "end") {
	end = default(end, -1)
	if (end == 0) {
		end = clp.FrameCount - 1
	} else if (end == -1) {
        end = start
	}
	assert(start >= 0 && start < clp.FrameCount, "'start' should be non-negative and less than clip length")
	assert(end >= start && end < clp.FrameCount, "'end' should be greater than or equal to 'start' and less than clip length")
	assert(end < replace.FrameCount, "'replace' clip should be longer than 'end'")
	
	result = replace.Trim(start, end == 0 ? -1 : end)
	if (start > 0) {
		start = start == 1 ? 0 : start
		result = clp.Trim(0, start - 1) + result
	}
	if (end < clp.FrameCount - 1) {
		result = result + clp.Trim(end + 1, 0)
	}
	return result
}

# Insert whole clip as a 'scene' from specified 'frame' of input clip with overwriting
function aoOverwrite(clip clp, clip scene, int frame) {
	assert(frame >= 0 && frame < clp.FrameCount, "'frame' should be non-negative and less than clip length")
    if (frame == 0) {
        return scene + clp.Trim(frame + scene.FrameCount(), 0)
    }
    clp.Trim(0, frame == 1 ? -1 : (frame - 1)) + scene
    if (FrameCount < clp.FrameCount) {
        last + clp.Trim(frame + scene.FrameCount(), 0)
    }
}

# 'Insert' scene from another clip from 'start' to 'end' inclusive without overwiting
# 'end' equal to 'start' by default (one frame inserting)
# explicit zero 'end' means last frame of the clip
function aoInsert(clip clp, clip insert, int start, int "end") {
    end = default(end, -1)
	if (end == 0) {
		end = clp.FrameCount - 1
	} else if (end == -1) {
        end = start
	}
    assert(start >= 0 && start < clp.FrameCount, "'start' should be non-negative and less than clip length")
	assert(end >= start && end < clp.FrameCount, "'end' should be greater than or equal to 'start' and less than clip length")
	assert(end < insert.FrameCount, "'insert' clip should be longer than 'end'")
    scene = insert.Trim(start, end == 0 ? -1 : end)
    if (start == 0) {
        return scene + clp
    }
	if (start == clp.FrameCount) {
		return clp + scene
	}
    return clp.Trim(0, start == 1 ? -1 : (start - 1)) + scene + clp.Trim(start, 0)
}

# Smooth transition between synchronized clips
# transitionStart - first frame of transition
# Positive 'transitionEnd' - last frame of transition
# Negative 'transitionEnd' - transition length
# reverseTransitionStart -  first frame of reverse transition, no reverse transition by default
# reverseTransitionEnd - last frame or length of reverse transition, equal to direct transition length by default
function aoTransition(clip prev, clip next, int transitionStart, int transitionEnd, int "reverseTransitionStart", int "reverseTransitionEnd") {
    reverseTransitionStart = default(reverseTransitionStart, 0)
    reverseTransitionEnd = default(reverseTransitionEnd, transitionEnd > 0 ? transitionStart - transitionEnd : reverseTransitionStart)
    transitionEnd = transitionEnd >= 0 ? transitionEnd : transitionStart - transitionEnd
    reverseTransitionEnd = reverseTransitionEnd > 0 ? reverseTransitionEnd : reverseTransitionStart - reverseTransitionEnd
    
    assert(transitionStart > 0 && transitionStart < prev.FrameCount && transitionStart < next.FrameCount, \
                "'transitionStart' should be non-negative and less than clips length")
    assert(reverseTransitionStart == 0 || reverseTransitionStart >= transitionStart && reverseTransitionStart < prev.FrameCount && reverseTransitionStart < next.FrameCount, \
                "'reverseTransitionStart' should be greater than 'reverseTransitionStart' and less than clips length")
    
    res = prev.Trim(0, transitionStart == 1 ? -1 : (transitionStart - 1))
    total = transitionEnd - transitionStart + 1
    for (i=0, total - 1) {
        frameNum = transitionStart + i
        res = res + prev.Merge(next, (i + 1)/(total + 1.0)).Trim(frameNum, frameNum)
    }
    res + next.Trim(transitionEnd + 1, 0)
    if (reverseTransitionStart != 0) {
        aoTransition(prev, reverseTransitionStart, reverseTransitionEnd)
    }
}

# Smooth transition of specified 'length' between synchronized clips for scene replacing from 'start' to 'end' frame
function aoTransitionScene(clip prev, clip next, int start, int end, int "length") {
    length = default(length, 10)
    assert(length >= 0, "Length should be non-negative")
    return aoTransition(prev, next, start - length, start - 1, end + 1, end + length)
}

# Fix invalid color level near borders using ColorAdjust filter
# left, top, right, bottom params specified how much lines or columns need to fix
# right and bottom are optional and equal to left and right by default
# refLength (default 1) describes number of columns or lines after LTRB that will be used as color reference
# segments (default 1) allows to split image into blocks and process them separatly with smooth transition to avoid invalid color adjustment when image is complicated
# blur (default 0, max 1.5) makes nearest to edge pixels more smooth to avoid noise
# dither - dithering level for ColorAdjust filter
function aoBorders(clip clp, int left, int top, int "right", int "bottom", int "refLength", int "segments", float "blur", float "dither") {

	right = default(right, left)
	bottom = default(bottom, top)
	refLength = Default(refLength, 1)
	segments = default(segments, 1)
	blur = default(blur, 0)
	dither = default(dither, 1)
	
	clp
	
	assert(segments > 0 && segments < min(width, height)/2, "segments should be non-negative and less than min(width, height)/2")
	assert(refLength > 0 && refLength < min(width, height), "refLength should be non-negative and less than min(width, height)")
	assert(left >= 0 && left < Width - refLength, "left should be non-negative and less than (width-refLength)")
	assert(right >= 0 && right < Width - left - refLength, "right should be non-negative and less than (width-left-refLength)")
    assert(top >= 0 && top < Height - refLength, "top should be non-negative and less than (height-refLength)")
	assert(bottom >= 0 && bottom < Height - top - refLength, "bottom should be non-negative and less than (height-top-refLength)")
	
	if (top > 0) {
        length = top + refLength
        crop = Height - length
        resample = length > 6 ? "bicubic" : (length > 2 ? "bilinear" : "point")
        canvas = clp.Crop(0, 0, 0, crop%2 - crop)
        canvas = canvas.IsRgb() ? canvas : canvas.ConvertToYUV444(chromaresample = resample)
        target = canvas.Crop(0, 0, 0, refLength%2 - refLength)
        canvas = canvas.Crop(0, 0, 0, -crop%2)
        gradient = NULL 
        
        stepSize = Width/(segments + 1)
        blockSize = stepSize*2

        for (i = 0, segments - 1) {
            segmentOffset = stepSize*i
            segment = canvas.Crop(stepSize*i, 0, -stepSize*(segments - i - 1), 0)
            reference = segment.Crop(0, top, 0, 0)
            for (step = 0, top - 1) {
                stepOffset = top - step - 1
                sample = segment.Crop(0, stepOffset, 0, - step - refLength)
                adjusted = sample.ColorAdjust(sample, reference, dither = dither, seed = BitAnd(13*step, 27*i))
                segment = segment.Overlay(adjusted, 0, stepOffset)
            }
            if (i == 1) {
                gradient = OverlayMask(segment, left = stepSize, gradient = true)
            }
            target = i == 0 ? target.Overlay(segment, segmentOffset, 0) : target.Overlay(segment, segmentOffset, 0, mask = gradient)
        }
        if (blur > 0) {
            target = target.Overlay(target.Blur(blur), mask = OverlayMask(target, bottom = top, gradient = true))
        }
        Overlay(target)
	}
	if (bottom > 0) {
        length = bottom + refLength
        crop = Height - length
        resample = length > 6 ? "bicubic" : (length > 2 ? "bilinear" : "point")
        canvas = clp.Crop(0, crop - crop%2, 0, 0)
        canvas = canvas.IsRgb() ? canvas : canvas.ConvertToYUV444(chromaresample = resample)
        target = canvas.Crop(0, refLength - refLength%2, 0, 0)
        canvas = canvas.Crop(0, crop%2, 0, 0)
        gradient = NULL 
        
        stepSize = Width/(segments + 1)
        blockSize = stepSize*2

        for (i = 0, segments - 1) {
            segmentOffset = stepSize*i
            segment = canvas.Crop(stepSize*i, 0, -stepSize*(segments - i - 1), 0)
            reference = segment.Crop(0, 0, 0, -bottom)
            for (step = 0, bottom - 1) {
                stepOffset = bottom - step - 1
                sample = segment.Crop(0, step + refLength, 0, -stepOffset)
                adjusted = sample.ColorAdjust(sample, reference, dither = dither, seed = BitAnd(13*step, 27*i))
                segment = segment.Overlay(adjusted, 0, step + refLength)
            }
            if (i == 1) {
                gradient = OverlayMask(segment, left = stepSize, gradient = true)
            }
            y = target.Height - segment.Height
            target = i == 0 ? target.Overlay(segment, segmentOffset, y) : target.Overlay(segment, segmentOffset, y, mask = gradient)
        }
        if (blur > 0) {
            target = target.Overlay(target.Blur(blur), mask = OverlayMask(target, top = bottom, gradient = true))
        }
        Overlay(target, 0, Height - target.Height)
	}
	if (left > 0) {
        length = left + refLength
        crop = Width - length
        resample = length > 4 ? "bicubic" : (length > 2 ? "bilinear" : "point")
        canvas = clp.Crop(0, 0, crop%2 - crop, 0)
        canvas = canvas.IsRgb() ? canvas : canvas.ConvertToYUV444(chromaresample = resample)
        target = canvas.Crop(0,0,-refLength + refLength%2,0)
        canvas = canvas.Crop(0, 0, -crop%2, 0)
        gradient = NULL 
        
        stepSize = Height/(segments + 1)
        blockSize = stepSize*2

        for (i = 0, segments - 1) {
            segmentOffset = stepSize*i
            segment = canvas.Crop(0, stepSize*i, 0, -stepSize*(segments - i - 1))
            reference = segment.Crop(left, 0, 0, 0)
            for (step = 0, left - 1) {
                stepOffset = left - step - 1
                sample = segment.Crop(stepOffset, 0, -step - refLength, 0)
                adjusted = sample.ColorAdjust(sample, reference, dither = dither, seed = BitAnd(13*step, 27*i))
                segment = segment.Overlay(adjusted, stepOffset)
            }
            if (i == 1) {
                gradient = OverlayMask(segment, top = stepSize, gradient = true)
            }
            target = i == 0 ? target.Overlay(segment, 0, segmentOffset) : target.Overlay(segment, 0, segmentOffset, mask = gradient)
        }
        if (blur > 0) {
            target = target.Overlay(target.Blur(blur), mask = OverlayMask(target, right = left, gradient = true))
        }
        Overlay(target)
	}
	if (right > 0) {
        length = right + refLength
        crop = Width - length
        resample = length > 4 ? "bicubic" : (length > 2 ? "bilinear" : "point")
        canvas = clp.Crop(crop - crop%2, 0, 0, 0)
        canvas = canvas.IsRgb() ? canvas : canvas.ConvertToYUV444(chromaresample = resample)
        target = canvas.Crop(refLength - refLength%2, 0, 0, 0)
        canvas = canvas.Crop(crop%2, 0, 0, 0)
        gradient = NULL 
        
        stepSize = Height/(segments + 1)
        blockSize = stepSize*2

        for (i = 0, segments - 1) {
            segmentOffset = stepSize*i
            segment = canvas.Crop(0, stepSize*i, 0, -stepSize*(segments - i - 1))
            reference = segment.Crop(0, 0, -right, 0)
            for (step = 0, right - 1) {
                stepOffset = right - step - 1
                sample = segment.Crop(step + refLength, 0, -stepOffset, 0)
                adjusted = sample.ColorAdjust(sample, reference, dither = dither, seed = BitAnd(13*step, 27*i))
                segment = segment.Overlay(adjusted, step + refLength)
            }
            if (i == 1) {
                gradient = OverlayMask(segment, top = stepSize, gradient = true)
            }
            x = target.Width - segment.Width
            target = i == 0 ? target.Overlay(segment, x, segmentOffset) : target.Overlay(segment, x, segmentOffset, mask = gradient)
        }
        if (blur > 0) {
            target = target.Overlay(target.Blur(blur), mask = OverlayMask(target, left = right, gradient = true))
        }
        Overlay(target, Width - target.Width)
	}
	return last
}

# Invert color at borders, useful for masks
# If input is YUV only luma will be processed 
# right and bottom equal to left and top by default
function aoInvertBorders(clip clp, int left, int top, int "right", int "bottom") {
	right = default(right, left)
	bottom = default(bottom, top)

	clp.isRgb() ? clp : clp.ExtractY()
	inverted = invert()
	if (left > 0) {
		Overlay(inverted.Crop(0,0,left-width,0),0,0)
	}
	if (top > 0) {
		Overlay(inverted.Crop(0,0,0,top-height),0,0)
	}
	if (right > 0) {
		Overlay(inverted.Crop(width-right,0,0,0),width-right,0)
	}
	if (bottom > 0) {
		Overlay(inverted.Crop(0,height-bottom,0,0),0, height-bottom)
	}
	return clp.isRgb() ? clp : clp.MergeLuma(last)
}

# Interpolate frame sequence from 'start' to 'end' to the target 'length' with MVTools
function aoInterpolate(clip clp, int length, int "start", int "end", int "removeGrain") {
    clp    
    start = default(start, 0)
    end = default(end, FrameCount - 1)
    removeGrain = default(removeGrain, 22)
    
    assert(removeGrain >= 0 && removeGrain <= 28, "removeGrain should be non-negative and less than or equal to 28")
    assert(length > 2, "start should be greater than 2")
    assert(start >= 0 && start < FrameCount, "start should be non-negative and less than clip length")
    assert(end > start && end < FrameCount, "end should be greater than end and less than clip length")
    
    inTotal = end - start
    outTotal = length - 1
    AssumeFps(inTotal)
    Trim(start, end)
    prefiltered = removeGrain == 0 ? last : RemoveGrain(removeGrain)
    super = MSuper(hpad = 16, vpad = 16, levels = 1)
    superfilt = MSuper(prefiltered, hpad = 16, vpad = 16)
    backward = MAnalyse(superfilt, isb = true, blksize = 16, overlap = 4, search = 3, dct = 0)
    forward = MAnalyse(superfilt, isb = false, blksize = 16, overlap = 4, search = 3, dct = 0)
    forward_re = MRecalculate(super, forward, blksize = 8, overlap = 2, thSAD = 100)
    backward_re = MRecalculate(super, backward, blksize = 8, overlap = 2, thSAD = 100)
    out = MFlowFps(super, backward_re, forward_re, num = outTotal, den = 1, blend = false, ml = 100, mask = 2)
    assert(out.FrameCount == length, "Internal error")
    return out.AssumeFps(clp.FrameRateNumerator, clp.FrameRateDenominator)
}

# Interpolate frame sequence from 'inStart' to 'inEnd' to the target scene from 'outStart' to 'outEnd' with MVTools
function aoInterpolateScene(clip clp, int inStart, int inEnd, int outStart, int outEnd, int "removeGrain") {
    clp    
    removeGrain = default(removeGrain, 22)
    
    assert(inStart >= 0 && inStart < FrameCount, "inStart should be non-negative and less than clip length")
    assert(inEnd > inStart && inEnd < FrameCount, "inEnd should be greater than inStart and less than clip length")
    assert(outStart >= 0 && outStart < FrameCount, "outStart should be non-negative and less than clip length")
    assert(outEnd > outStart && outEnd < FrameCount, "outEnd should be greater than outStart and less than clip length")
    
    scene = aoInterpolateScene(clp, outEnd - outStart + 1, inStart, inEnd)
    return clp.Trim(0, outStart - 1) + scene + clp.Trim(outEnd + 1, 0)
}

# Insert by default or replace with interpolated frame from the nearest with MVTools
function aoInterpolateOne(clip clp, int frame, bool "insert", int "removeGrain") {
    clp    
    insert = default(removeGrain, true)
    removeGrain = default(removeGrain, 22)
    
    assert(frame > 0 && frame < FrameCount - 1, "frame should be greater than zero and less than clip length minus 1")
    
    if (!insert) {
        DeleteFrame(frame)
    }
    interpolated = aoInterpolate(3, frame - 1, frame)
    return Trim(0, frame - 1) + interpolated.Trim(1,1) + Trim(frame, 0)
}
