using System;
using System.IO;

namespace monono2.Common.BinaryXml
{
    public class BinaryXmlStringTable
    {
        private byte[] m_data;

        public String getData(int index)
        {
            if (index == 0)
                return "";

            return ByteHelpers.ReadUTF16Z(m_data, index * 2);
        }

        public void Read(BinaryReader input)
        {
            int count = BinaryXmlFileHelpers.ReadPackedS32(input);
            m_data = input.ReadBytes(count);
        }
    }
}
