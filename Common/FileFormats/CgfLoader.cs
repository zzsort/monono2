using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;

namespace monono2.Common
{
    public struct MeshFace
    {
        public int v0;
        public int v1;
        public int v2;
    }
    public class MeshData
    {
        public Vector3[] vertices;
        public MeshFace[] indices;
    }

    public enum HelperType
    {
        Point = 0,
        Dummy = 1,
        Xref = 2,
        Camera = 3,
        Geometry = 4,
    }

    public class HelperData
    {
        public HelperType helperType;
        public Vector3 position; // needs /100
    }

    public class MaterialData
    {
        public string name;
        public int ChunkId;
        public int MaterialType;
        public List<int> MultiMaterialIds; // if matType==2, this mat is a group of matIds.
        public int MaterialFlags;
        public bool IsCollision; //local to this mat
    }

    public class NodeData
    {
        /*
         * // References:
        // https://github.com/Markemp/Cryengine-Converter/blob/github-master/cgf-converter/CryEngine_Core/Chunks/ChunkNode.cs
        // pyffi - knows some aion-specific structures
        // CryHeaders.h, CryHeaders_info.h, CGFContent.h - original structures
        // CryENGINE-3.3.4.2456\Tools - cgf/cga maya/3ds export scripts. 
        // https://docs.cryengine.com/display/CEMANUAL/Using+the+Resource+Compiler - includes caf compression
        */
        public int chunkId; // chunk header

        public int materialId;
        public MaterialData Material;

        public int parentId;
        public List<NodeData> Children;

        public int objectId; // the object is data that is associated with this node
        public MeshData Mesh; // not null if type is mesh
        public HelperData Helper; // not null if type is helper
        

        public float[] transform; //4x4

        // transform is the combination of these next 3:
        public Vector3 position;//len=3
        public float[] rotQuat;//len=4
        public Vector3 scale;//len=3

        public int positionControllerId;
        public int rotationControllerId;
        public int scaleControllerId;
    }

    public enum CgfChunkType : uint
    {
        Mesh = 0xCCCC0000,
        Helper = 0xCCCC0001,
        Node = 0xCCCC000B,
        Material = 0xCCCC000C,
        Controller = 0xCCCC000D,
    }

    public class CgfChunkHeader
    {
        public CgfChunkType chunkType;
        public int chunkVersion;
        public int chunkOffset;
        public int chunkId;

        public static CgfChunkHeader Read(BinaryReader br)
        {
            var header = new CgfChunkHeader();
            header.chunkType = (CgfChunkType)br.ReadInt32();
            header.chunkVersion = br.ReadInt32();
            header.chunkOffset = br.ReadInt32();
            header.chunkId = br.ReadInt32();
            return header;
        }
    }

    public class CgfLoader
    {
        public List<CgfChunkHeader> m_chunkHeaders;

        private byte[] m_originalFileBytes;

        public List<NodeData> Nodes = new List<NodeData>();

        private Dictionary<int, MaterialData> m_materials = new Dictionary<int, MaterialData>();

        public CgfLoader(string path)
        {
            Load(File.ReadAllBytes(path));
        }

        public CgfLoader(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                Load(ms.ToArray());
            }
        }

