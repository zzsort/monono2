// forked from https://github.com/nickbabcock/Pfim
using System;
using System.IO;

namespace Pfim
{
    /// <summary>
    /// Defines a common interface that all images are decoded into
    /// </summary>
    public interface IImage
    {
        /// <summary>The raw data</summary>
        byte[] Data { get; }

        /// <summary>Width of the image in pixels</summary>
        int Width { get; }

        /// <summary>Height of the image in pixels</summary>
        int Height { get; }

        /// <summary>The number of bytes that compose one line</summary>
        int Stride { get; }

        /// <summary>The number of bits that compose a pixel</summary>
        int BitsPerPixel { get; }

        /// <summary>The format of the raw data</summary>
        ImageFormat Format { get; }

        /// <summary>If the image format is compressed</summary>
        bool Compressed { get; }

        /// <summary>Decompress the image. Will have no effect if not compressed</summary>
        void Decompress();
    }

    public static class PfimUtil
    {
        public static void Fill(Stream stream, byte[] data, int bufSize)
        {
            InnerFill(stream, data, bufSize);
        }
        /// <summary>
        /// Fills the buffer all the way up with info from the stream
        /// </summary>
        /// <param name="str">Stream that will be used to fill the buffer</param>
        /// <param name="buf">Buffer that will house the information from the stream</param>
        /// <param name="bufSize">The chunk size of data that will be read from the stream</param>
        private static void InnerFill(Stream str, byte[] buf, int bufSize)
        {
            int bufPosition = 0;
            for (int i = buf.Length / bufSize; i > 0; i--)
                bufPosition += str.Read(buf, bufPosition, bufSize);
            str.Read(buf, bufPosition, buf.Length % bufSize);
        } 
        /// <summary>
        /// Takes all the bytes at and after an index and moves them to the front and fills the rest
        /// of the buffer with information from the stream.
        /// </summary>
        /// <remarks>
        /// This function is useful when the buffer doesn't have enough information to process a
        /// certain amount of information thus more information from the stream has to be read. This
        /// preserves the information that hasn't been read by putting it at the front.
        /// </remarks>
        /// <param name="str">Stream where more data will be read to fill in end of the buffer.</param>
        /// <param name="buf">The buffer that contains the data that will be translated.</param>
        /// <param name="bufIndex">
        /// Start of the translation. The value initially at this index will be the value at index 0
        /// in the buffer after translation.
        /// </param>
        /// <returns>
        /// The total number of bytes read into the buffer and translated. May be less than the
        /// buffer's length.
        /// </returns>
        public static int Translate(Stream str, byte[] buf, int bufIndex)
        {
            Buffer.BlockCopy(buf, bufIndex, buf, 0, buf.Length - bufIndex);
            int result = str.Read(buf, buf.Length - bufIndex, bufIndex);
            return result + buf.Length - bufIndex;
        }
        internal static MemoryStream CreateExposed(byte[] data)
        {
            return new MemoryStream(data, 0, data.Length);
        }
    }
    /// <summary>Describes how pixel data is arranged</summary>
    public enum ImageFormat
    {
        /// <summary>Red, green, and blue are the same values contained in a single byte</summary>
        Rgb8,

        /// <summary>Red, green, and blue are contained in a two bytes</summary>
        R5g5b5,

        R5g6b5,

        R5g5b5a1,

        Rgba16,

        /// <summary>Red, green, and blue channels are 8 bits apiece</summary>
        Rgb24,

        /// <summary>
        /// Red, green, blue, and alpha are 8 bits apiece
        /// </summary>
        Rgba32
    }

    public class Bc5Dds : CompressedDds
    {
        private readonly byte[] _firstGradient = new byte[8];
        private readonly byte[] _secondGradient = new byte[8];

        public Bc5Dds(DdsHeader header) : base(header)
        {
        }

        public override int BitsPerPixel => PixelDepth * 8;
        public override ImageFormat Format => ImageFormat.Rgb24;
        protected override byte PixelDepth => 3;
        protected override byte DivSize => 4;
        protected override byte CompressedBytesPerBlock => 16;

        protected override int Decode(byte[] stream, byte[] data, int streamIndex, uint dataIndex, uint width)
        {
            streamIndex = ExtractGradient(_firstGradient, stream, streamIndex);
            ulong firstCodes = stream[streamIndex++];
            firstCodes |= ((ulong)stream[streamIndex++] << 8);
            firstCodes |= ((ulong)stream[streamIndex++] << 16);
            firstCodes |= ((ulong)stream[streamIndex++] << 24);
            firstCodes |= ((ulong)stream[streamIndex++] << 32);
            firstCodes |= ((ulong)stream[streamIndex++] << 40);

            streamIndex = ExtractGradient(_secondGradient, stream, streamIndex);
            ulong secondCodes = stream[streamIndex++];
            secondCodes |= ((ulong)stream[streamIndex++] << 8);
            secondCodes |= ((ulong)stream[streamIndex++] << 16);
            secondCodes |= ((ulong)stream[streamIndex++] << 24);
            secondCodes |= ((ulong)stream[streamIndex++] << 32);
            secondCodes |= ((ulong)stream[streamIndex++] << 40);

            for (int alphaShift = 0; alphaShift < 48; alphaShift += 12)
            {
                for (int j = 0; j < 4; j++)
                {
                    // 3 bits determine alpha index to use
                    byte firstIndex = (byte)((firstCodes >> (alphaShift + 3 * j)) & 0x07);
                    byte secondIndex = (byte)((secondCodes >> (alphaShift + 3 * j)) & 0x07);
                    dataIndex++; // skip blue
                    data[dataIndex++] = _secondGradient[secondIndex];
                    data[dataIndex++] = _firstGradient[firstIndex];
                }
                dataIndex += PixelDepth * (width - DivSize);
            }

            return streamIndex;
        }

        internal static int ExtractGradient(byte[] gradient, byte[] stream, int bIndex)
        {
            byte endpoint0;
            byte endpoint1;
            gradient[0] = endpoint0 = stream[bIndex++];
            gradient[1] = endpoint1 = stream[bIndex++];

            if (endpoint0 > endpoint1)
            {
                for (int i = 1; i < 7; i++)
                    gradient[1 + i] = (byte)(((7 - i) * endpoint0 + i * endpoint1) / 7);
            }
            else
            {
                for (int i = 1; i < 5; ++i)
                    gradient[1 + i] = (byte)(((5 - i) * endpoint0 + i * endpoint1) / 5);
                gradient[6] = 0;
                gradient[7] = 255;
            }
            return bIndex;
        }
    }

    struct Color888
    {
        public byte r;
        public byte g;
        public byte b;
    }

    /// <summary>
    /// Class representing decoding compressed direct draw surfaces
    /// </summary>
    public abstract class CompressedDds : Dds
    {
        private bool _compressed;
        protected CompressedDds(DdsHeader header) : base(header)
        {
        }

        /// <summary>Uncompress a given block</summary>
        protected abstract int Decode(byte[] stream, byte[] data, int streamIndex, uint dataIndex, uint width);

        /// <summary>Number of bytes for a pixel in the decoded data</summary>
        protected abstract byte PixelDepth { get; }

        /// <summary>
        /// The length of a block is in pixels. This mainly affects compressed
        /// images as they are encoded in blocks that are divSize by divSize.
        /// Uncompressed DDS do not need this value.
        /// </summary>
        protected abstract byte DivSize { get; }

