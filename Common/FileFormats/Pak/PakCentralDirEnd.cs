using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace monono2.Common.FileFormats.Pak
{
    public class PakCentralDirEnd
    {
        public const int HeaderSize = 22;

        public ushort signature1;
        public ushort signature2;
        public ushort diskNum;
        public ushort firstDisk;
        public ushort thisDiskCentralDirCount;
        public ushort totalCentralDirCount;
        public uint centralDirSize;
        public uint centralDirOffset;
        public ushort commentLength;

        public static PakCentralDirEnd Read(BinaryReader br)
        {
            var result = new PakCentralDirEnd();
            result.signature1 = br.ReadUInt16();
            result.signature2 = br.ReadUInt16();
            result.diskNum = br.ReadUInt16();
            result.firstDisk = br.ReadUInt16();
            result.thisDiskCentralDirCount = br.ReadUInt16();
            result.totalCentralDirCount = br.ReadUInt16();
            result.centralDirSize = br.ReadUInt32();
            result.centralDirOffset = br.ReadUInt32();
            result.commentLength = br.ReadUInt16();

            if (result.signature1 != PakConstants.PAK_SIGNATURE1 ||
                result.signature2 != PakConstants.PAK_SIGNATURE2_END)
            {
                if (result.signature1 != PakConstants.ZIP_SIGNATURE1 ||
                    result.signature2 != PakConstants.ZIP_SIGNATURE2_END)
                {
                    throw new InvalidOperationException("bad EOCD signature");
                }

                // zipformat = true
            }

            if (result.diskNum != 0)
                throw new InvalidOperationException("expected disk 0. multi disk not supported");

            if (result.thisDiskCentralDirCount == 0)
                throw new InvalidOperationException("unexpected empty dir count");

            if (result.thisDiskCentralDirCount != result.totalCentralDirCount)
                throw new InvalidOperationException("expected matching counts");

            return result;
        }
    }
}