        private enum ControllerType
        {
            NONE = 0,
            CRYBONE = 1,
            LINEAR1 = 2,
            LINEAR3 = 3,
            LINEARQ = 4,
            BEZIER1 = 5,
            BEZIER3 = 6,
            BEZIERQ = 7,
            // TCB = tension-continuity-bias. after value is 5 floats: t,c,b,ein,eout
            TCB1 = 8,  // 1 float
            TCB3 = 9,  // 3 values xyz, 1+8*4 bytes per frame
            TCBQ = 10, // 4 value rotation, 1+9*4 bytes per frame
            BSPLINE2O = 11, //2-byte fixed values, open
            BSPLINE1O = 12, //1-byte fixed values, open
            BSPLINE2C = 13, //2-byte fixed values, closed
            BSPLINE1C = 14, //1-byte fixed values, closed
            CONST = 15
        }
        private class ControllerData
        {
            public int StartTicks;
            public float[] Params;
        }
        private List<ControllerData> GetControllerData(int idx, ControllerType expectedControllerType)
        {
            if (idx < 0 || idx >= m_chunkHeaders.Count)
                throw new ArgumentOutOfRangeException();
            if (m_chunkHeaders[idx].chunkType != CgfChunkType.Controller)
                throw new ArgumentException();

            using (var br = new BinaryReader(new MemoryStream(m_originalFileBytes)))
            {
                br.BaseStream.Seek(m_chunkHeaders[idx].chunkOffset, SeekOrigin.Begin);
                
                br.ReadInt32(); // chunktype
                br.ReadInt32(); // chunkversion
                br.ReadInt32(); // chunkOffset
                br.ReadInt32(); // this chunk id

                var controllerType = br.ReadInt32(); // CtrlType
                if (controllerType != (int)expectedControllerType &&
                    (controllerType != 6 && expectedControllerType != ControllerType.TCB3)) // hack - these are compatible
                    throw new InvalidOperationException("unexpected controller type");
                int numKeys = br.ReadInt32();
                br.ReadInt32(); // flags
                int controllerId = br.ReadInt32(); // controllerId

                if (numKeys < 1)
                    throw new InvalidOperationException("unexpected key count");
                if (controllerId != idx)
                    throw new InvalidOperationException("unexpected controller id");

                var result = new List<ControllerData>();
                for (int i = 0; i < numKeys; i++)
                {
                    var cd = new ControllerData();
                    cd.StartTicks = br.ReadInt32();
                    // TODO - each key type uses a different struct. this is good enough for now...
                    cd.Params = new float[9];
                    for (int j = 0; j < 8; j++)
                        cd.Params[j] = br.ReadSingle();

                    if (controllerType == (int)ControllerType.TCBQ) {
                        cd.Params[8] = br.ReadSingle();
                    }

                    result.Add(cd);
                }
                return result;
            }
        }

        private void Load(byte[] rawBytes)
        {
            m_originalFileBytes = rawBytes;
            m_chunkHeaders = new List<CgfChunkHeader>();
            using (var meshInputStream = new BinaryReader(new MemoryStream(m_originalFileBytes)))
            {
                byte[] signature = meshInputStream.ReadBytes(8);
                if (signature[0] != 0x4E || signature[1] != 0x43 || signature[2] != 0x41 || signature[3] != 0x69
                        || signature[4] != 0x6F || signature[5] != 0x6E || signature[6] != 0x00 || signature[7] != 0x00) // NCAion
                    throw new IOException("Wrong signature");

                int fileType = meshInputStream.ReadInt32();
                if ((uint)fileType == 0xFFFF0001) // animation data
                    throw new InvalidOperationException("todo"); // System.out.println("Animation data");
                if ((uint)fileType != 0xFFFF0000) // static model
                    throw new IOException("Wrong filetype");

                meshInputStream.ReadInt32(); // unknown data

                int tableOffset = meshInputStream.ReadInt32();

                // Move to the chunks table
                meshInputStream.BaseStream.Seek(tableOffset, SeekOrigin.Begin);

                int chunksCount = meshInputStream.ReadInt32();
                for (int i = 0; i < chunksCount; i++)
                {
                    var chunkHeader = CgfChunkHeader.Read(meshInputStream);
                    m_chunkHeaders.Add(chunkHeader);
                }
            }

            for (int i = 0; i < m_chunkHeaders.Count; i++)
            {
                if (m_chunkHeaders[i].chunkType == CgfChunkType.Material)
                {
                    m_materials.Add(i, LoadMaterialData(i));
                }
            }

            // Create NodeData items
            var flatNodes = new List<NodeData>();
            for (int i = 0; i < m_chunkHeaders.Count; i++)
            {
                if (m_chunkHeaders[i].chunkType == CgfChunkType.Node)
                {
                    flatNodes.Add(GetNodeData(i));
                }
            }

            // TODO - add check to ensure all nodes are accounted for

            // Build node tree. For each node, find the parent (or root) and add it to its parent's children.
            foreach (var node in flatNodes)
            {
                if (node.parentId != -1)
                {
                    foreach (var p in flatNodes)
                        if (p.chunkId == node.parentId)
                        {
                            if (p.Children == null)
                                p.Children = new List<NodeData>();
                            p.Children.Add(node);
                            break;
                        }
                }
                else
                {
                    Nodes.Add(node); // node is top level
                }
            }
        }

        public bool IsNodeCollidable(NodeData node)
        {
            if (node.Material == null)
                return false;
            return IsMaterialCollidable(node.Material);
        }