        /// <summary>
        /// Number of bytes needed to decode block of pixels that is divSize
        /// by divSize.  This takes into account how many bytes it takes to
        /// extract color and alpha information. Uncompressed DDS do not need
        /// this value.
        /// </summary>
        protected abstract byte CompressedBytesPerBlock { get; }
        public override bool Compressed => _compressed;

        private int BytesPerStride => BlocksPerStride * CompressedBytesPerBlock;
        private int BlocksPerStride => (int)(Header.Width / DivSize);

        /// <summary>Decode data into raw rgb format</summary>
        public byte[] DataDecode(Stream stream)
        {
            byte[] data = new byte[Header.Width * Header.Height * PixelDepth];
            uint dataIndex = 0;

            int bufferSize;
            uint pixelsLeft = Header.Width * Header.Height;
            uint divSize = DivSize;

            byte[] streamBuffer = new byte[0x10000];
            int bytesPerStride = BytesPerStride;
            int blocksPerStride = BlocksPerStride;

            do
            {
                int workingSize;
                bufferSize = workingSize = stream.Read(streamBuffer, 0, 0x10000);
                int bIndex = 0;
                while (workingSize > 0 && pixelsLeft > 0)
                {
                    // If there is not enough of the buffer to fill the next
                    // set of 16 square pixels Get the next buffer
                    if (workingSize < bytesPerStride)
                    {
                        bufferSize = workingSize = PfimUtil.Translate(stream, streamBuffer, bIndex);
                        bIndex = 0;
                    }

                    // Now that we have enough pixels to fill a stride (and
                    // this includes the normally 4 pixels below the stride)
                    for (uint i = 0; i < blocksPerStride; i++)
                    {
                        bIndex = Decode(streamBuffer, data, bIndex, dataIndex, Header.Width);

                        // Advance to the next block, which is (pixel depth *
                        // divSize) bytes away
                        dataIndex += divSize * PixelDepth;
                    }

                    // Each decoded block is divSize by divSize so pixels left
                    // is Width * multiplied by block height
                    pixelsLeft -= Header.Width * divSize;
                    workingSize -= bytesPerStride;

                    // Jump down to the block that is exactly (divSize - 1)
                    // below the current row we are on
                    dataIndex += (PixelDepth * (divSize - 1) * Header.Width);
                }
            } while (bufferSize != 0 && pixelsLeft != 0);

            return data;
        }

        private byte[] InMemoryDecode(byte[] memBuffer, int bIndex)
        {
            byte[] data = new byte[Header.Width * Header.Height * PixelDepth];
            uint dataIndex = 0;
            uint divSize = DivSize;
            int blocksPerStride = BlocksPerStride;
            uint pixelsLeft = Header.Width * Header.Height;

            // Same implementation as the stream based decoding, just a little bit
            // more straightforward.
            while (pixelsLeft > 0)
            {
                for (uint i = 0; i < blocksPerStride; i++)
                {
                    bIndex = Decode(memBuffer, data, bIndex, dataIndex, Header.Width);
                    dataIndex += divSize * PixelDepth;
                }

                pixelsLeft -= Header.Width * divSize;
                dataIndex += (PixelDepth * (divSize - 1) * Header.Width);
            }

            return data;
        }

        protected override void Decode(Stream stream, bool decompress)
        {
            if (decompress)
            {
                Data = DataDecode(stream);
            }
            else
            {
                int blocksPerStride = (int)(Header.Width / DivSize);
                long totalSize = blocksPerStride * CompressedBytesPerBlock * (Header.Height / DivSize);

                var width = (int)Header.Width;
                var height = (int)Header.Height;
                for (int i = 1; i < Header.MipMapCout; i++)
                {
                    width = (int)Math.Pow(2, Math.Floor(Math.Log(width - 1, 2)));
                    height = (int)Math.Pow(2, Math.Floor(Math.Log(height - 1, 2)));
                    var widthBlocks = Math.Max(DivSize, width) / DivSize;
                    var heightBlocks = Math.Max(DivSize, height) / DivSize;
                    totalSize += widthBlocks * heightBlocks * CompressedBytesPerBlock;
                }

                Data = new byte[totalSize];
                _compressed = true;
                PfimUtil.Fill(stream, Data, 0x10000);
            }
        }

        public override void Decompress()
        {
            if (!_compressed)
            {
                return;
            }

            Data = InMemoryDecode(Data, 0);
            _compressed = false;
        }
    }

    public enum D3D10ResourceDimension : uint
    {
        D3D10_RESOURCE_DIMENSION_UNKNOWN = 0,
        D3D10_RESOURCE_DIMENSION_BUFFER = 1,
        D3D10_RESOURCE_DIMENSION_TEXTURE1D = 2,
        D3D10_RESOURCE_DIMENSION_TEXTURE2D = 3,
        D3D10_RESOURCE_DIMENSION_TEXTURE3D = 4
    }

    /// <summary>
    /// Class that represents direct draw surfaces
    /// </summary>
    public abstract class Dds : IImage
    {
        /// <summary>
        /// Instantiates a direct draw surface image from a header, the data,
        /// and additional info.
        /// </summary>
        protected Dds(DdsHeader header)
        {
            Header = header;
        }

        public DdsHeader Header { get; }
        public abstract int BitsPerPixel { get; }
        public int BytesPerPixel => BitsPerPixel / 8;
        public int Stride => (int)(4 * ((Header.Width * BytesPerPixel + 3) / 4));
        public virtual byte[] Data { get; protected set; }
        public int Width => (int)Header.Width;
        public int Height => (int)Header.Height;
        public abstract ImageFormat Format { get; }
        public abstract bool Compressed { get; }
        public abstract void Decompress();
        public DdsHeaderDxt10 Header10 { get; private set; }

        public static Dds Create(byte[] data, bool decompress)
        {
            return Create(PfimUtil.CreateExposed(data), decompress);
        }

        /// <summary>Create a direct draw image from a stream</summary>
        public static Dds Create(Stream stream, bool decompress)
        {
            DdsHeader header = new DdsHeader(stream);
            Dds dds;
            switch (header.PixelFormat.FourCC)
            {
                case CompressionAlgorithm.D3DFMT_DXT1:
                    dds = new Dxt1Dds(header);
                    break;

                case CompressionAlgorithm.D3DFMT_DXT2:
                case CompressionAlgorithm.D3DFMT_DXT4:
                    throw new ArgumentException("Cannot support DXT2 or DXT4");
                case CompressionAlgorithm.D3DFMT_DXT3:
                    dds = new Dxt3Dds(header);
                    break;

                case CompressionAlgorithm.D3DFMT_DXT5:
                    dds = new Dxt5Dds(header);
                    break;

                case CompressionAlgorithm.None:
                    dds = new UncompressedDds(header);
                    break;

                case CompressionAlgorithm.DX10:
                    var header10 = new DdsHeaderDxt10(stream);
                    dds = header10.NewDecoder(header);
                    dds.Header10 = header10;
                    break;

                case CompressionAlgorithm.ATI2:
                    dds = new Bc5Dds(header);
                    break;

                default:
                    throw new ArgumentException($"FourCC: {header.PixelFormat.FourCC} not supported.");
            }

            dds.Decode(stream, decompress);
            return dds;
        }

        protected abstract void Decode(Stream stream, bool decompress);
    }

    /// <summary>
    /// Denotes the compression algorithm used in the image. Either the image
    /// is uncompressed or uses some sort of block compression. The
    /// compression used is encoded in the header of image as textual
    /// representation of itself. So a DXT1 image is encoded as "1TXD" so the
    /// enum represents these values directly
    /// </summary>
    public enum CompressionAlgorithm : uint
    {
        /// <summary>
        /// No compression was used in the image.
        /// </summary>
        None = 0,

