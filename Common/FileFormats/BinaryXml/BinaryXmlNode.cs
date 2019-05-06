using System;
using System.Collections.Generic;
using System.IO;

namespace monono2.Common.BinaryXml
{
    public class BinaryXmlNode
    {
        public string Name;
        public string Value;
        public Dictionary<string, string> Attributes;
        public List<BinaryXmlNode> Children;

        public void Read(BinaryReader input, BinaryXmlStringTable table)
        {
            Name = table.getData(BinaryXmlFileHelpers.ReadPackedS32(input));
         //   Log.Write("<" + Name);
            Attributes = new Dictionary<string, string>();
            Children = new List<BinaryXmlNode>();
            Value = null;
            int num1 = input.ReadByte();
            if ((num1 & 1) == 1)
            {
                var offset = BinaryXmlFileHelpers.ReadPackedS32(input);
                Value = table.getData(offset);
            }
            if ((num1 & 2) == 2)
            {
                int attributeCount = BinaryXmlFileHelpers.ReadPackedS32(input);
                for (int index = 0; index < attributeCount; ++index)
                {
                    int keyTableOffset = BinaryXmlFileHelpers.ReadPackedS32(input);
                    int valueTableOffset = BinaryXmlFileHelpers.ReadPackedS32(input);

                    var k = table.getData(keyTableOffset);
                    var v = table.getData(valueTableOffset);
                    //    Log.WriteLine($" \"{k}\"=\"{v}\"");
                    Attributes[k] = v;
                }
            }

            //Log.WriteLine(">");
            //if (Value != null)
            //    Log.WriteLine(Value);

            if ((num1 & 4) == 4) // has child nodes
            {
                int num3 = BinaryXmlFileHelpers.ReadPackedS32(input);
                for (int index = 0; index < num3; ++index)
                {
                    BinaryXmlNode binaryXmlNode = new BinaryXmlNode();
                    binaryXmlNode.Read(input, table);
                    Children.Add(binaryXmlNode);
                }
            }
            //Log.WriteLine("</" + Name + ">");
        }
    }

}
