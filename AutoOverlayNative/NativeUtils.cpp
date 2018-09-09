#define _USE_MATH_DEFINES

#include "Stdafx.h"
#include <iostream>
#include <math.h>

namespace AutoOverlay {

	const double EPSILON = std::numeric_limits<double>::epsilon();

	void BilinearRotate1(
		IntPtr srcImage, int srcWidth, int srcHeight, int srcStride,
		IntPtr dstImage, int dstWidth, int dstHeight, int dstStride,
		double angle)
	{
		unsigned char* src = reinterpret_cast<unsigned char*>(srcImage.ToPointer());
		unsigned char* dst = reinterpret_cast<unsigned char*>(dstImage.ToPointer());

		double oldXradius = (srcWidth - 1) / 2.0;
		double oldYradius = (srcHeight - 1) / 2.0;
		double newXradius = (dstWidth - 1) / 2.0;
		double newYradius = (dstHeight - 1) / 2.0;
		int dstOffset = dstStride - dstWidth;

		double angleRad = -angle * M_PI / 180;
		double angleCos = cos(angleRad);
		double angleSin = sin(angleRad);

		int ymax = srcHeight - 1;
		int xmax = srcWidth - 1;

		double cy = -newYradius;
		for (int y = 0; y < dstHeight; y++, cy++, dst += dstStride)
		{
			// do some pre-calculations of source points' coordinates
			// (calculate the part which depends on y-loop, but does not
			// depend on x-loop)
			double tx = angleSin * cy + oldXradius;
			double ty = angleCos * cy + oldYradius;

			double cx = -newXradius;
			for (int x = 0; x < dstWidth; x++, cx++)
			{
				// coordinates of source point
				double ox = tx + angleCos * cx;
				double oy = ty - angleSin * cx;

				// top-left coordinate
				int ox1 = (int)ox;
				int oy1 = (int)oy;

				// validate source pixel's coordinates
				if (ox1 >= 0 && oy1 >= 0 && ox1 < srcWidth && oy1 < srcHeight)
				{
					// bottom-right coordinate
					int ox2 = ox1 == xmax ? ox1 : ox1 + 1;
					int oy2 = oy1 == ymax ? oy1 : oy1 + 1;

					double dx1 = std::max(0.0, ox - ox1);
					double dx2 = 1.0 - dx1;

					double dy1 = std::max(0.0, oy - oy1);
					double dy2 = 1.0 - dy1;

					// get four points
					unsigned char* p = src + oy1 * srcStride;
					unsigned char p1 = p[ox1];
					unsigned char p2 = p[ox2];

					p = src + oy2 * srcStride;
					unsigned char p3 = p[ox1];
					unsigned char p4 = p[ox2];

					dst[x] = static_cast<unsigned char>(dy2 * (dx2 * p1 + dx1 * p2) + dy1 * (dx2 * p3 + dx1 * p4));
				}
			}
		}
	}

	void NativeUtils::BilinearRotate(
		IntPtr srcImage, int srcWidth, int srcHeight, int srcStride,
		IntPtr dstImage, int dstWidth, int dstHeight, int dstStride,
		double angle, int pixelSize)
	{
		if (pixelSize == 1)
		{
			BilinearRotate1(
				srcImage, srcWidth, srcHeight, srcStride,
				dstImage, dstWidth, dstHeight, dstStride,
				angle);
			return;
		}
		unsigned char* src = reinterpret_cast<unsigned char*>(srcImage.ToPointer());
		unsigned char* dst = reinterpret_cast<unsigned char*>(dstImage.ToPointer());

		double oldXradius = (srcWidth - 1) / 2.0;
		double oldYradius = (srcHeight - 1) / 2.0;
		double newXradius = (dstWidth - 1) / 2.0;
		double newYradius = (dstHeight - 1) / 2.0;
		int dstOffset = dstStride - dstWidth * pixelSize;

		double angleRad = -angle * M_PI / 180;
		double angleCos = cos(angleRad);
		double angleSin = sin(angleRad);

		int ymax = srcHeight - 1;
		int xmax = srcWidth - 1;

		double cy = -newYradius;
		for (int y = 0; y < dstHeight; y++, cy++)
		{
			// do some pre-calculations of source points' coordinates
			// (calculate the part which depends on y-loop, but does not
			// depend on x-loop)
			double tx = angleSin * cy + oldXradius;
			double ty = angleCos * cy + oldYradius;

			double cx = -newXradius;
			for (int x = 0; x < dstWidth; x++, dst += pixelSize, cx++)
			{
				// coordinates of source point
				double ox = tx + angleCos * cx;
				double oy = ty - angleSin * cx;

				// top-left coordinate
				int ox1 = static_cast<int>(ox);
				int oy1 = static_cast<int>(oy);

				// validate source pixel's coordinates
				if (ox1 >= 0 && oy1 >= 0 && ox1 < srcWidth && oy1 < srcHeight)
				{
					// bottom-right coordinate
					int ox2 = ox1 == xmax ? ox1 : ox1 + 1;
					int oy2 = oy1 == ymax ? oy1 : oy1 + 1;

					double dx1 = std::max(0.0, ox - ox1);
					double dx2 = 1.0 - dx1;

					double dy1 = std::max(0.0, oy - oy1);
					double dy2 = 1.0 - dy1;

					// get four points
					unsigned char* p1 = src + oy1 * srcStride;
					unsigned char* p2 = p1;
					p1 += ox1 * pixelSize;
					p2 += ox2 * pixelSize;

					unsigned char* p3 = src + oy2 * srcStride;
					unsigned char* p4 = p3;
					p3 += ox1 * pixelSize;
					p4 += ox2 * pixelSize;

					// interpolate using 4 points
					for (int z = 0; z < pixelSize; z++)
					{
						dst[z] = static_cast<unsigned char>(dy2 * (dx2 * p1[z] + dx1 * p2[z]) +
							dy1 * (dx2 * p3[z] + dx1 * p4[z]));
					}
				}
			}
			dst += dstOffset;
		}
	}
}