        /// <summary>
        /// <see cref="Dxt1Dds"/>. Also known as BC1
        /// </summary>
        D3DFMT_DXT1 = 827611204,

        /// <summary>
        /// Not supported. Also known as BC2
        /// </summary>
        D3DFMT_DXT2 = 844388420,

        /// <summary>
        /// <see cref="Dxt3Dds"/>. Also known as BC3
        /// </summary>
        D3DFMT_DXT3 = 861165636,

        /// <summary>
        /// Not supported. Also known as BC4
        /// </summary>
        D3DFMT_DXT4 = 877942852,

        /// <summary>
        /// <see cref="Dxt5Dds"/>. Also known as BC5
        /// </summary>
        D3DFMT_DXT5 = 894720068,

        DX10 = 808540228,

        ATI2 = 843666497
    }

    /// <summary>Flags to indicate which members contain valid data.</summary>
    [Flags]
    public enum DdsFlags : uint
    {
        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Caps = 0x1,

        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Height = 0x2,

        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        Width = 0x4,

        /// <summary>
        /// Required when pitch is provided for an uncompressed texture.
        /// </summary>
        Pitch = 0x8,

        /// <summary>
        /// Required in every .dds file.
        /// </summary>
        PixelFormat = 0x1000,

        /// <summary>
        /// Required in a mipmapped texture.
        /// </summary>
        MipMapCount = 0x20000,

        /// <summary>
        /// Required when pitch is provided for a compressed texture.
        /// </summary>
        LinearSize = 0x80000,

        /// <summary>
        /// Required in a depth texture.
        /// </summary>
        Depth = 0x800000
    }

    /// <summary>Values which indicate what type of data is in the surface.</summary>
    [Flags]
    public enum DdsPixelFormatFlags : uint
    {
        /// <summary>
        ///     Texture contains alpha data; dwRGBAlphaBitMask contains valid data.
        /// </summary>
        AlphaPixels = 0x1,

        /// <summary>
        ///     Used in some older DDS files for alpha channel only uncompressed data (dwRGBBitCount contains the alpha channel
        ///     bitcount; dwABitMask contains valid data)
        /// </summary>
        Alpha = 0x2,

        /// <summary>
        ///     Texture contains compressed RGB data; dwFourCC contains valid data.
        /// </summary>
        Fourcc = 0x4,

        /// <summary>
        ///     Texture contains uncompressed RGB data; dwRGBBitCount and the RGB masks (dwRBitMask, dwGBitMask, dwBBitMask)
        ///     contain valid data.
        /// </summary>
        Rgb = 0x40,

        /// <summary>
        ///     Used in some older DDS files for YUV uncompressed data (dwRGBBitCount contains the YUV bit count; dwRBitMask
        ///     contains the Y mask, dwGBitMask contains the U mask, dwBBitMask contains the V mask)
        /// </summary>
        Yuv = 0x200,

        /// <summary>
        ///     Used in some older DDS files for single channel color uncompressed data (dwRGBBitCount contains the luminance
        ///     channel bit count; dwRBitMask contains the channel mask). Can be combined with DDPF_ALPHAPIXELS for a two channel
        ///     DDS file.
        /// </summary>
        Luminance = 0x20000
    }

    /// <summary>
    /// Surface pixel format.
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/bb943984(v=vs.85).aspx
    /// </summary>
    public struct DdsPixelFormat
    {
        /// <summary>
        /// Structure size; set to 32 (bytes).
        /// </summary>
        public uint Size;

        /// <summary>
        /// Values which indicate what type of data is in the surface. 
        /// </summary>
        public DdsPixelFormatFlags PixelFormatFlags;

        /// <summary>
        /// Four-character codes for specifying compressed or custom formats.
        /// Possible values include: DXT1, DXT2, DXT3, DXT4, or DXT5.  A
        /// FourCC of DX10 indicates the prescense of the DDS_HEADER_DXT10
        /// extended header,  and the dxgiFormat member of that structure
        /// indicates the true format. When using a four-character code,
        /// dwFlags must include DDPF_FOURCC.
        /// </summary>
        public CompressionAlgorithm FourCC;

        /// <summary>
        /// Number of bits in an RGB (possibly including alpha) format.
        /// Valid when dwFlags includes DDPF_RGB, DDPF_LUMINANCE, or DDPF_YUV.
        /// </summary>
        public uint RGBBitCount;

        /// <summary>
        /// Red (or lumiannce or Y) mask for reading color data.
        /// For instance, given the A8R8G8B8 format, the red mask would be 0x00ff0000.
        /// </summary>
        public uint RBitMask;

        /// <summary>
        /// Green (or U) mask for reading color data.
        /// For instance, given the A8R8G8B8 format, the green mask would be 0x0000ff00.
        /// </summary>
        public uint GBitMask;

        /// <summary>
        /// Blue (or V) mask for reading color data.
        /// For instance, given the A8R8G8B8 format, the blue mask would be 0x000000ff.
        /// </summary>
        public uint BBitMask;

        /// <summary>
        /// Alpha mask for reading alpha data. 
        /// dwFlags must include DDPF_ALPHAPIXELS or DDPF_ALPHA. 
        /// For instance, given the A8R8G8B8 format, the alpha mask would be 0xff000000.
        /// </summary>
        public uint ABitMask;
    }

    /// <summary>
    /// The header that accompanies all direct draw images
    /// https://msdn.microsoft.com/en-us/library/windows/desktop/bb943982(v=vs.85).aspx
    /// </summary>
    public class DdsHeader
    {
        /// <summary>
        /// Size of a Direct Draw Header in number of bytes.
        /// This does not include the magic number
        /// </summary>
        private const int SIZE = 124;

        /// <summary>
        /// The magic number is the 4 bytes that starts off every Direct Draw Surface file.
        /// </summary>
        private const uint DDS_MAGIC = 542327876;

        DdsPixelFormat pixelFormat;

        /// <summary>Create header from stream</summary>
        public DdsHeader(Stream stream)
        {
            headerInit(stream);
        }

        private void headerInit(Stream stream)
        {
            using (var br = new BinaryReader(stream, System.Text.Encoding.Default, true))
            {
                if (br.ReadInt32() != DDS_MAGIC)
                    throw new Exception("Not a valid DDS");
                if ((Size = br.ReadUInt32()) != SIZE)
                    throw new Exception("Not a valid header size");
                Flags = (DdsFlags)br.ReadUInt32();
                Height = br.ReadUInt32();
                Width = br.ReadUInt32();
                PitchOrLinearSize = br.ReadUInt32();
                Depth = br.ReadUInt32();
                MipMapCout = br.ReadUInt32();

                Reserved1 = new uint[11];
                for (int i = 0; i < 11; i++)
                    Reserved1[i] = br.ReadUInt32();
                    
                pixelFormat.Size = br.ReadUInt32();
                if (pixelFormat.Size != 32)
                {
                    throw new Exception($"Expected pixel size to be 32, not: ${pixelFormat.Size}");
                }

                pixelFormat.PixelFormatFlags = (DdsPixelFormatFlags)br.ReadUInt32();
                pixelFormat.FourCC = (CompressionAlgorithm)br.ReadUInt32();
                pixelFormat.RGBBitCount = br.ReadUInt32();
                pixelFormat.RBitMask = br.ReadUInt32();
                pixelFormat.GBitMask = br.ReadUInt32();
                pixelFormat.BBitMask = br.ReadUInt32();
                pixelFormat.ABitMask = br.ReadUInt32();

                Caps = br.ReadUInt32();
                Caps2 = br.ReadUInt32();
                Caps3 = br.ReadUInt32();
                Caps4 = br.ReadUInt32();
                Reserved2 = br.ReadUInt32();
            }
        }

