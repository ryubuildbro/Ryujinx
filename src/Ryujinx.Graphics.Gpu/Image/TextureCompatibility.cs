using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Texture;
using System;

namespace Ryujinx.Graphics.Gpu.Image
{
    /// <summary>
    /// Texture format compatibility checks.
    /// </summary>
    static class TextureCompatibility
    {
        private enum FormatClass
        {
            Unclassified,
            Bc1Rgba,
            Bc2,
            Bc3,
            Bc4,
            Bc5,
            Bc6,
            Bc7,
            Etc2Rgb,
            Etc2Rgba,
            Astc4x4,
            Astc5x4,
            Astc5x5,
            Astc6x5,
            Astc6x6,
            Astc8x5,
            Astc8x6,
            Astc8x8,
            Astc10x5,
            Astc10x6,
            Astc10x8,
            Astc10x10,
            Astc12x10,
            Astc12x12
        }

        /// <summary>
        /// Checks if a format is host incompatible.
        /// </summary>
        /// <remarks>
        /// Host incompatible formats can't be used directly, the texture data needs to be converted
        /// to a compatible format first.
        /// </remarks>
        /// <param name="info">Texture information</param>
        /// <param name="caps">Host GPU capabilities</param>
        /// <returns>True if the format is incompatible, false otherwise</returns>
        public static bool IsFormatHostIncompatible(TextureInfo info, Capabilities caps)
        {
            Format originalFormat = info.FormatInfo.Format;
            return ToHostCompatibleFormat(info, caps).Format != originalFormat;
        }

        /// <summary>
        /// Converts a incompatible format to a host compatible format, or return the format directly
        /// if it is already host compatible.
        /// </summary>
        /// <remarks>
        /// This can be used to convert a incompatible compressed format to the decompressor
        /// output format.
        /// </remarks>
        /// <param name="info">Texture information</param>
        /// <param name="caps">Host GPU capabilities</param>
        /// <returns>A host compatible format</returns>
        public static FormatInfo ToHostCompatibleFormat(TextureInfo info, Capabilities caps)
        {
            // The host API does not support those compressed formats.
            // We assume software decompression will be done for those textures,
            // and so we adjust the format here to match the decompressor output.

            if (!caps.SupportsAstcCompression)
            {
                if (info.FormatInfo.Format.IsAstcUnorm())
                {
                    return GraphicsConfig.EnableTextureRecompression
                        ? new FormatInfo(Format.Bc7Unorm, 4, 4, 16, 4)
                        : new FormatInfo(Format.R8G8B8A8Unorm, 1, 1, 4, 4);
                }
                else if (info.FormatInfo.Format.IsAstcSrgb())
                {
                    return GraphicsConfig.EnableTextureRecompression
                        ? new FormatInfo(Format.Bc7Srgb, 4, 4, 16, 4)
                        : new FormatInfo(Format.R8G8B8A8Srgb, 1, 1, 4, 4);
                }
            }

            if (!HostSupportsBcFormat(info.FormatInfo.Format, info.Target, caps))
            {
                switch (info.FormatInfo.Format)
                {
                    case Format.Bc1RgbaSrgb:
                    case Format.Bc2Srgb:
                    case Format.Bc3Srgb:
                    case Format.Bc7Srgb:
                        return new FormatInfo(Format.R8G8B8A8Srgb, 1, 1, 4, 4);
                    case Format.Bc1RgbaUnorm:
                    case Format.Bc2Unorm:
                    case Format.Bc3Unorm:
                    case Format.Bc7Unorm:
                        return new FormatInfo(Format.R8G8B8A8Unorm, 1, 1, 4, 4);
                    case Format.Bc4Unorm:
                        return new FormatInfo(Format.R8Unorm, 1, 1, 1, 1);
                    case Format.Bc4Snorm:
                        return new FormatInfo(Format.R8Snorm, 1, 1, 1, 1);
                    case Format.Bc5Unorm:
                        return new FormatInfo(Format.R8G8Unorm, 1, 1, 2, 2);
                    case Format.Bc5Snorm:
                        return new FormatInfo(Format.R8G8Snorm, 1, 1, 2, 2);
                    case Format.Bc6HSfloat:
                    case Format.Bc6HUfloat:
                        return new FormatInfo(Format.R16G16B16A16Float, 1, 1, 8, 4);
                }
            }

            if (!caps.SupportsEtc2Compression)
            {
                switch (info.FormatInfo.Format)
                {
                    case Format.Etc2RgbaSrgb:
                    case Format.Etc2RgbPtaSrgb:
                    case Format.Etc2RgbSrgb:
                        return new FormatInfo(Format.R8G8B8A8Srgb, 1, 1, 4, 4);
                    case Format.Etc2RgbaUnorm:
                    case Format.Etc2RgbPtaUnorm:
                    case Format.Etc2RgbUnorm:
                        return new FormatInfo(Format.R8G8B8A8Unorm, 1, 1, 4, 4);
                }
            }

            if (!caps.SupportsR4G4Format && info.FormatInfo.Format == Format.R4G4Unorm)
            {
                if (caps.SupportsR4G4B4A4Format)
                {
                    return new FormatInfo(Format.R4G4B4A4Unorm, 1, 1, 2, 4);
                }
                else
                {
                    return new FormatInfo(Format.R8G8B8A8Unorm, 1, 1, 4, 4);
                }
            }

            if (info.FormatInfo.Format == Format.R4G4B4A4Unorm)
            {
                if (!caps.SupportsR4G4B4A4Format)
                {
                    return new FormatInfo(Format.R8G8B8A8Unorm, 1, 1, 4, 4);
                }
            }
            else if (!caps.Supports5BitComponentFormat && info.FormatInfo.Format.Is16BitPacked())
            {
                return new FormatInfo(info.FormatInfo.Format.IsBgr() ? Format.B8G8R8A8Unorm : Format.R8G8B8A8Unorm, 1, 1, 4, 4);
            }

            return info.FormatInfo;
        }