        private bool IsMaterialCollidable(MaterialData matTree)
        {
            if (matTree.IsCollision)
                return true;
            if (matTree.MultiMaterialIds != null)
            {
                // this looks for any collidable material in the group and if found reports the node as collidable.
                // if there is a mix of both collide/non-collide mats, the associated mesh is both displayed and used for collision.
                foreach (int matId in matTree.MultiMaterialIds)
                {

                    if (matId == -1) continue; // but why?

                    MaterialData m = m_materials[matId];

                    if (m.MaterialType == 0) continue; // TODO 5.x LF6 has some of these... should investigate

                    if (m.MaterialType == 2) // material is another group of materials...
                    {
                        if (IsMaterialCollidable(m)) // recurse
                            return true;
                    }
                    else if (m.MaterialType != 1)
                        throw new InvalidOperationException("unexpected matType under a matType 2: " + m.MaterialType);

                    if (m.IsCollision)
                        return true;
                }
            }
            return false;
        }

        public void TraverseNodes(Action<NodeData, Matrix> funcNodeHandler, ref Matrix brushMatrix)
        {
            TraverseNodesInternal(0, Nodes, Vector3.Zero /*objectOrigin*/, Vector3.One/*parentScale*/,ref brushMatrix, funcNodeHandler);
        }

        private void TraverseNodesInternal(int recurseDepth, IEnumerable<NodeData> nodeList, Vector3 objectOrigin, Vector3 parentScale, ref Matrix brushMatrix,
            Action<NodeData, Matrix> funcNodeHandler)
        {
            int i = 0;
            foreach (var node in nodeList)
            {
                var localPos = objectOrigin + node.position;
                if (node.Helper != null)
                {
                    //Debug.WriteLine($"  apply helper: {node.Helper.position} TYPE:{node.Helper.helperType}");

                    if (node.Helper.helperType == HelperType.Point)
                    {
                        //Debug.WriteLine("point! children:" + (node.Children != null));
                    }
                    else if (node.Helper.helperType == HelperType.Dummy)
                    {
                        if (node.Children == null)
                        {
                            continue;
                        }
                        TraverseNodesInternal(recurseDepth + 1, node.Children, localPos, node.scale, ref brushMatrix, funcNodeHandler);
                    }
                    else
                        throw new InvalidOperationException("unhandled helper type " + node.Helper.helperType);

                    //Debug.WriteLine($"  end helper: {node.Helper.position} TYPE:{node.Helper.helperType}");
                    continue;
                }
                
                //============== transform
                Matrix m = Matrix.CreateScale(node.scale);
                m *= Matrix.CreateFromQuaternion(new Quaternion(-node.rotQuat[0], -node.rotQuat[1], -node.rotQuat[2], node.rotQuat[3]));
                m *= Matrix.CreateTranslation(localPos / 100f);
                m *= Matrix.CreateScale(parentScale);
                m *= brushMatrix;
                //========================
                
                funcNodeHandler(node, m);

                i++;
            }
        }

        private MeshData GetMeshData(int idx)
        {
            using (var br = new BinaryReader(new MemoryStream(m_originalFileBytes)))
            {
                if (m_chunkHeaders[idx].chunkType != CgfChunkType.Mesh) // 0xCCCC0000
                    return null; //throw new InvalidOperationException("chunk is not a mesh");

                // Move to the chunks table
                br.BaseStream.Seek(m_chunkHeaders[idx].chunkOffset, SeekOrigin.Begin);

                // Skip duplicate chunk header and byte[hasVertexWeights, hasVertexColors, reserved1, reserved2]
                br.ReadInt32(); // chunktype
                br.ReadInt32(); // chunkversion
                br.ReadInt32(); // chunkOffset
                br.ReadInt32(); // this chunk id
                br.ReadInt32(); // parent chunk id?

                int verticesCount = br.ReadInt32();

                // Skip uvsCount
                br.ReadInt32();

                int indicesCount = br.ReadInt32();

                // Skip vertAnim reference
                br.ReadInt32();

                var res = new MeshData();
                res.vertices = new Vector3[verticesCount];
                res.indices = new MeshFace[indicesCount];

                // read vertices
                for (int i = 0; i < verticesCount; i++)
                {
                    res.vertices[i] = new Vector3();
                    res.vertices[i].X = br.ReadSingle() / 100f;
                    res.vertices[i].Y = br.ReadSingle() / 100f;
                    res.vertices[i].Z = br.ReadSingle() / 100f;

                    // Skip normal
                    br.ReadInt32();
                    br.ReadInt32();
                    br.ReadInt32();
                }

                // read indices
                for (int i = 0; i < indicesCount; i++)
                {
                    res.indices[i] = new MeshFace();
                    res.indices[i].v0 = br.ReadInt32();
                    res.indices[i].v1 = br.ReadInt32();
                    res.indices[i].v2 = br.ReadInt32();

                    // Skip Material and Smoothing Group
                    br.ReadInt32();
                    br.ReadInt32();
                }
                return res;
            }
        }

