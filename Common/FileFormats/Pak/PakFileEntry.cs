using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace monono2.Common.FileFormats.Pak
{
    public class PakFileEntry
    {
        public ushort signature1;
        public ushort signature2;
        public ushort extractVersion;
        public ushort flags;
        public ushort compressionMethod;
        public ushort time;
        public ushort date;
        public uint crc;
        public uint compressedSize;
        public uint uncompressedSize;
        public ushort filenameLength;
        public ushort extraFieldLength;

        public string filename;

        // after Read(), stream points to the start of the compressed data.
        public static PakFileEntry Read(BinaryReader br)
        {
            var result = new PakFileEntry();
            result.signature1 = br.ReadUInt16();
            result.signature2 = br.ReadUInt16();
            result.extractVersion = br.ReadUInt16();
            result.flags = br.ReadUInt16();
            result.compressionMethod = br.ReadUInt16();
            result.time = br.ReadUInt16();
            result.date = br.ReadUInt16();
            result.crc = br.ReadUInt32();
            result.compressedSize = br.ReadUInt32();
            result.uncompressedSize = br.ReadUInt32();
            result.filenameLength = br.ReadUInt16();
            result.extraFieldLength = br.ReadUInt16();

            result.filename = PakUtil.ReadFilename(br, result.filenameLength);

            if (result.signature1 != PakConstants.PAK_SIGNATURE1 ||
                result.signature2 != PakConstants.PAK_SIGNATURE2_FILE)
            {
                if (result.signature1 != PakConstants.ZIP_SIGNATURE1 ||
                    result.signature2 != PakConstants.ZIP_SIGNATURE2_FILE)
                    throw new InvalidOperationException("bad file signature");

                // zipformat = true
            }

            if (result.extraFieldLength != 0)
                throw new InvalidOperationException("extra field not supported");

            return result;
        }
    }
}