        /// <summary>
        /// Checks if the host API supports a given texture compression format of the BC family.
        /// </summary>
        /// <param name="format">BC format to be checked</param>
        /// <param name="target">Target usage of the texture</param>
        /// <param name="caps">Host GPU Capabilities</param>
        /// <returns>True if the texture host supports the format with the given target usage, false otherwise</returns>
        public static bool HostSupportsBcFormat(Format format, Target target, Capabilities caps)
        {
            bool not3DOr3DCompressionSupported = target != Target.Texture3D || caps.Supports3DTextureCompression;

            switch (format)
            {
                case Format.Bc1RgbaSrgb:
                case Format.Bc1RgbaUnorm:
                case Format.Bc2Srgb:
                case Format.Bc2Unorm:
                case Format.Bc3Srgb:
                case Format.Bc3Unorm:
                    return caps.SupportsBc123Compression && not3DOr3DCompressionSupported;
                case Format.Bc4Unorm:
                case Format.Bc4Snorm:
                case Format.Bc5Unorm:
                case Format.Bc5Snorm:
                    return caps.SupportsBc45Compression && not3DOr3DCompressionSupported;
                case Format.Bc6HSfloat:
                case Format.Bc6HUfloat:
                case Format.Bc7Srgb:
                case Format.Bc7Unorm:
                    return caps.SupportsBc67Compression && not3DOr3DCompressionSupported;
            }

            return true;
        }

        /// <summary>
        /// Determines whether a texture can flush its data back to guest memory.
        /// </summary>
        /// <param name="info">Texture information</param>
        /// <param name="caps">Host GPU Capabilities</param>
        /// <returns>True if the texture can flush, false otherwise</returns>
        public static bool CanTextureFlush(TextureInfo info, Capabilities caps)
        {
            if (IsFormatHostIncompatible(info, caps))
            {
                return false; // Flushing this format is not supported, as it may have been converted to another host format.
            }

            if (info.Target == Target.Texture2DMultisample ||
                info.Target == Target.Texture2DMultisampleArray)
            {
                return false; // Flushing multisample textures is not supported, the host does not allow getting their data.
            }

            return true;
        }

        /// <summary>
        /// Checks if the texture format matches with the specified texture information.
        /// </summary>
        /// <param name="lhs">Texture information to compare</param>
        /// <param name="rhs">Texture information to compare with</param>
        /// <param name="forSampler">Indicates that the texture will be used for shader sampling</param>
        /// <param name="depthAlias">Indicates if aliasing between color and depth format should be allowed</param>
        /// <returns>A value indicating how well the formats match</returns>
        public static TextureMatchQuality FormatMatches(TextureInfo lhs, TextureInfo rhs, bool forSampler, bool depthAlias)
        {
            // D32F and R32F texture have the same representation internally,
            // however the R32F format is used to sample from depth textures.
            if (lhs.FormatInfo.Format == Format.D32Float && rhs.FormatInfo.Format == Format.R32Float && (forSampler || depthAlias))
            {
                return TextureMatchQuality.FormatAlias;
            }

            if (depthAlias)
            {
                // The 2D engine does not support depth-stencil formats, so it will instead
                // use equivalent color formats. We must also consider them as compatible.
                if (lhs.FormatInfo.Format == Format.S8Uint && rhs.FormatInfo.Format == Format.R8Unorm)
                {
                    return TextureMatchQuality.FormatAlias;
                }

                if (lhs.FormatInfo.Format == Format.D16Unorm && rhs.FormatInfo.Format == Format.R16Unorm)
                {
                    return TextureMatchQuality.FormatAlias;
                }

                if ((lhs.FormatInfo.Format == Format.D24UnormS8Uint ||
                     lhs.FormatInfo.Format == Format.S8UintD24Unorm) && rhs.FormatInfo.Format == Format.B8G8R8A8Unorm)
                {
                    return TextureMatchQuality.FormatAlias;
                }
            }

            return lhs.FormatInfo.Format == rhs.FormatInfo.Format ? TextureMatchQuality.Perfect : TextureMatchQuality.NoMatch;
        }