        private HelperData GetHelperData(int idx)
        {
            using (var br = new BinaryReader(new MemoryStream(m_originalFileBytes)))
            {
                if (m_chunkHeaders[idx].chunkType != CgfChunkType.Helper) // 0xCCCC0001
                    return null; //throw new InvalidOperationException("chunk is not a helper");

                // Move to the chunks table
                br.BaseStream.Seek(m_chunkHeaders[idx].chunkOffset, SeekOrigin.Begin);

                // skip header
                br.ReadInt32();
                br.ReadInt32();
                br.ReadInt32();
                br.ReadInt32();

                var result = new HelperData();
                result.helperType = (HelperType)br.ReadInt32();
                
                float x = br.ReadSingle();
                float y = br.ReadSingle();
                float z = br.ReadSingle();
                result.position = new Vector3(x, y, z);
                
                return result;
            }
        }

        private NodeData GetNodeData(int idx)
        {
            using (var br = new BinaryReader(new MemoryStream(m_originalFileBytes)))
            {
                if (m_chunkHeaders[idx].chunkType != CgfChunkType.Node) // 0xCCCC000B
                    throw new InvalidOperationException("expected node chunk");

                // Move to the chunks table
                br.BaseStream.Seek(m_chunkHeaders[idx].chunkOffset, SeekOrigin.Begin);

                var result = new NodeData();

                // skip header
                br.ReadInt32();
                br.ReadInt32();
                br.ReadInt32();
                result.chunkId = br.ReadInt32();

                br.BaseStream.Position += 64; // material name, skip

                result.objectId = br.ReadInt32(); // chunk id of associated chunk. could be mesh, helper, etc.
                result.Mesh = GetMeshData(result.objectId);
                result.Helper = GetHelperData(result.objectId);
                if (result.Mesh == null && result.Helper == null)
                    throw new InvalidOperationException("Expected either a mesh or helper");

                result.parentId = br.ReadInt32(); // parent node id

                br.ReadInt32(); // number of children
                result.materialId = br.ReadInt32();
                if (result.materialId != -1)
                    result.Material = m_materials[result.materialId];
                br.ReadInt32(); // unknown

                result.transform = new float[16];
                for (int i = 0; i < 16; i++)
                    result.transform[i] = br.ReadSingle();

                result.position = new Vector3();
                result.position.X = br.ReadSingle();
                result.position.Y = br.ReadSingle();
                result.position.Z = br.ReadSingle();

                result.rotQuat = new float[4];
                result.rotQuat[0] = br.ReadSingle();
                result.rotQuat[1] = br.ReadSingle();
                result.rotQuat[2] = br.ReadSingle();
                result.rotQuat[3] = br.ReadSingle();

                result.scale = new Vector3();
                result.scale.X = br.ReadSingle();
                result.scale.Y = br.ReadSingle();
                result.scale.Z = br.ReadSingle();

                result.positionControllerId = br.ReadInt32();
                result.rotationControllerId = br.ReadInt32();
                result.scaleControllerId = br.ReadInt32();

                return result;
            }
        }

        private MaterialData LoadMaterialData(int idx)
        {
            using (var br = new BinaryReader(new MemoryStream(m_originalFileBytes)))
            {
                if (m_chunkHeaders[idx].chunkType != CgfChunkType.Material) // 0xCCCC000C
                    throw new InvalidOperationException("expected material chunk");

                // Move to the chunks table
                br.BaseStream.Seek(m_chunkHeaders[idx].chunkOffset, SeekOrigin.Begin);

                var result = new MaterialData();

                br.BaseStream.Position += 4 * 4; // skip header

                var nameBytes = br.ReadBytes(128);
                result.name = Encoding.UTF8.GetString(nameBytes).Trim().Trim('\0');
                result.MaterialType = br.ReadInt32();

                // if type==2, next is multicount. if type is 1, the next int is a color (ignored).
                int multicount = 0;
                int tmp = br.ReadInt32();
                if (result.MaterialType == 2)
                    multicount = tmp;

                // if matType==1, the next chunk is texture/shader info.
                // if matType==2, the next chunk is zeros.
                br.BaseStream.Position += 67 * 4 + 128 + 263 * 4 + 128 + 204 * 4;

                result.MaterialFlags = br.ReadInt32();
                float collision = br.ReadSingle();
                if (collision != 0f && collision != 1f)
                    throw new InvalidOperationException("expected 0.0 or 1.0 for collision flag.");
                result.IsCollision = (collision == 1f);

                br.ReadSingle();
                br.ReadSingle();

                if (result.MaterialType == 2 && multicount > 0)
                    result.MultiMaterialIds = new List<int>();
                for (int i = 0; i < multicount; i++)
                    result.MultiMaterialIds.Add(br.ReadInt32());

                return result;
            }
        }
        
