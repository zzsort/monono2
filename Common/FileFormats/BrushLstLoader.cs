using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

namespace monono2.Common
{
    public class BrushInfo
    {
        public int brushInfoIndex;
        public string filename;
        public BoundingBox bbox;
    }

    public class BrushEntry
    {
        public int entryIdx;
        public int meshIdx; // maps to brushInfoIndex
        public Vector3 position;
        public Matrix rotationMatrix;
        public byte eventType;
    }

    public class BrushLstLoader
    {
        public List<BrushInfo> brushInfoList = new List<BrushInfo>();
        public List<BrushEntry> brushEntries = new List<BrushEntry>();
        public bool[] m_eventUsage = new bool[5];

        public BrushLstLoader(string path)
        { 
            using (var meshInputStream = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                Load(meshInputStream);
            }
        }

        public BrushLstLoader(Stream s)
        {
            Load(new BinaryReader(s));
        }

        private void Load(BinaryReader meshInputStream)
        {
            byte[] signature = new byte[3];
            meshInputStream.Read(signature, 0, 3);
            if (signature[0] != 0x43 || signature[1] != 0x52 || signature[2] != 0x59) // CRY
                throw new IOException("Wrong signature");
            
            int dw1 = meshInputStream.ReadInt32();
            
            int meshDataBlockSz = meshInputStream.ReadInt32();

            if (meshDataBlockSz < 17 || meshDataBlockSz > 19)
            {
                throw new InvalidOperationException("unexpected block size");
            }

            int titlesCount = meshInputStream.ReadInt32();
            for (int i = 0; i < titlesCount; i++)
            {
                int nameLen = meshInputStream.ReadInt32();
                byte[] nameBytes = new byte[nameLen - 4];
                meshInputStream.Read(nameBytes, 0, nameLen - 4);
                // TODO Use these names somehow
            }

            // meshes info
            int meshInfoCount = meshInputStream.ReadInt32();
            byte[] fileNameBytes = new byte[128];
            for (int i = 0; i < meshInfoCount; i++)
            {
                var info = new BrushInfo();
                info.brushInfoIndex = i;

                meshInputStream.ReadInt32(); // skip

                //			int dw1 = meshInputStream.readInt();

                meshInputStream.Read(fileNameBytes, 0, 128);
                info.filename = Encoding.UTF8.GetString(fileNameBytes).Trim().Trim('\0').ToLower().Replace('\\', '/');

                meshInputStream.ReadInt32(); // skip - usually 1, sometimes 3
                    
                float x1 = meshInputStream.ReadSingle();
                float y1 = meshInputStream.ReadSingle();
                float z1 = meshInputStream.ReadSingle();
                float x2 = meshInputStream.ReadSingle();
                float y2 = meshInputStream.ReadSingle();
                float z2 = meshInputStream.ReadSingle();
                info.bbox = new BoundingBox(new Vector3(x1, y1, z1), new Vector3(x2, y2, z2));
                brushInfoList.Add(info);
            }

            // meshes data
            int meshDataCount = meshInputStream.ReadInt32();
            for (int i = 0; i < meshDataCount; i++)
            {
                var meshData = new BrushEntry();
                meshData.entryIdx = i;

                meshInputStream.ReadInt32();//skip
                meshInputStream.ReadInt32();//skip

                meshData.meshIdx = meshInputStream.ReadInt32();
                if (meshData.meshIdx < 0 || meshData.meshIdx >= brushInfoList.Count)
                    throw new IndexOutOfRangeException();

                meshInputStream.ReadInt32();//skip
                meshInputStream.ReadInt32();//skip
                meshInputStream.ReadInt32();//skip


                // read 3x4 matrix
                float[] matrix = new float[3*4];
                for (int j = 0; j < 3*4; j++)
                    matrix[j] = meshInputStream.ReadSingle();
                    
                // position 
                var posx = matrix[0 * 4 + 3];
                var posy = matrix[1 * 4 + 3];
                var posz = matrix[2 * 4 + 3];
                meshData.position = new Vector3(posx, posy, posz);

                // orientation matrix
                var m = new Matrix();
                m.M11 = matrix[0 * 4 + 0];
                m.M12 = matrix[0 * 4 + 1];
                m.M13 = matrix[0 * 4 + 2];
                //m.M14 = matrix[0 * 4 + 3];
                m.M21 = matrix[1 * 4 + 0];
                m.M22 = matrix[1 * 4 + 1];
                m.M23 = matrix[1 * 4 + 2];
                //m.M24 = matrix[1 * 4 + 3];
                m.M31 = matrix[2 * 4 + 0];
                m.M32 = matrix[2 * 4 + 1];
                m.M33 = matrix[2 * 4 + 2];
                //m.M34 = matrix[2 * 4 + 3];
                m.M44 = 1;

                meshData.rotationMatrix = m;
                    
                meshInputStream.ReadByte(); // 100, or some sewing objects are 200 ?
                meshInputStream.ReadByte(); // maybe an angle?
                meshInputStream.ReadByte(); // unknown
                meshInputStream.ReadByte(); // unknown
                int b = meshInputStream.ReadInt32(); // unknown - maybe shorts
                meshInputStream.ReadInt32(); // unknown - maybe shorts
                meshInputStream.ReadInt32(); // 0 - unknown


                // Server Event Decorations
                // 00 = no decoration/also means normal usage of event service 
                // 01 = christmas 
                // 02 = halloween
                // 03 = braxcafe
                // 04 = valentine
                // 08 = oversea maid event
                int eventType = meshInputStream.ReadInt32();
                if (eventType < 0 || eventType > 4)
                {
                    Log.WriteLine($"Ignoring unknown event: {eventType} - {brushInfoList[meshData.meshIdx].filename}");
                    //throw new InvalidOperationException("invalid event type: " + eventType);
                }
                else
                {
                    meshData.eventType = (byte)eventType;
                    m_eventUsage[eventType] = true;
                }

            //    //if (brushInfoList[meshData.meshIdx].filename.IndexOf("halloween", StringComparison.OrdinalIgnoreCase) >= 0)
            //    if (eventType == 2)
            //        Debug.WriteLine(eventType + " " + brushInfoList[meshData.meshIdx].filename);

                meshInputStream.ReadInt32(); // always 3?


                meshInputStream.ReadInt32(); // 0 - unknown

                meshInputStream.ReadBytes(4 * (meshDataBlockSz - 17));

                //if (b != 0)
                //  Debug.WriteLine($"meshIdx:{meshData.meshIdx}  value:{b}  {brushInfoList[meshData.meshIdx].filename}");

                //DEBUG if (eventType == 1)
                brushEntries.Add(meshData);
            }
        }
    }
}
