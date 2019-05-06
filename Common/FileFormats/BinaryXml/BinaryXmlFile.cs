using System;
using System.Collections.Generic;
using System.IO;

namespace monono2.Common.BinaryXml
{
    public class BinaryXmlFile
    {
        public BinaryXmlNode Root;
        
        public void Read(BinaryReader input)
        {
            if (input.ReadByte() != 128)
                throw new Exception("not a binary XML file");
            BinaryXmlStringTable table = new BinaryXmlStringTable();
            table.Read(input);
            Root = new BinaryXmlNode();
            Root.Read(input, table);
        }
    }
}