        /// <summary>
        /// Size of structure. This member must be set to 124.
        /// </summary>
        public uint Size { get; private set; }

        /// <summary>
        /// Flags to indicate which members contain valid data. 
        /// </summary>
        DdsFlags Flags { get; set; }

        /// <summary>
        /// Surface height in pixels
        /// </summary>
        public uint Height { get; private set; }

        /// <summary>
        /// Surface width in pixels
        /// </summary>
        public uint Width { get; private set; }

        /// <summary>
        /// The pitch or number of bytes per scan line in an uncompressed texture.
        /// The total number of bytes in the top level texture for a compressed texture.
        /// </summary>
        public uint PitchOrLinearSize { get; private set; }

        /// <summary>
        /// Depth of a volume texture (in pixels), otherwise unused. 
        /// </summary>
        public uint Depth { get; private set; }

        /// <summary>
        /// Number of mipmap levels, otherwise unused.
        /// </summary>
        public uint MipMapCout { get; private set; }

        /// <summary>
        /// Unused
        /// </summary>
        public uint[] Reserved1 { get; private set; }

        /// <summary>
        /// The pixel format 
        /// </summary>
        public DdsPixelFormat PixelFormat
        {
            get => pixelFormat;
            set => pixelFormat = value;
        }

        /// <summary>
        /// Specifies the complexity of the surfaces stored.
        /// </summary>
        public uint Caps { get; private set; }

        /// <summary>
        /// Additional detail about the surfaces stored.
        /// </summary>
        public uint Caps2 { get; private set; }

        /// <summary>
        /// Unused
        /// </summary>
        public uint Caps3 { get; private set; }

        /// <summary>
        /// Unused
        /// </summary>
        public uint Caps4 { get; private set; }

        /// <summary>
        /// Unused
        /// </summary>
        public uint Reserved2 { get; private set; }
    }

    public class DdsHeaderDxt10
    {
        public DxgiFormat DxgiFormat { get; }
        public D3D10ResourceDimension ResourceDimension { get; }
        public uint MiscFlag { get; }
        public uint ArraySize { get; }
        public uint MiscFlags2 { get; }

        public DdsHeaderDxt10(Stream stream)
        {
            using (var br = new BinaryReader(stream, System.Text.Encoding.Default, true))
            {
                DxgiFormat = (DxgiFormat)br.ReadUInt32();
                ResourceDimension = (D3D10ResourceDimension)br.ReadUInt32();
                MiscFlag = br.ReadUInt32();
                ArraySize = br.ReadUInt32();
                MiscFlags2 = br.ReadUInt32();
            }
        }