        /// <summary>
        /// Checks if the texture layout specified matches with this texture layout.
        /// The layout information is composed of the Stride for linear textures, or GOB block size
        /// for block linear textures.
        /// </summary>
        /// <param name="lhs">Texture information to compare</param>
        /// <param name="rhs">Texture information to compare with</param>
        /// <returns>True if the layout matches, false otherwise</returns>
        public static bool LayoutMatches(TextureInfo lhs, TextureInfo rhs)
        {
            if (lhs.IsLinear != rhs.IsLinear)
            {
                return false;
            }

            // For linear textures, gob block sizes are ignored.
            // For block linear textures, the stride is ignored.
            if (rhs.IsLinear)
            {
                return lhs.Stride == rhs.Stride;
            }
            else
            {
                return lhs.GobBlocksInY == rhs.GobBlocksInY &&
                       lhs.GobBlocksInZ == rhs.GobBlocksInZ;
            }
        }

        /// <summary>
        /// Obtain the minimum compatibility level of two provided view compatibility results.
        /// </summary>
        /// <param name="first">The first compatibility level</param>
        /// <param name="second">The second compatibility level</param>
        /// <returns>The minimum compatibility level of two provided view compatibility results</returns>
        public static TextureViewCompatibility PropagateViewCompatibility(TextureViewCompatibility first, TextureViewCompatibility second)
        {
            if (first == TextureViewCompatibility.Incompatible || second == TextureViewCompatibility.Incompatible)
            {
                return TextureViewCompatibility.Incompatible;
            }
            else if (first == TextureViewCompatibility.LayoutIncompatible || second == TextureViewCompatibility.LayoutIncompatible)
            {
                return TextureViewCompatibility.LayoutIncompatible;
            }
            else if (first == TextureViewCompatibility.CopyOnly || second == TextureViewCompatibility.CopyOnly)
            {
                return TextureViewCompatibility.CopyOnly;
            }
            else
            {
                return TextureViewCompatibility.Full;
            }
        }