        // hack: creates a new cgf at the specified time in ticks.
        // uses the controllers to modify the original transforms.
        // this loads exact keyframe values, curves are not interpolated.
        public CgfLoader CloneAtTime(int ticks)
        {
            // TODO - check loop type.
            // TODO - check ticks is not greater than global range - need to load timing chunk.
            // TODO - validate keyframe start times are ascending and within global range.
            // TODO - validate controller type. TCB3 for pos, scale, TCBQ for rot, others unexpected...
            // TODO - validate cga vs cgf... some doors have .cgf extension...

            CgfLoader clone;
            using (var stream = new MemoryStream(m_originalFileBytes))
                clone = new CgfLoader(stream);

            //Debug.WriteLine("cga");
            //Debug.WriteLine("-----------------------------------------------");
            foreach (var node in clone.Nodes)
            {
                if (node.positionControllerId != -1)
                {
                    var cd = clone.GetControllerData(node.positionControllerId, ControllerType.TCB3);
                    for (int i = cd.Count - 1; i >= 0; i--) {
                        if (ticks >= cd[i].StartTicks) {
                            node.position.X = cd[i].Params[0];
                            node.position.Y = cd[i].Params[1];
                            node.position.Z = cd[i].Params[2];
                            break;
                        }
                    }
                }
                //Debug.WriteLine("node");
                //Debug.WriteLine("");
                if (node.rotationControllerId != -1)
                {
                    // Note: collision meshes usually have simpler transforms, for example most have no rotation,
                    // so bugs here have less chance of affecting geo data.

                    // BUG: idraksha - idraksha_door\IDraksha5f_eventbridge01a.cga - rotated incorrectly, but has no collision data.

                    var cd = clone.GetControllerData(node.rotationControllerId, ControllerType.TCBQ);

                    if (cd.Count <= 1)
                    {
                        // BUG: doors in theo lab have nodes with broken rotations.
                        // see librarydoor_04d controller=12 values: {0, 45 deg, 45 deg, 180 deg}
                        // they appear in a 1-key set, so ignoring those for now.
                        goto skipRot;
                    }

                    var rot = new Quaternion(node.rotQuat[0], node.rotQuat[1], node.rotQuat[2], node.rotQuat[3]);

                    int curTime = 0;
                    for (int i = 0; i < cd.Count; i++)
                    {
                        // params 4 through 8: t,c,b,ein,eout
                        //if (cd[i].Params[4] != 0 || cd[i].Params[5] != 0 || cd[i].Params[6] != 0 || cd[i].Params[7] != 0 || cd[i].Params[8] != 0)
                        //    throw new InvalidOperationException("expected zeroes");
                        
                        if (!(Math.Abs(cd[i].Params[0] - 1) < 0.00001 &&
                            Math.Abs(cd[i].Params[1]) < 0.00001 &&
                            Math.Abs(cd[i].Params[2]) < 0.00001 &&
                            Math.Abs(cd[i].Params[3]) < 0.00001))
                        {
                            rot *= Quaternion.CreateFromAxisAngle(
                                new Vector3(cd[i].Params[0], cd[i].Params[1], cd[i].Params[2]),
                                cd[i].Params[3]);
                        }

                        // Check if ticks ends before the next frame, or past the end
                        if ((i < cd.Count - 1 && ticks <= curTime + cd[i].StartTicks) || i == cd.Count - 1)
                        {
                            // stop and use this frame
                            node.rotQuat[0] = rot.X;
                            node.rotQuat[1] = rot.Y;
                            node.rotQuat[2] = rot.Z;
                            node.rotQuat[3] = rot.W;
                            break;
                        }

                    }
                    skipRot:;
                }

                if (node.scaleControllerId != -1)
                {
                    var cd = clone.GetControllerData(node.scaleControllerId, ControllerType.TCB3);
                    foreach (var d in cd)
                        if (d.Params[0] != 1 || d.Params[1] != 1 || d.Params[2] != 1)
                        {
                            node.scale.X *= d.Params[0];
                            node.scale.Y *= d.Params[1];
                            node.scale.Z *= d.Params[2];
                        }
                }
            }
            return clone;
        }
    }
}