        internal Dds NewDecoder(DdsHeader header)
        {
            switch (DxgiFormat)
            {
                case DxgiFormat.BC1_TYPELESS:
                case DxgiFormat.BC1_UNORM_SRGB:
                case DxgiFormat.BC1_UNORM:
                    return new Dxt1Dds(header);

                case DxgiFormat.BC3_TYPELESS:
                case DxgiFormat.BC3_UNORM:
                case DxgiFormat.BC3_UNORM_SRGB:
                    return new Dxt3Dds(header);

                case DxgiFormat.BC5_SNORM:
                case DxgiFormat.BC5_TYPELESS:
                case DxgiFormat.BC5_UNORM:
                    return new Dxt5Dds(header);

                case DxgiFormat.R8G8B8A8_TYPELESS:
                case DxgiFormat.R8G8B8A8_UNORM:
                case DxgiFormat.R8G8B8A8_UNORM_SRGB:
                case DxgiFormat.R8G8B8A8_UINT:
                case DxgiFormat.R8G8B8A8_SNORM:
                case DxgiFormat.R8G8B8A8_SINT:
                    return new UncompressedDds(header, 32, true);
                case DxgiFormat.B8G8R8A8_TYPELESS:
                case DxgiFormat.B8G8R8A8_UNORM:
                case DxgiFormat.B8G8R8A8_UNORM_SRGB:
                    return new UncompressedDds(header, 32, false);

                case DxgiFormat.UNKNOWN:
                case DxgiFormat.R32G32B32A32_TYPELESS:
                case DxgiFormat.R32G32B32A32_FLOAT:
                case DxgiFormat.R32G32B32A32_UINT:
                case DxgiFormat.R32G32B32A32_SINT:
                case DxgiFormat.R32G32B32_TYPELESS:
                case DxgiFormat.R32G32B32_FLOAT:
                case DxgiFormat.R32G32B32_UINT:
                case DxgiFormat.R32G32B32_SINT:
                case DxgiFormat.R16G16B16A16_TYPELESS:
                case DxgiFormat.R16G16B16A16_FLOAT:
                case DxgiFormat.R16G16B16A16_UNORM:
                case DxgiFormat.R16G16B16A16_UINT:
                case DxgiFormat.R16G16B16A16_SNORM:
                case DxgiFormat.R16G16B16A16_SINT:
                case DxgiFormat.R32G32_TYPELESS:
                case DxgiFormat.R32G32_FLOAT:
                case DxgiFormat.R32G32_UINT:
                case DxgiFormat.R32G32_SINT:
                case DxgiFormat.R32G8X24_TYPELESS:
                case DxgiFormat.D32_FLOAT_S8X24_UINT:
                case DxgiFormat.R32_FLOAT_X8X24_TYPELESS:
                case DxgiFormat.X32_TYPELESS_G8X24_UINT:
                case DxgiFormat.R10G10B10A2_TYPELESS:
                case DxgiFormat.R10G10B10A2_UNORM:
                case DxgiFormat.R10G10B10A2_UINT:
                case DxgiFormat.R11G11B10_FLOAT:
                case DxgiFormat.R16G16_TYPELESS:
                case DxgiFormat.R16G16_FLOAT:
                case DxgiFormat.R16G16_UNORM:
                case DxgiFormat.R16G16_UINT:
                case DxgiFormat.R16G16_SNORM:
                case DxgiFormat.R16G16_SINT:
                case DxgiFormat.R32_TYPELESS:
                case DxgiFormat.D32_FLOAT:
                case DxgiFormat.R32_FLOAT:
                case DxgiFormat.R32_UINT:
                case DxgiFormat.R32_SINT:
                case DxgiFormat.R24G8_TYPELESS:
                case DxgiFormat.D24_UNORM_S8_UINT:
                case DxgiFormat.R24_UNORM_X8_TYPELESS:
                case DxgiFormat.X24_TYPELESS_G8_UINT:
                case DxgiFormat.R8G8_TYPELESS:
                case DxgiFormat.R8G8_UNORM:
                case DxgiFormat.R8G8_UINT:
                case DxgiFormat.R8G8_SNORM:
                case DxgiFormat.R8G8_SINT:
                case DxgiFormat.R16_TYPELESS:
                case DxgiFormat.R16_FLOAT:
                case DxgiFormat.D16_UNORM:
                case DxgiFormat.R16_UNORM:
                case DxgiFormat.R16_UINT:
                case DxgiFormat.R16_SNORM:
                case DxgiFormat.R16_SINT:
                case DxgiFormat.R8_TYPELESS:
                case DxgiFormat.R8_UNORM:
                case DxgiFormat.R8_UINT:
                case DxgiFormat.R8_SNORM:
                case DxgiFormat.R8_SINT:
                case DxgiFormat.A8_UNORM:
                case DxgiFormat.R1_UNORM:
                case DxgiFormat.R9G9B9E5_SHAREDEXP:
                case DxgiFormat.R8G8_B8G8_UNORM:
                case DxgiFormat.G8R8_G8B8_UNORM:
                case DxgiFormat.BC2_TYPELESS:
                case DxgiFormat.BC2_UNORM:
                case DxgiFormat.BC2_UNORM_SRGB:
                case DxgiFormat.BC4_TYPELESS:
                case DxgiFormat.B8G8R8X8_UNORM:
                case DxgiFormat.R10G10B10_XR_BIAS_A2_UNORM:
                case DxgiFormat.B8G8R8X8_TYPELESS:
                case DxgiFormat.B8G8R8X8_UNORM_SRGB:
                case DxgiFormat.BC6H_TYPELESS:
                case DxgiFormat.BC6H_UF16:
                case DxgiFormat.BC6H_SF16:
                case DxgiFormat.BC7_TYPELESS:
                case DxgiFormat.BC7_UNORM:
                case DxgiFormat.NV12:
                case DxgiFormat.P010:
                case DxgiFormat.P016:
                case DxgiFormat.OPAQUE_420:
                case DxgiFormat.YUY2:
                case DxgiFormat.Y210:
                case DxgiFormat.Y216:
                case DxgiFormat.NV11:
                case DxgiFormat.AI44:
                case DxgiFormat.IA44:
                case DxgiFormat.P8:
                case DxgiFormat.A8P8:
                case DxgiFormat.B4G4R4A4_UNORM:
                case DxgiFormat.P208:
                case DxgiFormat.V208:
                case DxgiFormat.V408:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    /// <summary>Contains additional info about the image</summary>
    public struct DdsLoadInfo
    {
        public ImageFormat Format { get; }
        public bool Compressed { get; }
        public bool Swap { get; }
        public bool Palette { get; }

        /// <summary>
        /// The length of a block is in pixels.
        /// This mainly affects compressed images as they are
        /// encoded in blocks that are divSize by divSize.
        /// Uncompressed DDS do not need this value.
        /// </summary>
        public uint DivSize { get; }

        /// <summary>
        /// Number of bytes needed to decode block of pixels
        /// that is divSize by divSize.  This takes into account
        /// how many bytes it takes to extract color and alpha information.
        /// Uncompressed DDS do not need this value.
        /// </summary>
        public uint BlockBytes { get; }

        public int Depth { get; }

        /// <summary>Initialize the load info structure</summary>
        public DdsLoadInfo(bool isCompresed, bool isSwap, bool isPalette, uint aDivSize, uint aBlockBytes, int aDepth, ImageFormat format)
        {
            Format = format;
            Compressed = isCompresed;
            Swap = isSwap;
            Palette = isPalette;
            DivSize = aDivSize;
            BlockBytes = aBlockBytes;
            Depth = aDepth;
        }
    }

    public enum DxgiFormat : uint
    {
        UNKNOWN = 0,
        R32G32B32A32_TYPELESS = 1,
        R32G32B32A32_FLOAT = 2,
        R32G32B32A32_UINT = 3,
        R32G32B32A32_SINT = 4,
        R32G32B32_TYPELESS = 5,
        R32G32B32_FLOAT = 6,
        R32G32B32_UINT = 7,
        R32G32B32_SINT = 8,
        R16G16B16A16_TYPELESS = 9,
        R16G16B16A16_FLOAT = 10,
        R16G16B16A16_UNORM = 11,
        R16G16B16A16_UINT = 12,
        R16G16B16A16_SNORM = 13,
        R16G16B16A16_SINT = 14,
        R32G32_TYPELESS = 15,
        R32G32_FLOAT = 16,
        R32G32_UINT = 17,
        R32G32_SINT = 18,
        R32G8X24_TYPELESS = 19,
        D32_FLOAT_S8X24_UINT = 20,
        R32_FLOAT_X8X24_TYPELESS = 21,
        X32_TYPELESS_G8X24_UINT = 22,
        R10G10B10A2_TYPELESS = 23,
        R10G10B10A2_UNORM = 24,
        R10G10B10A2_UINT = 25,
        R11G11B10_FLOAT = 26,
        R8G8B8A8_TYPELESS = 27,
        R8G8B8A8_UNORM = 28,
        R8G8B8A8_UNORM_SRGB = 29,
        R8G8B8A8_UINT = 30,
        R8G8B8A8_SNORM = 31,
        R8G8B8A8_SINT = 32,
        R16G16_TYPELESS = 33,
        R16G16_FLOAT = 34,
        R16G16_UNORM = 35,
        R16G16_UINT = 36,
        R16G16_SNORM = 37,
        R16G16_SINT = 38,
        R32_TYPELESS = 39,
        D32_FLOAT = 40,
        R32_FLOAT = 41,
        R32_UINT = 42,
        R32_SINT = 43,
        R24G8_TYPELESS = 44,
        D24_UNORM_S8_UINT = 45,
        R24_UNORM_X8_TYPELESS = 46,
        X24_TYPELESS_G8_UINT = 47,
        R8G8_TYPELESS = 48,
        R8G8_UNORM = 49,
        R8G8_UINT = 50,
        R8G8_SNORM = 51,
        R8G8_SINT = 52,
        R16_TYPELESS = 53,
        R16_FLOAT = 54,
        D16_UNORM = 55,
        R16_UNORM = 56,
        R16_UINT = 57,
        R16_SNORM = 58,
        R16_SINT = 59,
        R8_TYPELESS = 60,
        R8_UNORM = 61,
        R8_UINT = 62,
        R8_SNORM = 63,
        R8_SINT = 64,
        A8_UNORM = 65,
        R1_UNORM = 66,
        R9G9B9E5_SHAREDEXP = 67,
        R8G8_B8G8_UNORM = 68,
        G8R8_G8B8_UNORM = 69,
        BC1_TYPELESS = 70,
        BC1_UNORM = 71,
        BC1_UNORM_SRGB = 72,
        BC2_TYPELESS = 73,
        BC2_UNORM = 74,
        BC2_UNORM_SRGB = 75,
        BC3_TYPELESS = 76,
        BC3_UNORM = 77,
        BC3_UNORM_SRGB = 78,
        BC4_TYPELESS = 79,
        BC4_UNORM = 80,
        BC4_SNORM = 81,
        BC5_TYPELESS = 82,
        BC5_UNORM = 83,
        BC5_SNORM = 84,
        B5G6R5_UNORM = 85,
        B5G5R5A1_UNORM = 86,
        B8G8R8A8_UNORM = 87,
        B8G8R8X8_UNORM = 88,
        R10G10B10_XR_BIAS_A2_UNORM = 89,
        B8G8R8A8_TYPELESS = 90,
        B8G8R8A8_UNORM_SRGB = 91,
        B8G8R8X8_TYPELESS = 92,
        B8G8R8X8_UNORM_SRGB = 93,
        BC6H_TYPELESS = 94,
        BC6H_UF16 = 95,
        BC6H_SF16 = 96,
        BC7_TYPELESS = 97,
        BC7_UNORM = 98,
        BC7_UNORM_SRGB = 99,
        AYUV = 100,
        Y410 = 101,
        Y416 = 102,
        NV12 = 103,
        P010 = 104,
        P016 = 105,
        OPAQUE_420 = 106,
        YUY2 = 107,
        Y210 = 108,
        Y216 = 109,
        NV11 = 110,
        AI44 = 111,
        IA44 = 112,
        P8 = 113,
        A8P8 = 114,
        B4G4R4A4_UNORM = 115,
        P208 = 130,
        V208 = 131,
        V408 = 132,
    }

    public static class FormatExtensions
    {
        public static int BitsPerPixel(this DxgiFormat fmt)
        {
            switch (fmt)
            {
                case DxgiFormat.R32G32B32A32_TYPELESS:
                case DxgiFormat.R32G32B32A32_FLOAT:
                case DxgiFormat.R32G32B32A32_UINT:
                case DxgiFormat.R32G32B32A32_SINT:
                    return 128;

                case DxgiFormat.R32G32B32_TYPELESS:
                case DxgiFormat.R32G32B32_FLOAT:
                case DxgiFormat.R32G32B32_UINT:
                case DxgiFormat.R32G32B32_SINT:
                    return 96;

                case DxgiFormat.R16G16B16A16_TYPELESS:
                case DxgiFormat.R16G16B16A16_FLOAT:
                case DxgiFormat.R16G16B16A16_UNORM:
                case DxgiFormat.R16G16B16A16_UINT:
                case DxgiFormat.R16G16B16A16_SNORM:
                case DxgiFormat.R16G16B16A16_SINT:
                case DxgiFormat.R32G32_TYPELESS:
                case DxgiFormat.R32G32_FLOAT:
                case DxgiFormat.R32G32_UINT:
                case DxgiFormat.R32G32_SINT:
                case DxgiFormat.R32G8X24_TYPELESS:
                case DxgiFormat.D32_FLOAT_S8X24_UINT:
                case DxgiFormat.R32_FLOAT_X8X24_TYPELESS:
                case DxgiFormat.X32_TYPELESS_G8X24_UINT:
                case DxgiFormat.Y416:
                case DxgiFormat.Y210:
                case DxgiFormat.Y216:
                    return 64;

                case DxgiFormat.R10G10B10A2_TYPELESS:
                case DxgiFormat.R10G10B10A2_UNORM:
                case DxgiFormat.R10G10B10A2_UINT:
                case DxgiFormat.R11G11B10_FLOAT:
                case DxgiFormat.R8G8B8A8_TYPELESS:
                case DxgiFormat.R8G8B8A8_UNORM:
                case DxgiFormat.R8G8B8A8_UNORM_SRGB:
                case DxgiFormat.R8G8B8A8_UINT:
                case DxgiFormat.R8G8B8A8_SNORM:
                case DxgiFormat.R8G8B8A8_SINT:
                case DxgiFormat.R16G16_TYPELESS:
                case DxgiFormat.R16G16_FLOAT:
                case DxgiFormat.R16G16_UNORM:
                case DxgiFormat.R16G16_UINT:
                case DxgiFormat.R16G16_SNORM:
                case DxgiFormat.R16G16_SINT:
                case DxgiFormat.R32_TYPELESS:
                case DxgiFormat.D32_FLOAT:
                case DxgiFormat.R32_FLOAT:
                case DxgiFormat.R32_UINT:
                case DxgiFormat.R32_SINT:
                case DxgiFormat.R24G8_TYPELESS:
                case DxgiFormat.D24_UNORM_S8_UINT:
                case DxgiFormat.R24_UNORM_X8_TYPELESS:
                case DxgiFormat.X24_TYPELESS_G8_UINT:
                case DxgiFormat.R9G9B9E5_SHAREDEXP:
                case DxgiFormat.R8G8_B8G8_UNORM:
                case DxgiFormat.G8R8_G8B8_UNORM:
                case DxgiFormat.B8G8R8A8_UNORM:
                case DxgiFormat.B8G8R8X8_UNORM:
                case DxgiFormat.R10G10B10_XR_BIAS_A2_UNORM:
                case DxgiFormat.B8G8R8A8_TYPELESS:
                case DxgiFormat.B8G8R8A8_UNORM_SRGB:
                case DxgiFormat.B8G8R8X8_TYPELESS:
                case DxgiFormat.B8G8R8X8_UNORM_SRGB:
                case DxgiFormat.AYUV:
                case DxgiFormat.Y410:
                case DxgiFormat.YUY2:
                    return 32;

                case DxgiFormat.P010:
                case DxgiFormat.P016:
                    return 24;

                case DxgiFormat.R8G8_TYPELESS:
                case DxgiFormat.R8G8_UNORM:
                case DxgiFormat.R8G8_UINT:
                case DxgiFormat.R8G8_SNORM:
                case DxgiFormat.R8G8_SINT:
                case DxgiFormat.R16_TYPELESS:
                case DxgiFormat.R16_FLOAT:
                case DxgiFormat.D16_UNORM:
                case DxgiFormat.R16_UNORM:
                case DxgiFormat.R16_UINT:
                case DxgiFormat.R16_SNORM:
                case DxgiFormat.R16_SINT:
                case DxgiFormat.B5G6R5_UNORM:
                case DxgiFormat.B5G5R5A1_UNORM:
                case DxgiFormat.A8P8:
                case DxgiFormat.B4G4R4A4_UNORM:
                    return 16;

                case DxgiFormat.NV12:
                case DxgiFormat.OPAQUE_420:
                case DxgiFormat.NV11:
                    return 12;

                case DxgiFormat.R8_TYPELESS:
                case DxgiFormat.R8_UNORM:
                case DxgiFormat.R8_UINT:
                case DxgiFormat.R8_SNORM:
                case DxgiFormat.R8_SINT:
                case DxgiFormat.A8_UNORM:
                case DxgiFormat.AI44:
                case DxgiFormat.IA44:
                case DxgiFormat.P8:
                    return 8;

                case DxgiFormat.R1_UNORM:
                    return 1;

                case DxgiFormat.BC1_TYPELESS:
                case DxgiFormat.BC1_UNORM:
                case DxgiFormat.BC1_UNORM_SRGB:
                case DxgiFormat.BC4_TYPELESS:
                case DxgiFormat.BC4_UNORM:
                case DxgiFormat.BC4_SNORM:
                    return 4;

                case DxgiFormat.BC2_TYPELESS:
                case DxgiFormat.BC2_UNORM:
                case DxgiFormat.BC2_UNORM_SRGB:
                case DxgiFormat.BC3_TYPELESS:
                case DxgiFormat.BC3_UNORM:
                case DxgiFormat.BC3_UNORM_SRGB:
                case DxgiFormat.BC5_TYPELESS:
                case DxgiFormat.BC5_UNORM:
                case DxgiFormat.BC5_SNORM:
                case DxgiFormat.BC6H_TYPELESS:
                case DxgiFormat.BC6H_UF16:
                case DxgiFormat.BC6H_SF16:
                case DxgiFormat.BC7_TYPELESS:
                case DxgiFormat.BC7_UNORM:
                case DxgiFormat.BC7_UNORM_SRGB:
                    return 8;

                default:
                    return 0;
            }
        }
    }
}
namespace Pfim
{
    public class Dxt1Dds : CompressedDds
    {
        private const int PIXEL_DEPTH = 3;
        private const int DIV_SIZE = 4;

        public Dxt1Dds(DdsHeader header) : base(header)
        {
        }

        protected override byte PixelDepth => PIXEL_DEPTH;
        protected override byte DivSize => DIV_SIZE;
        protected override byte CompressedBytesPerBlock => 8;
        public override ImageFormat Format => ImageFormat.Rgb24;
        public override int BitsPerPixel => 8 * PIXEL_DEPTH;

        private readonly Color888[] colors = new Color888[4];

        protected override int Decode(byte[] stream, byte[] data, int streamIndex, uint dataIndex, uint width)
        {
            // Colors are stored in a pair of 16 bits
            ushort color0 = stream[streamIndex++];
            color0 |= (ushort)(stream[streamIndex++] << 8);

            ushort color1 = (stream[streamIndex++]);
            color1 |= (ushort)(stream[streamIndex++] << 8);

            // Extract R5G6B5 (in that order)
            colors[0].r = (byte)((color0 & 0x1f));
            colors[0].g = (byte)((color0 & 0x7E0) >> 5);
            colors[0].b = (byte)((color0 & 0xF800) >> 11);
            colors[0].r = (byte)(colors[0].r << 3 | colors[0].r >> 2);
            colors[0].g = (byte)(colors[0].g << 2 | colors[0].g >> 3);
            colors[0].b = (byte)(colors[0].b << 3 | colors[0].b >> 2);

            colors[1].r = (byte)((color1 & 0x1f));
            colors[1].g = (byte)((color1 & 0x7E0) >> 5);
            colors[1].b = (byte)((color1 & 0xF800) >> 11);
            colors[1].r = (byte)(colors[1].r << 3 | colors[1].r >> 2);
            colors[1].g = (byte)(colors[1].g << 2 | colors[1].g >> 3);
            colors[1].b = (byte)(colors[1].b << 3 | colors[1].b >> 2);

            // Used the two extracted colors to create two new colors that are
            // slightly different.
            if (color0 > color1)
            {
                colors[2].r = (byte)((2 * colors[0].r + colors[1].r) / 3);
                colors[2].g = (byte)((2 * colors[0].g + colors[1].g) / 3);
                colors[2].b = (byte)((2 * colors[0].b + colors[1].b) / 3);

                colors[3].r = (byte)((colors[0].r + 2 * colors[1].r) / 3);
                colors[3].g = (byte)((colors[0].g + 2 * colors[1].g) / 3);
                colors[3].b = (byte)((colors[0].b + 2 * colors[1].b) / 3);
            }
            else
            {
                colors[2].r = (byte)((colors[0].r + colors[1].r) / 2);
                colors[2].g = (byte)((colors[0].g + colors[1].g) / 2);
                colors[2].b = (byte)((colors[0].b + colors[1].b) / 2);

                colors[3].r = 0;
                colors[3].g = 0;
                colors[3].b = 0;
            }


            for (int i = 0; i < 4; i++)
            {
                // Every 2 bit is a code [0-3] and represent what color the
                // current pixel is

                // Read in a byte and thus 4 colors
                byte rowVal = stream[streamIndex++];
                for (int j = 0; j < 8; j += 2)
                {
                    // Extract code by shifting the row byte so that we can
                    // AND it with 3 and get a value [0-3]
                    var col = colors[(rowVal >> j) & 0x03];
                    data[dataIndex++] = col.r;
                    data[dataIndex++] = col.g;
                    data[dataIndex++] = col.b;
                }

                // Jump down a row and start at the beginning of the row
                dataIndex += PIXEL_DEPTH * (width - DIV_SIZE);
            }

            // Reset position to start of block
            return streamIndex;
        }
    }
}
namespace Pfim
{
    public class Dxt3Dds : CompressedDds
    {
        private const byte PIXEL_DEPTH = 4;
        private const byte DIV_SIZE = 4;

        protected override byte DivSize => DIV_SIZE;
        protected override byte CompressedBytesPerBlock => 16;
        protected override byte PixelDepth => PIXEL_DEPTH;
        public override int BitsPerPixel => PIXEL_DEPTH * 8;
        public override ImageFormat Format => ImageFormat.Rgba32;

        public Dxt3Dds(DdsHeader header) : base(header)
        {
        }

        private readonly Color888[] colors = new Color888[4];

        protected override int Decode(byte[] stream, byte[] data, int streamIndex, uint dataIndex, uint width)
        {
            /* 
             * Strategy for decompression:
             * -We're going to decode both alpha and color at the same time 
             * to save on space and time as we don't have to allocate an array 
             * to store values for later use.
             */

            // Remember where the alpha data is stored so we can decode simultaneously
            int alphaPtr = streamIndex;

            // Jump ahead to the color data
            streamIndex += 8;

            // Colors are stored in a pair of 16 bits
            ushort color0 = stream[streamIndex++];
            color0 |= (ushort)(stream[streamIndex++] << 8);

            ushort color1 = (stream[streamIndex++]);
            color1 |= (ushort)(stream[streamIndex++] << 8);

            // Extract R5G6B5 (in that order)
            colors[0].r = (byte)((color0 & 0x1f));
            colors[0].g = (byte)((color0 & 0x7E0) >> 5);
            colors[0].b = (byte)((color0 & 0xF800) >> 11);
            colors[0].r = (byte)(colors[0].r << 3 | colors[0].r >> 2);
            colors[0].g = (byte)(colors[0].g << 2 | colors[0].g >> 3);
            colors[0].b = (byte)(colors[0].b << 3 | colors[0].b >> 2);

            colors[1].r = (byte)((color1 & 0x1f));
            colors[1].g = (byte)((color1 & 0x7E0) >> 5);
            colors[1].b = (byte)((color1 & 0xF800) >> 11);
            colors[1].r = (byte)(colors[1].r << 3 | colors[1].r >> 2);
            colors[1].g = (byte)(colors[1].g << 2 | colors[1].g >> 3);
            colors[1].b = (byte)(colors[1].b << 3 | colors[1].b >> 2);

            // Used the two extracted colors to create two new colors
            // that are slightly different.
            colors[2].r = (byte)((2 * colors[0].r + colors[1].r) / 3);
            colors[2].g = (byte)((2 * colors[0].g + colors[1].g) / 3);
            colors[2].b = (byte)((2 * colors[0].b + colors[1].b) / 3);

            colors[3].r = (byte)((colors[0].r + 2 * colors[1].r) / 3);
            colors[3].g = (byte)((colors[0].g + 2 * colors[1].g) / 3);
            colors[3].b = (byte)((colors[0].b + 2 * colors[1].b) / 3);

            for (int i = 0; i < 4; i++)
            {
                byte rowVal = stream[streamIndex++];

                // Each row of rgb values have 4 alpha values that  are
                // encoded in 4 bits
                ushort rowAlpha = stream[alphaPtr++];
                rowAlpha |= (ushort)(stream[alphaPtr++] << 8);

                for (int j = 0; j < 8; j += 2)
                {
                    byte currentAlpha = (byte)((rowAlpha >> (j * 2)) & 0x0f);
                    currentAlpha |= (byte)(currentAlpha << 4);
                    var col = colors[((rowVal >> j) & 0x03)];
                    data[dataIndex++] = col.r;
                    data[dataIndex++] = col.g;
                    data[dataIndex++] = col.b;
                    data[dataIndex++] = currentAlpha;
                }
                dataIndex += PIXEL_DEPTH * (width - DIV_SIZE);
            }
            return streamIndex;
        }
    }

    public class Dxt5Dds : CompressedDds
    {
        private const byte PIXEL_DEPTH = 4;
        private const byte DIV_SIZE = 4;

        private readonly byte[] alpha = new byte[8];
        private readonly Color888[] colors = new Color888[4];

        public override int BitsPerPixel => 8 * PIXEL_DEPTH;
        public override ImageFormat Format => ImageFormat.Rgba32;
        protected override byte DivSize => DIV_SIZE;
        protected override byte CompressedBytesPerBlock => 16;

        public Dxt5Dds(DdsHeader header) : base(header)
        {
        }

        protected override byte PixelDepth => PIXEL_DEPTH;

        protected override int Decode(byte[] stream, byte[] data, int streamIndex, uint dataIndex, uint width)
        {
            streamIndex = Bc5Dds.ExtractGradient(alpha, stream, streamIndex);

            ulong alphaCodes = stream[streamIndex++];
            alphaCodes |= ((ulong)stream[streamIndex++] << 8);
            alphaCodes |= ((ulong)stream[streamIndex++] << 16);
            alphaCodes |= ((ulong)stream[streamIndex++] << 24);
            alphaCodes |= ((ulong)stream[streamIndex++] << 32);
            alphaCodes |= ((ulong)stream[streamIndex++] << 40);

            // Colors are stored in a pair of 16 bits
            ushort color0 = stream[streamIndex++];
            color0 |= (ushort)(stream[streamIndex++] << 8);

            ushort color1 = (stream[streamIndex++]);
            color1 |= (ushort)(stream[streamIndex++] << 8);

            // Extract R5G6B5 (in that order)
            colors[0].r = (byte)((color0 & 0x1f));
            colors[0].g = (byte)((color0 & 0x7E0) >> 5);
            colors[0].b = (byte)((color0 & 0xF800) >> 11);
            colors[0].r = (byte)(colors[0].r << 3 | colors[0].r >> 2);
            colors[0].g = (byte)(colors[0].g << 2 | colors[0].g >> 3);
            colors[0].b = (byte)(colors[0].b << 3 | colors[0].b >> 2);

            colors[1].r = (byte)((color1 & 0x1f));
            colors[1].g = (byte)((color1 & 0x7E0) >> 5);
            colors[1].b = (byte)((color1 & 0xF800) >> 11);
            colors[1].r = (byte)(colors[1].r << 3 | colors[1].r >> 2);
            colors[1].g = (byte)(colors[1].g << 2 | colors[1].g >> 3);
            colors[1].b = (byte)(colors[1].b << 3 | colors[1].b >> 2);

            colors[2].r = (byte)((2 * colors[0].r + colors[1].r) / 3);
            colors[2].g = (byte)((2 * colors[0].g + colors[1].g) / 3);
            colors[2].b = (byte)((2 * colors[0].b + colors[1].b) / 3);

            colors[3].r = (byte)((colors[0].r + 2 * colors[1].r) / 3);
            colors[3].g = (byte)((colors[0].g + 2 * colors[1].g) / 3);
            colors[3].b = (byte)((colors[0].b + 2 * colors[1].b) / 3);

            for (int alphaShift = 0; alphaShift < 48; alphaShift += 12)
            {
                byte rowVal = stream[streamIndex++];
                for (int j = 0; j < 4; j++)
                {
                    // 3 bits determine alpha index to use
                    byte alphaIndex = (byte)((alphaCodes >> (alphaShift + 3 * j)) & 0x07);
                    var col = colors[((rowVal >> (j * 2)) & 0x03)];
                    data[dataIndex++] = col.r;
                    data[dataIndex++] = col.g;
                    data[dataIndex++] = col.b;
                    data[dataIndex++] = alpha[alphaIndex];
                }
                dataIndex += PIXEL_DEPTH * (width - DIV_SIZE);
            }
            return streamIndex;
        }
    }

    internal interface IDecodeDds
    {
        DdsLoadInfo ImageInfo(DdsHeader header);
        byte[] Decode(Stream str, DdsHeader header, DdsLoadInfo imageInfo);
    }

    /// <summary>
    /// A DirectDraw Surface that is not compressed.  
    /// Thus what is in the input stream gets directly translated to the image buffer.
    /// </summary>
    public class UncompressedDds : Dds
    {
        private readonly uint? _bitsPerPixel;
        private readonly bool? _rgbSwapped;
        private ImageFormat _format;

        internal UncompressedDds(DdsHeader header, uint bitsPerPixel, bool rgbSwapped) : base(header)
        {
            _bitsPerPixel = bitsPerPixel;
            _rgbSwapped = rgbSwapped;
        }

        internal UncompressedDds(DdsHeader header) : base(header)
        {

        }

        public override int BitsPerPixel => ImageInfo().Depth;

        public override ImageFormat Format => _format;

        public override bool Compressed => false;
        public override void Decompress()
        {
        }

        protected override void Decode(Stream stream, bool decompress)
        {
            Data = DataDecode(stream);
        }

        /// <summary>Determine image info from header</summary>
        public DdsLoadInfo ImageInfo()
        {
            bool rgbSwapped = _rgbSwapped ?? Header.PixelFormat.RBitMask < Header.PixelFormat.GBitMask;

            switch (_bitsPerPixel ?? Header.PixelFormat.RGBBitCount)
            {
                case 8:
                    return new DdsLoadInfo(false, rgbSwapped, true, 1, 1, 8, ImageFormat.Rgb8);
                case 16:
                    ImageFormat format = SixteenBitImageFormat();
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 2, 16, format);
                case 24:
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 3, 24, ImageFormat.Rgb24);
                case 32:
                    return new DdsLoadInfo(false, rgbSwapped, false, 1, 4, 32, ImageFormat.Rgba32);
                default:
                    throw new Exception($"Unrecognized rgb bit count: {Header.PixelFormat.RGBBitCount}");
            }
        }

        private ImageFormat SixteenBitImageFormat()
        {
            var pf = Header.PixelFormat;

            if (pf.ABitMask == 0xF000 && pf.RBitMask == 0xF00 && pf.GBitMask == 0xF0 && pf.BBitMask == 0xF)
            {
                return ImageFormat.Rgba16;
            }

            if (pf.PixelFormatFlags.HasFlag(DdsPixelFormatFlags.AlphaPixels))
            {
                return ImageFormat.R5g5b5a1;
            }

            return pf.GBitMask == 0x7e0 ? ImageFormat.R5g6b5 : ImageFormat.R5g5b5;
        }

        /// <summary>Calculates the number of bytes to hold image data</summary>
        private int CalcSize(DdsLoadInfo info)
        {
            int width = (int)Math.Max(info.DivSize, Header.Width);
            int height = (int)Math.Max(info.DivSize, Header.Height);
            return (int)(width / info.DivSize * height / info.DivSize * info.BlockBytes);
        }

        /// <summary>Decode data into raw rgb format</summary>
        private byte[] DataDecode(Stream str)
        {
            var imageInfo = ImageInfo();
            _format = imageInfo.Format;

            byte[] data = new byte[CalcSize(imageInfo)];

            PfimUtil.Fill(str, data, 0x10000);

            // Swap the R and B channels
            if (imageInfo.Swap)
            {
                switch (imageInfo.Format)
                {
                    case ImageFormat.Rgba32:
                        for (int i = 0; i < data.Length; i += 4)
                        {
                            byte temp = data[i];
                            data[i] = data[i + 2];
                            data[i + 2] = temp;
                        }
                        break;
                    case ImageFormat.Rgba16:
                        for (int i = 0; i < data.Length; i += 2)
                        {
                            byte temp = (byte)(data[i] & 0xF);
                            data[i] = (byte)((data[i] & 0xF0) + (data[i + 1] & 0XF));
                            data[i + 1] = (byte)((data[i + 1] & 0xF0) + temp);

                        }
                        break;
                    default:
                        throw new Exception($"Do not know how to swap {imageInfo.Format}");
                }
            }

            return data;
        }
    }
}