        /// <summary>
        /// Checks if the sizes of two texture levels are copy compatible.
        /// </summary>
        /// <param name="lhs">Texture information of the texture view</param>
        /// <param name="rhs">Texture information of the texture view to match against</param>
        /// <param name="lhsLevel">Mipmap level of the texture view in relation to this texture</param>
        /// <param name="rhsLevel">Mipmap level of the texture view in relation to the second texture</param>
        /// <returns>True if both levels are view compatible</returns>
        public static bool CopySizeMatches(TextureInfo lhs, TextureInfo rhs, int lhsLevel, int rhsLevel)
        {
            Size size = GetAlignedSize(lhs, lhsLevel);

            Size otherSize = GetAlignedSize(rhs, rhsLevel);

            if (size.Width == otherSize.Width && size.Height == otherSize.Height)
            {
                return true;
            }
            else if (lhs.IsLinear && rhs.IsLinear)
            {
                // Copy between linear textures with matching stride.
                int stride = BitUtils.AlignUp(Math.Max(1, lhs.Stride >> lhsLevel), Constants.StrideAlignment);

                return stride == rhs.Stride;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the sizes of two given textures are view compatible.
        /// </summary>
        /// <param name="lhs">Texture information of the texture view</param>
        /// <param name="rhs">Texture information of the texture view to match against</param>
        /// <param name="exact">Indicates if the sizes must be exactly equal</param>
        /// <param name="level">Mipmap level of the texture view in relation to this texture</param>
        /// <returns>The view compatibility level of the view sizes</returns>
        public static TextureViewCompatibility ViewSizeMatches(TextureInfo lhs, TextureInfo rhs, bool exact, int level)
        {
            Size lhsAlignedSize = GetAlignedSize(lhs, level);
            Size rhsAlignedSize = GetAlignedSize(rhs);

            Size lhsSize = GetSizeInBlocks(lhs, level);
            Size rhsSize = GetSizeInBlocks(rhs);

            bool alignedWidthMatches = lhsAlignedSize.Width == rhsAlignedSize.Width;

            if (lhs.FormatInfo.BytesPerPixel != rhs.FormatInfo.BytesPerPixel && IsIncompatibleFormatAliasingAllowed(lhs.FormatInfo, rhs.FormatInfo))
            {
                alignedWidthMatches = lhsSize.Width * lhs.FormatInfo.BytesPerPixel == rhsSize.Width * rhs.FormatInfo.BytesPerPixel;
            }

            TextureViewCompatibility result = TextureViewCompatibility.Full;

            // For copies, we can copy a subset of the 3D texture slices,
            // so the depth may be different in this case.
            if (rhs.Target == Target.Texture3D && lhsSize.Depth != rhsSize.Depth)
            {
                result = TextureViewCompatibility.CopyOnly;
            }

            // Some APIs align the width for copy and render target textures,
            // so the width may not match in this case for different uses of the same texture.
            // To account for this, we compare the aligned width here.
            // We expect height to always match exactly, if the texture is the same.
            if (alignedWidthMatches && lhsSize.Height == rhsSize.Height)
            {
                return (exact && lhsSize.Width != rhsSize.Width) || lhsSize.Width < rhsSize.Width
                    ? TextureViewCompatibility.CopyOnly
                    : result;
            }
            else if (lhs.IsLinear && rhs.IsLinear && lhsSize.Height == rhsSize.Height)
            {
                // Copy between linear textures with matching stride.
                int stride = BitUtils.AlignUp(Math.Max(1, lhs.Stride >> level), Constants.StrideAlignment);

                return stride == rhs.Stride ? TextureViewCompatibility.CopyOnly : TextureViewCompatibility.LayoutIncompatible;
            }
            else
            {
                return TextureViewCompatibility.LayoutIncompatible;
            }
        }

        /// <summary>
        /// Checks if the potential child texture fits within the level and layer bounds of the parent.
        /// </summary>
        /// <param name="parent">Texture information for the parent</param>
        /// <param name="child">Texture information for the child</param>
        /// <param name="layer">Base layer of the child texture</param>
        /// <param name="level">Base level of the child texture</param>
        /// <returns>Full compatiblity if the child's layer and level count fit within the parent, incompatible otherwise</returns>
        public static TextureViewCompatibility ViewSubImagesInBounds(TextureInfo parent, TextureInfo child, int layer, int level)
        {
            if (level + child.Levels <= parent.Levels &&
                layer + child.GetSlices() <= parent.GetSlices())
            {
                return TextureViewCompatibility.Full;
            }
            else
            {
                return TextureViewCompatibility.LayoutIncompatible;
            }
        }

        /// <summary>
        /// Checks if the texture sizes of the supplied texture informations match.
        /// </summary>
        /// <param name="lhs">Texture information to compare</param>
        /// <param name="rhs">Texture information to compare with</param>
        /// <param name="exact">Indicates if the size must be exactly equal between the textures, or if <paramref name="rhs"/> is allowed to be larger</param>
        /// <returns>True if the sizes matches, false otherwise</returns>
        public static bool SizeMatches(TextureInfo lhs, TextureInfo rhs, bool exact)
        {
            if (lhs.GetLayers() != rhs.GetLayers())
            {
                return false;
            }

            Size lhsSize = GetSizeInBlocks(lhs);
            Size rhsSize = GetSizeInBlocks(rhs);

            if (exact || lhs.IsLinear || rhs.IsLinear)
            {
                return lhsSize.Width == rhsSize.Width &&
                       lhsSize.Height == rhsSize.Height &&
                       lhsSize.Depth == rhsSize.Depth;
            }
            else
            {
                Size lhsAlignedSize = GetAlignedSize(lhs);
                Size rhsAlignedSize = GetAlignedSize(rhs);

                return lhsAlignedSize.Width == rhsAlignedSize.Width &&
                       lhsSize.Width >= rhsSize.Width &&
                       lhsSize.Height == rhsSize.Height &&
                       lhsSize.Depth == rhsSize.Depth;
            }
        }

        /// <summary>
        /// Gets the aligned sizes for the given dimensions, using the specified texture information.
        /// The alignment depends on the texture layout and format bytes per pixel.
        /// </summary>
        /// <param name="info">Texture information to calculate the aligned size from</param>
        /// <param name="width">The width to be aligned</param>
        /// <param name="height">The height to be aligned</param>
        /// <param name="depth">The depth to be aligned</param>
        /// <returns>The aligned texture size</returns>
        private static Size GetAlignedSize(TextureInfo info, int width, int height, int depth)
        {
            if (info.IsLinear)
            {
                return SizeCalculator.GetLinearAlignedSize(
                    width,
                    height,
                    info.FormatInfo.BlockWidth,
                    info.FormatInfo.BlockHeight,
                    info.FormatInfo.BytesPerPixel);
            }
            else
            {
                return SizeCalculator.GetBlockLinearAlignedSize(
                    width,
                    height,
                    depth,
                    info.FormatInfo.BlockWidth,
                    info.FormatInfo.BlockHeight,
                    info.FormatInfo.BytesPerPixel,
                    info.GobBlocksInY,
                    info.GobBlocksInZ,
                    info.GobBlocksInTileX);
            }
        }

        /// <summary>
        /// Gets the aligned sizes of the specified texture information.
        /// The alignment depends on the texture layout and format bytes per pixel.
        /// </summary>
        /// <param name="info">Texture information to calculate the aligned size from</param>
        /// <param name="level">Mipmap level for texture views</param>
        /// <returns>The aligned texture size</returns>
        public static Size GetAlignedSize(TextureInfo info, int level = 0)
        {
            int width = Math.Max(1, info.Width >> level);
            int height = Math.Max(1, info.Height >> level);
            int depth = Math.Max(1, info.GetDepth() >> level);

            return GetAlignedSize(info, width, height, depth);
        }

        /// <summary>
        /// Gets the size in blocks for the given texture information.
        /// For non-compressed formats, that's the same as the regular size.
        /// </summary>
        /// <param name="info">Texture information to calculate the aligned size from</param>
        /// <param name="level">Mipmap level for texture views</param>
        /// <returns>The texture size in blocks</returns>
        public static Size GetSizeInBlocks(TextureInfo info, int level = 0)
        {
            int width = Math.Max(1, info.Width >> level);
            int height = Math.Max(1, info.Height >> level);
            int depth = Math.Max(1, info.GetDepth() >> level);

            return new Size(
                BitUtils.DivRoundUp(width, info.FormatInfo.BlockWidth),
                BitUtils.DivRoundUp(height, info.FormatInfo.BlockHeight),
                depth);
        }

        /// <summary>
        /// Check if it's possible to create a view with the layout of the second texture information from the first.
        /// The layout information is composed of the Stride for linear textures, or GOB block size
        /// for block linear textures.
        /// </summary>
        /// <param name="lhs">Texture information of the texture view</param>
        /// <param name="rhs">Texture information of the texture view to compare against</param>
        /// <param name="level">Start level of the texture view, in relation with the first texture</param>
        /// <returns>True if the layout is compatible, false otherwise</returns>
        public static bool ViewLayoutCompatible(TextureInfo lhs, TextureInfo rhs, int level)
        {
            if (lhs.IsLinear != rhs.IsLinear)
            {
                return false;
            }

            // For linear textures, gob block sizes are ignored.
            // For block linear textures, the stride is ignored.
            if (rhs.IsLinear)
            {
                int stride = Math.Max(1, lhs.Stride >> level);
                stride = BitUtils.AlignUp(stride, Constants.StrideAlignment);

                return stride == rhs.Stride;
            }
            else
            {
                int height = Math.Max(1, lhs.Height >> level);
                int depth = Math.Max(1, lhs.GetDepth() >> level);

                (int gobBlocksInY, int gobBlocksInZ) = SizeCalculator.GetMipGobBlockSizes(
                    height,
                    depth,
                    lhs.FormatInfo.BlockHeight,
                    lhs.GobBlocksInY,
                    lhs.GobBlocksInZ);

                return gobBlocksInY == rhs.GobBlocksInY &&
                       gobBlocksInZ == rhs.GobBlocksInZ;
            }
        }

        /// <summary>
        /// Check if it's possible to create a view with the layout of the second texture information from the first.
        /// The layout information is composed of the Stride for linear textures, or GOB block size
        /// for block linear textures.
        /// </summary>
        /// <param name="lhs">Texture information of the texture view</param>
        /// <param name="rhs">Texture information of the texture view to compare against</param>
        /// <param name="lhsLevel">Start level of the texture view, in relation with the first texture</param>
        /// <param name="rhsLevel">Start level of the texture view, in relation with the second texture</param>
        /// <returns>True if the layout is compatible, false otherwise</returns>
        public static bool ViewLayoutCompatible(TextureInfo lhs, TextureInfo rhs, int lhsLevel, int rhsLevel)
        {
            if (lhs.IsLinear != rhs.IsLinear)
            {
                return false;
            }

            // For linear textures, gob block sizes are ignored.
            // For block linear textures, the stride is ignored.
            if (rhs.IsLinear)
            {
                int lhsStride = Math.Max(1, lhs.Stride >> lhsLevel);
                lhsStride = BitUtils.AlignUp(lhsStride, Constants.StrideAlignment);

                int rhsStride = Math.Max(1, rhs.Stride >> rhsLevel);
                rhsStride = BitUtils.AlignUp(rhsStride, Constants.StrideAlignment);

                return lhsStride == rhsStride;
            }
            else
            {
                int lhsHeight = Math.Max(1, lhs.Height >> lhsLevel);
                int lhsDepth = Math.Max(1, lhs.GetDepth() >> lhsLevel);

                (int lhsGobBlocksInY, int lhsGobBlocksInZ) = SizeCalculator.GetMipGobBlockSizes(
                    lhsHeight,
                    lhsDepth,
                    lhs.FormatInfo.BlockHeight,
                    lhs.GobBlocksInY,
                    lhs.GobBlocksInZ);

                int rhsHeight = Math.Max(1, rhs.Height >> rhsLevel);
                int rhsDepth = Math.Max(1, rhs.GetDepth() >> rhsLevel);

                (int rhsGobBlocksInY, int rhsGobBlocksInZ) = SizeCalculator.GetMipGobBlockSizes(
                    rhsHeight,
                    rhsDepth,
                    rhs.FormatInfo.BlockHeight,
                    rhs.GobBlocksInY,
                    rhs.GobBlocksInZ);

                return lhsGobBlocksInY == rhsGobBlocksInY &&
                       lhsGobBlocksInZ == rhsGobBlocksInZ;
            }
        }

        /// <summary>
        /// Checks if the view format of the first texture format is compatible with the format of the second.
        /// In general, the formats are considered compatible if the bytes per pixel values are equal,
        /// but there are more complex rules for some formats, like compressed or depth-stencil formats.
        /// This follows the host API copy compatibility rules.
        /// </summary>
        /// <param name="lhs">Texture information of the texture view</param>
        /// <param name="rhs">Texture information of the texture view</param>
        /// <param name="caps">Host GPU capabilities</param>
        /// <returns>The view compatibility level of the texture formats</returns>
        public static TextureViewCompatibility ViewFormatCompatible(TextureInfo lhs, TextureInfo rhs, Capabilities caps)
        {
            FormatInfo lhsFormat = lhs.FormatInfo;
            FormatInfo rhsFormat = rhs.FormatInfo;

            if (lhsFormat.Format.IsDepthOrStencil() || rhsFormat.Format.IsDepthOrStencil())
            {
                return lhsFormat.Format == rhsFormat.Format ? TextureViewCompatibility.Full : TextureViewCompatibility.Incompatible;
            }

            if (IsFormatHostIncompatible(lhs, caps) || IsFormatHostIncompatible(rhs, caps))
            {
                return lhsFormat.Format == rhsFormat.Format ? TextureViewCompatibility.Full : TextureViewCompatibility.Incompatible;
            }

            if (lhsFormat.IsCompressed && rhsFormat.IsCompressed)
            {
                FormatClass lhsClass = GetFormatClass(lhsFormat.Format);
                FormatClass rhsClass = GetFormatClass(rhsFormat.Format);

                return lhsClass == rhsClass ? TextureViewCompatibility.Full : TextureViewCompatibility.Incompatible;
            }
            else if (lhsFormat.BytesPerPixel == rhsFormat.BytesPerPixel)
            {
                return lhs.FormatInfo.IsCompressed == rhs.FormatInfo.IsCompressed
                    ? TextureViewCompatibility.Full
                    : TextureViewCompatibility.CopyOnly;
            }
            else if (IsIncompatibleFormatAliasingAllowed(lhsFormat, rhsFormat))
            {
                return TextureViewCompatibility.CopyOnly;
            }

            return TextureViewCompatibility.Incompatible;
        }

        /// <summary>
        /// Checks if aliasing of two formats that would normally be considered incompatible be allowed,
        /// using copy dependencies.
        /// </summary>
        /// <param name="lhsFormat">Format information of the first texture</param
        /// <param name="rhsFormat">Format information of the second texture</param>
        /// <returns>True if aliasing should be allowed, false otherwise</returns>
        private static bool IsIncompatibleFormatAliasingAllowed(FormatInfo lhsFormat, FormatInfo rhsFormat)
        {
            // Some games will try to alias textures with incompatible foramts, with different BPP (bytes per pixel).
            // We allow that in some cases as long Width * BPP is equal on both textures.
            // This is very conservative right now as we want to avoid copies as much as possible,
            // so we only consider the formats we have seen being aliased.

            if (rhsFormat.BytesPerPixel < lhsFormat.BytesPerPixel)
            {
                (lhsFormat, rhsFormat) = (rhsFormat, lhsFormat);
            }

            return lhsFormat.Format == Format.R8Unorm && rhsFormat.Format == Format.R8G8B8A8Unorm;
        }

        /// <summary>
        /// Check if the target of the first texture view information is compatible with the target of the second texture view information.
        /// This follows the host API target compatibility rules.
        /// </summary>
        /// <param name="lhs">Texture information of the texture view</param
        /// <param name="rhs">Texture information of the texture view</param>
        /// <param name="caps">Host GPU capabilities</param>
        /// <returns>True if the targets are compatible, false otherwise</returns>
        public static TextureViewCompatibility ViewTargetCompatible(TextureInfo lhs, TextureInfo rhs, ref Capabilities caps)
        {
            bool result = false;
            switch (lhs.Target)
            {
                case Target.Texture1D:
                case Target.Texture1DArray:
                    result = rhs.Target == Target.Texture1D ||
                             rhs.Target == Target.Texture1DArray;
                    break;

                case Target.Texture2D:
                    result = rhs.Target == Target.Texture2D ||
                             rhs.Target == Target.Texture2DArray;
                    break;

                case Target.Texture2DArray:
                    result = rhs.Target == Target.Texture2D ||
                             rhs.Target == Target.Texture2DArray;

                    if (rhs.Target == Target.Cubemap || rhs.Target == Target.CubemapArray)
                    {
                        return caps.SupportsCubemapView ? TextureViewCompatibility.Full : TextureViewCompatibility.CopyOnly;
                    }
                    break;
                case Target.Cubemap:
                case Target.CubemapArray:
                    result = rhs.Target == Target.Cubemap ||
                             rhs.Target == Target.CubemapArray;

                    if (rhs.Target == Target.Texture2D || rhs.Target == Target.Texture2DArray)
                    {
                        return caps.SupportsCubemapView ? TextureViewCompatibility.Full : TextureViewCompatibility.CopyOnly;
                    }
                    break;
                case Target.Texture2DMultisample:
                case Target.Texture2DMultisampleArray:
                    if (rhs.Target == Target.Texture2D || rhs.Target == Target.Texture2DArray)
                    {
                        return TextureViewCompatibility.CopyOnly;
                    }

                    result = rhs.Target == Target.Texture2DMultisample ||
                             rhs.Target == Target.Texture2DMultisampleArray;
                    break;

                case Target.Texture3D:
                    if (rhs.Target == Target.Texture2D)
                    {
                        return TextureViewCompatibility.CopyOnly;
                    }

                    result = rhs.Target == Target.Texture3D;
                    break;
            }

            return result ? TextureViewCompatibility.Full : TextureViewCompatibility.Incompatible;
        }

        /// <summary>
        /// Checks if a swizzle component in two textures functionally match, taking into account if the components are defined.
        /// </summary>
        /// <param name="lhs">Texture information to compare</param>
        /// <param name="rhs">Texture information to compare with</param>
        /// <param name="swizzleLhs">Swizzle component for the first texture</param>
        /// <param name="swizzleRhs">Swizzle component for the second texture</param>
        /// <param name="component">Component index, starting at 0 for red</param>
        /// <returns>True if the swizzle components functionally match, false othersize</returns>
        private static bool SwizzleComponentMatches(TextureInfo lhs, TextureInfo rhs, SwizzleComponent swizzleLhs, SwizzleComponent swizzleRhs, int component)
        {
            int lhsComponents = lhs.FormatInfo.Components;
            int rhsComponents = rhs.FormatInfo.Components;

            if (lhsComponents == 4 && rhsComponents == 4)
            {
                return swizzleLhs == swizzleRhs;
            }

            // Swizzles after the number of components a format defines are "undefined".
            // We allow these to not be equal under certain circumstances.
            // This can only happen when there are less than 4 components in a format.
            // It tends to happen when float depth textures are sampled.

            bool lhsDefined = (swizzleLhs - SwizzleComponent.Red) < lhsComponents;
            bool rhsDefined = (swizzleRhs - SwizzleComponent.Red) < rhsComponents;

            if (lhsDefined == rhsDefined)
            {
                // If both are undefined, return true. Otherwise just check if they're equal.
                return lhsDefined ? swizzleLhs == swizzleRhs : true;
            }
            else
            {
                SwizzleComponent defined = lhsDefined ? swizzleLhs : swizzleRhs;
                SwizzleComponent undefined = lhsDefined ? swizzleRhs : swizzleLhs;

                // Undefined swizzle can be matched by a forced value (0, 1), exact equality, or expected value.
                // For example, R___ matches R001, RGBA but not RBGA.
                return defined == undefined || defined < SwizzleComponent.Red || defined == SwizzleComponent.Red + component;
            }
        }

        /// <summary>
        /// Checks if the texture shader sampling parameters of two texture informations match.
        /// </summary>
        /// <param name="lhs">Texture information to compare</param>
        /// <param name="rhs">Texture information to compare with</param>
        /// <returns>True if the texture shader sampling parameters matches, false otherwise</returns>
        public static bool SamplerParamsMatches(TextureInfo lhs, TextureInfo rhs)
        {
            return lhs.DepthStencilMode == rhs.DepthStencilMode &&
                   SwizzleComponentMatches(lhs, rhs, lhs.SwizzleR, rhs.SwizzleR, 0) &&
                   SwizzleComponentMatches(lhs, rhs, lhs.SwizzleG, rhs.SwizzleG, 1) &&
                   SwizzleComponentMatches(lhs, rhs, lhs.SwizzleB, rhs.SwizzleB, 2) &&
                   SwizzleComponentMatches(lhs, rhs, lhs.SwizzleA, rhs.SwizzleA, 3);
        }

        /// <summary>
        /// Check if the texture target and samples count (for multisampled textures) matches.
        /// </summary>
        /// <param name="first">Texture information to compare with</param>
        /// <param name="rhs">Texture information to compare with</param>
        /// <returns>True if the texture target and samples count matches, false otherwise</returns>
        public static bool TargetAndSamplesCompatible(TextureInfo lhs, TextureInfo rhs)
        {
            return lhs.Target == rhs.Target &&
                   lhs.SamplesInX == rhs.SamplesInX &&
                   lhs.SamplesInY == rhs.SamplesInY;
        }

        /// <summary>
        /// Gets the texture format class, for compressed textures, or Unclassified otherwise.
        /// </summary>
        /// <param name="format">The format</param>
        /// <returns>Format class</returns>
        private static FormatClass GetFormatClass(Format format)
        {
            switch (format)
            {
                case Format.Bc1RgbaSrgb:
                case Format.Bc1RgbaUnorm:
                    return FormatClass.Bc1Rgba;
                case Format.Bc2Srgb:
                case Format.Bc2Unorm:
                    return FormatClass.Bc2;
                case Format.Bc3Srgb:
                case Format.Bc3Unorm:
                    return FormatClass.Bc3;
                case Format.Bc4Snorm:
                case Format.Bc4Unorm:
                    return FormatClass.Bc4;
                case Format.Bc5Snorm:
                case Format.Bc5Unorm:
                    return FormatClass.Bc5;
                case Format.Bc6HSfloat:
                case Format.Bc6HUfloat:
                    return FormatClass.Bc6;
                case Format.Bc7Srgb:
                case Format.Bc7Unorm:
                    return FormatClass.Bc7;
                case Format.Etc2RgbSrgb:
                case Format.Etc2RgbUnorm:
                    return FormatClass.Etc2Rgb;
                case Format.Etc2RgbaSrgb:
                case Format.Etc2RgbaUnorm:
                    return FormatClass.Etc2Rgba;
                case Format.Astc4x4Srgb:
                case Format.Astc4x4Unorm:
                    return FormatClass.Astc4x4;
                case Format.Astc5x4Srgb:
                case Format.Astc5x4Unorm:
                    return FormatClass.Astc5x4;
                case Format.Astc5x5Srgb:
                case Format.Astc5x5Unorm:
                    return FormatClass.Astc5x5;
                case Format.Astc6x5Srgb:
                case Format.Astc6x5Unorm:
                    return FormatClass.Astc6x5;
                case Format.Astc6x6Srgb:
                case Format.Astc6x6Unorm:
                    return FormatClass.Astc6x6;
                case Format.Astc8x5Srgb:
                case Format.Astc8x5Unorm:
                    return FormatClass.Astc8x5;
                case Format.Astc8x6Srgb:
                case Format.Astc8x6Unorm:
                    return FormatClass.Astc8x6;
                case Format.Astc8x8Srgb:
                case Format.Astc8x8Unorm:
                    return FormatClass.Astc8x8;
                case Format.Astc10x5Srgb:
                case Format.Astc10x5Unorm:
                    return FormatClass.Astc10x5;
                case Format.Astc10x6Srgb:
                case Format.Astc10x6Unorm:
                    return FormatClass.Astc10x6;
                case Format.Astc10x8Srgb:
                case Format.Astc10x8Unorm:
                    return FormatClass.Astc10x8;
                case Format.Astc10x10Srgb:
                case Format.Astc10x10Unorm:
                    return FormatClass.Astc10x10;
                case Format.Astc12x10Srgb:
                case Format.Astc12x10Unorm:
                    return FormatClass.Astc12x10;
                case Format.Astc12x12Srgb:
                case Format.Astc12x12Unorm:
                    return FormatClass.Astc12x12;
            }

            return FormatClass.Unclassified;
        }
    }
}