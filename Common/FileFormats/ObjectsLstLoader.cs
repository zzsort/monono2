using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace monono2.Common
{
    public class ObjectsLstItem
    {
        public Vector3 Position;
        public int ObjectId;
        public float Scale;
        public int Heading;
    }

    public class ObjectsLstLoader
    {
        public static List<ObjectsLstItem> Load(string filename, int mapWidth, int mapHeight)
        {
            using (var stream = File.OpenRead(filename))
            {
                return Load(stream, mapWidth, mapHeight);
            }
        }

        public static List<ObjectsLstItem> Load(Stream stream, int mapWidth, int mapHeight)
        {
            if (mapWidth <= 0 || mapHeight <= 0)
                throw new ArgumentOutOfRangeException("width and height should be > 0");
            if (mapWidth != mapHeight)
                throw new InvalidOperationException("maps should be square");

            using (var br = new BinaryReader(stream))
            {
                if (br.ReadInt32() != 0x10) throw new InvalidOperationException("objects.lst: expected 0x10 header");

                // TODO - this currently uses the unscaled size. the input to this function should probably
                // be pre-scaled with the unit size, and then this calc will become (65536 / mapWidth).
                float magic = 32768.0f / mapWidth;
                var result = new List<ObjectsLstItem>();
                while (br.BaseStream.Position < br.BaseStream.Length)
                {
                    int xPos = br.ReadUInt16();
                    int yPos = br.ReadUInt16();
                    int zPos = br.ReadUInt16();
                    int objectId = br.ReadByte();
                    int unk123 = br.ReadByte(); // investigate... values 0 and 255. maybe 255 means no collision.
                    float scale = br.ReadSingle();
                    int heading = br.ReadInt32();

                    var item = new ObjectsLstItem();
                    item.Position = new Vector3(xPos / magic, yPos / magic, zPos / magic);
                    item.ObjectId = objectId;
                    item.Scale = scale;
                    item.Heading = heading * 360 / 255;

                    result.Add(item);
                }
                return result;
            }
        }
    }
}
