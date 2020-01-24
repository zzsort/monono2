using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using monono2.Common;
using monono2.Common.Navigation;

namespace monono2.AionMonoLib
{
    public class Label3D
    {
        public Vector3 position;
        public string text;
    }
    public class RenderData
    {
        public List<VertexBuffer> vertexBuffer;
        public List<VertexBuffer> vegetationVertexBuffer;
        public List<VertexBuffer> terrainVertexBuffer;
        public List<VertexBuffer> collisionVertexBuffer;

        public List<VertexBuffer> doorVertexBuffer1;
        public List<VertexBuffer> doorVertexBuffer2;
        public List<VertexBuffer> doorCollisionVertexBuffer1;
        public List<VertexBuffer> doorCollisionVertexBuffer2;

        public VertexBuffer lineVertexBuffer;
        public VertexBuffer floorLineVertexBuffer;
        public VertexBuffer astarLineVertexBuffer;

        public List<Label3D> labels = new List<Label3D>();
    }
    
    public class ContentLoader
    {
        private RenderData m_renderData;
        GraphicsDevice GraphicsDevice;

        private GeoSpace m_geoSpace = new GeoSpace();
        CompiledNavMeshSet m_tempCompiledNavMeshSet;
        Vector3 m_testNavTarget = new Vector3(1900.072f, 2591.614f, 264.4005f);
        Vector3 m_testNavStart = new Vector3(1874.048f, 2618.841f, 289.2002f);

        public ContentLoader(GraphicsDevice GraphicsDevice, RenderData renderData)
        {
            this.GraphicsDevice = GraphicsDevice;

            // ContentLoader loads data into RenderData
            m_renderData = renderData;
        }
        
        public void LoadLevelData(string aionRoot, string level)
        {
            using (var levelDir = new DirManager(Path.Combine(aionRoot, "Levels", level)))
            using (var meshesDir = new DirManager(aionRoot, new[] {
                @"levels",
                @"objects\npc\event_object",
                @"objects\npc\level_object",
                @"objects\npc\warship", }))
            {
                LoadContentHelper(levelDir, meshesDir);
            }
        }
        
        // loads a single cgf in the viewer
        public void LoadCgf(CgfLoader cgf)
        {
            var vertices = new List<VertexPositionColor>();
            var collisionVertices = new List<VertexPositionColor>();
            var lineVertices = new List<VertexPositionColor>();

            var xform = Matrix.Identity;
            DrawCgfInWorld(cgf, ref xform, Vector3.Zero, CgfDrawStyle.Brush, vertices, collisionVertices, lineVertices);
            
            m_renderData.vertexBuffer = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, vertices);

            m_renderData.collisionVertexBuffer = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, collisionVertices);

            if (lineVertices.Count > 0)
            {
                m_renderData.lineVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), lineVertices.Count, BufferUsage.WriteOnly);
                m_renderData.lineVertexBuffer.SetData<VertexPositionColor>(lineVertices.ToArray());
            }
        }

        public void LoadCgfDoorHack(CgfLoader cgf, int ticks)
        {
            var vertices = new List<VertexPositionColor>();
            var collisionVertices = new List<VertexPositionColor>();
            var lineVertices = new List<VertexPositionColor>();

            var xform = Matrix.Identity;
            DrawDoorCgaInWorld(cgf, ticks, ref xform, Vector3.Zero, vertices, collisionVertices, lineVertices);

            m_renderData.vertexBuffer = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, vertices);

            m_renderData.collisionVertexBuffer = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, collisionVertices);

            if (lineVertices.Count > 0)
            {
                m_renderData.lineVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), lineVertices.Count, BufferUsage.WriteOnly);
                m_renderData.lineVertexBuffer.SetData<VertexPositionColor>(lineVertices.ToArray());
            }
        }


        private void LoadContentHelper(DirManager levelDir, DirManager meshesDir)
        {
            var TIMER = DateTime.Now;

            //=========

            var vertices = new List<VertexPositionColor>();
            var vegVertices = new List<VertexPositionColor>();
            var collisionVertices = new List<VertexPositionColor>();
            var doorVertices1 = new List<VertexPositionColor>();
            var doorVertices2 = new List<VertexPositionColor>();
            var doorCollisionVertices1 = new List<VertexPositionColor>();
            var doorCollisionVertices2 = new List<VertexPositionColor>();
            var lineVertices = new List<VertexPositionColor>();

            //==========
            //Window.Title = "Loading brushes...";
            LoadBrushes(meshesDir, levelDir, vertices, collisionVertices, lineVertices);

            //============
            //Window.Title = "Loading vegetation...";
            LoadVegetation(meshesDir, levelDir, vegVertices, collisionVertices, lineVertices);

            //============
            // doors have 2 states - start and end. doors can start open or closed, so the end state is the opposite.
            LoadDoors(meshesDir, levelDir,      0, doorVertices1, doorCollisionVertices1, lineVertices);
            LoadDoors(meshesDir, levelDir, 999999, doorVertices2, doorCollisionVertices2, lineVertices);

            // ===========
            // terrain
            H32Loader h32;
            using (var landMapStream = levelDir.OpenFile(@"terrain\land_map.h32"))
                h32 = new H32Loader(landMapStream);

            Debug.WriteLine("Data load TIME: " + (DateTime.Now - TIMER));



            // ============== NAVMESH TEST =====================
            var floorLineVertices = 
                new List<VertexPositionColor>(); // disable navmesh
                //LoadNavMeshTestData(h32);



            //============
            m_renderData.vertexBuffer = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, vertices);
            
            m_renderData.vegetationVertexBuffer = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, vegVertices);

            m_renderData.collisionVertexBuffer = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, collisionVertices);

            m_renderData.doorVertexBuffer1 = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, doorVertices1);
            m_renderData.doorVertexBuffer2 = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, doorVertices2);
            m_renderData.doorCollisionVertexBuffer1 = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, doorCollisionVertices1);
            m_renderData.doorCollisionVertexBuffer2 = VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, doorCollisionVertices2);

            if (lineVertices.Count > 0)
            {
                m_renderData.lineVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), lineVertices.Count, BufferUsage.WriteOnly);
                m_renderData.lineVertexBuffer.SetData<VertexPositionColor>(lineVertices.ToArray());
            }

            if (floorLineVertices.Count > 0)
            {
                m_renderData.floorLineVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), floorLineVertices.Count, BufferUsage.WriteOnly);
                m_renderData.floorLineVertexBuffer.SetData<VertexPositionColor>(floorLineVertices.ToArray());
            }

            vertices = null;
            vegVertices = null;
            collisionVertices = null;
            doorVertices1 = null;
            doorVertices2 = null;
            doorCollisionVertices1 = null;
            doorCollisionVertices2 = null;
            lineVertices = null;
            GC.Collect();

            //============

            //Window.Title = "Loading terrain...";
            using (var levelDataXml = levelDir.OpenFile("leveldata.xml"))
            {
                m_renderData.terrainVertexBuffer = 
                    LoadH32Terrain(GraphicsDevice, h32, new LevelDataXmlLoader(levelDataXml).WaterLevel);
            }
            GC.Collect();
        }
        public void LoadNavMeshGridUnderPosition(Vector3 position)
        {
            // TODO make sure the right compilednavmesh is loaded (currently it is hardcoded!)

            if (m_tempCompiledNavMeshSet == null) {
                Debug.WriteLine("m_tempCompiledNavMeshSet is null!");
                return;
            }

            var mesh = m_tempCompiledNavMeshSet.FindSubgraphUnderPoint(position.X, position.Y, position.Z);
            if (mesh == null)
            {
                Debug.WriteLine("FindSubgraphUnderPoint returned null!");
                return;
            }

            var floorLineVertices = RenderFloorLines(mesh, position, 20);

            if (floorLineVertices.Count > 0)
            {
                m_renderData.floorLineVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), floorLineVertices.Count, BufferUsage.WriteOnly);
                m_renderData.floorLineVertexBuffer.SetData<VertexPositionColor>(floorLineVertices.ToArray());
            }
        }
        private void LoadBrushes(DirManager meshesDir, DirManager levelDir, List<VertexPositionColor> vertices,
            List<VertexPositionColor> collisionVertices, List<VertexPositionColor> lineVertices)
        {
            var brushlst = LevelLoadHelper.CreateBrushLstLoader(levelDir, "brush.lst");
            if (brushlst != null)
            {
                var cgfMap = LevelLoadHelper.CreateBrushLstCgfLoaderMap(meshesDir, brushlst);

                foreach (var brush in brushlst.brushEntries)
                {
                    CgfLoader cgf;
                    if (!cgfMap.TryGetValue(brush.meshIdx, out cgf))
                        continue;

                    //Debug.WriteLine("*Brush: " + cgf.m_path);
                    //Debug.WriteLine($"  entryIdx:{brush.entryIdx} meshIdx:{brush.meshIdx} pos:{brush.position} brRotMat:{brush.rotationMatrix}");

                    Matrix brushMatrix = LevelLoadHelper.GetBrushMatrix(brush);
                    DrawCgfInWorld(cgf, ref brushMatrix, brush.position, CgfDrawStyle.Brush, vertices, collisionVertices, lineVertices);
                }
            }
        }

        private void LoadVegetation(DirManager meshesDir, DirManager levelDir, List<VertexPositionColor> vertices,
            List<VertexPositionColor> collisionVertices, List<VertexPositionColor> lineVertices)
        {
            var ctx = LevelLoadHelper.LoadObjectsLst(meshesDir, levelDir, "");
            if (ctx == null)
                return;

            foreach (var o in ctx.objects)
            {
                if (o.ObjectId >= ctx.cgfMap.Count) {
                    Debug.WriteLine("Warning: object with invalid id: " + o.ObjectId + " - " + o.Position);
                    continue;
                }

                var cgf = ctx.cgfMap[o.ObjectId];
                var xform = LevelLoadHelper.GetObjectMatrix(o);

                DrawCgfInWorld(cgf, ref xform, o.Position, CgfDrawStyle.Vegetation, vertices, collisionVertices, lineVertices);
            }
        }

        private void LoadDoors(DirManager meshesDir, DirManager levelDir, int startTicks, List<VertexPositionColor> doorVertices,
            List<VertexPositionColor> doorCollisionVertices, List<VertexPositionColor> lineVertices)
        {
            var doorInfos = DoorLoader.LoadDoorInfosForLevel(levelDir);
            foreach (var door in doorInfos)
            {
                using (var s = meshesDir.OpenFile(door.object_AnimatedModel))
                {
                    var cgf = new CgfLoader(s);
                    var xform = door.GetMatrix();

                    DrawDoorCgaInWorld(cgf, startTicks, ref xform, door.Pos, doorVertices, doorCollisionVertices, lineVertices);

                    m_renderData.labels.Add(new Label3D { position = door.Pos, text = door.object_AnimatedModel });
                }
            }
        }

        bool DOOR_HACK;
        private void DrawDoorCgaInWorld(CgfLoader cga, int startTicks, ref Matrix xform, Vector3 position,
            List<VertexPositionColor> vertices,
            List<VertexPositionColor> collisionVertices,
            List<VertexPositionColor> lineVertices)
        {
            // Collect collidable meshes
            int collisionStart = collisionVertices.Count;
            
            var vertStartCount = vertices.Count;
            
            DOOR_HACK = true;//draw numbers on nodes
            cga.CloneAtTime(startTicks).TraverseNodes((node, transform)
                => NodeHandler(cga, node, position, transform, vertices, collisionVertices, lineVertices, CgfDrawStyle.Door2),
                ref xform);
            DOOR_HACK = false;

            var bboxTemp = new List<Vector3>();
            for (int i = collisionStart; i < collisionVertices.Count; i++)
                bboxTemp.Add(collisionVertices[i].Position);
            for (int i = vertStartCount; i < vertices.Count; i++)
                bboxTemp.Add(vertices[i].Position);
            Util.DrawBoundingBox(Util.GetBoundingBox(bboxTemp), lineVertices, new Color(25, 66, 250));
        }
        
        enum CgfDrawStyle
        {
            Brush, Vegetation, Door, Door2
        }

        private void DrawCgfInWorld(CgfLoader cgf, ref Matrix xform, Vector3 position, CgfDrawStyle style,
            List<VertexPositionColor> vertices,
            List<VertexPositionColor> collisionVertices,
            List<VertexPositionColor> lineVertices)
        {
            // Collect collidable meshes
            int collisionStart = collisionVertices.Count;

            // traverse nodes
            cgf.TraverseNodes((node, transform) 
                => NodeHandler(cgf, node, position, transform, vertices, collisionVertices, lineVertices, style), 
                ref xform);

            // Add collected vertices as a new mesh.
            m_geoSpace.AddCollidableMeshToTree(collisionVertices, collisionStart, collisionVertices.Count - collisionStart);
        }


        private Color NodeColorizer(CgfDrawStyle style, bool isNodeCollidable, int v0)
        {
            if (isNodeCollidable)
            {
                var chan = v0 % 128 + 128;
                if (style == CgfDrawStyle.Brush)
                    return new Color(255, chan, 0, 80);
                else if (style == CgfDrawStyle.Vegetation)
                    return new Color(chan, 100, 0, 100);
                else if (style == CgfDrawStyle.Door)
                    return new Color(0, 80, 255, 80);
                else if (style == CgfDrawStyle.Door2)
                    return new Color(0, 255, 80, 80);
                return new Color(128, 128, 128);
            }
            else
            {
                var chan = v0 % 256;
                if (style == CgfDrawStyle.Brush)
                    return new Color(chan, 0, 0);
                else if (style == CgfDrawStyle.Vegetation)
                    return new Color(0, chan, 0);
                else if (style == CgfDrawStyle.Door)
                    return new Color(0, 0, chan);
                else if (style == CgfDrawStyle.Door2)
                    return new Color(chan, 0, 80);
                return new Color(128, 128, 128);
            }
        }

        private void NodeHandler(CgfLoader cgf, NodeData node, Vector3 worldPosition, Matrix transform,
            List<VertexPositionColor> vertices,
            List<VertexPositionColor> collisionVertices,
            List<VertexPositionColor> lineVertices,
            CgfDrawStyle style)
        {
            var hackackack = true;

            //Debug.WriteLine($"NODE {transform.Translation} === {node.chunkId}");
            bool isNodeCollidable = cgf.IsNodeCollidable(node);
            foreach (var vi in node.Mesh.indices)
            {
                var v0 = Vector3.Transform(node.Mesh.vertices[vi.v0], transform);
                var v1 = Vector3.Transform(node.Mesh.vertices[vi.v1], transform);
                var v2 = Vector3.Transform(node.Mesh.vertices[vi.v2], transform);

                if (hackackack && DOOR_HACK) {
                    m_renderData.labels.Add(new Label3D { position = v0, text = node.objectId.ToString() });
                    hackackack = false;
                }

                Color color = NodeColorizer(style, isNodeCollidable, vi.v0);
                var dest = isNodeCollidable ? collisionVertices : vertices;

                dest.Add(new VertexPositionColor(v1, color));
                dest.Add(new VertexPositionColor(v0, color));
                dest.Add(new VertexPositionColor(v2, color));
            }

            var zero = Vector3.Transform(Vector3.Zero, transform);
            lineVertices.Add(new VertexPositionColor(zero, Color.Blue));
            lineVertices.Add(new VertexPositionColor(Vector3.Transform(Vector3.UnitX, transform), Color.Blue));
            lineVertices.Add(new VertexPositionColor(zero, Color.Red));
            lineVertices.Add(new VertexPositionColor(Vector3.Transform(Vector3.UnitY, transform), Color.Red));
            lineVertices.Add(new VertexPositionColor(zero, Color.Yellow));
            lineVertices.Add(new VertexPositionColor(Vector3.Transform(Vector3.UnitZ, transform), Color.Yellow));

            lineVertices.Add(new VertexPositionColor(worldPosition, Color.Cyan));
            lineVertices.Add(new VertexPositionColor(zero, Color.Cyan));
        }

        // load terrain vertices for rendering
        private List<VertexBuffer> LoadH32Terrain(GraphicsDevice GraphicsDevice, H32Loader h32, float waterLevel)
        {
            if (h32.isEmpty)
                return null;
            
            bool createBothTriangles = true;
            int triCount = (createBothTriangles ? 2 : 1);

            var terrainVertices = new VertexPositionColorNormal[h32.width * h32.width * 3 * triCount];
            int dst = 0;
            for (int y = 0; y < h32.width - 1; y++)
            {
                for (int x = 0; x < h32.width - 1; x++)
                {
                    var p1 = h32.VertexLookup(x, y);
                    var p2 = h32.VertexLookup(x, y + 1);
                    var p3 = h32.VertexLookup(x + 1, y);
                    var p4 = h32.VertexLookup(x + 1, y + 1);

                    Color color;
                    if (h32.IsCutout(x, y))
                        color = Color.Cyan;
                    else if (p1.Z < waterLevel)
                        color = Color.Blue;
                    else if (p1.Z <= waterLevel + 2)
                        color = Color.SandyBrown;
                    else if (Math.Abs(p1.Z - p2.Z) >= 2 || Math.Abs(p1.Z - p3.Z) >= 2)
                        color = Color.White;//Red;
                    else
                        color = Color.Green;
                    
                    // Tri 1/2
                    var normal1 = MathUtil.CalculateNormal(p1, p2, p3);
                    terrainVertices[dst++] = new VertexPositionColorNormal(p1, color, normal1);
                    terrainVertices[dst++] = new VertexPositionColorNormal(p3, color, normal1);
                    terrainVertices[dst++] = new VertexPositionColorNormal(p2, color, normal1);

                    // Tri 2/2
                    if (createBothTriangles)
                    {
                        var normal2 = MathUtil.CalculateNormal(p2, p4, p3);
                        terrainVertices[dst++] = new VertexPositionColorNormal(p2, color, normal2);
                        terrainVertices[dst++] = new VertexPositionColorNormal(p3, color, normal2);
                        terrainVertices[dst++] = new VertexPositionColorNormal(p4, color, normal2);
                    }
                }
            }

            return VertexBufferUtil.CreateLargeVertexBuffer(GraphicsDevice, terrainVertices);
        }

        public CompiledNavMeshSet GetCompiledNavMeshSet() { return m_tempCompiledNavMeshSet; }
 
        // NavMesh generator
        private List<VertexPositionColor> LoadNavMeshTestData(H32Loader h32)
        {
            // TODO - configure nav dir - determine filename using worldid xml

            DateTime TIMER;
            bool generate = false;
            bool loadFromFile = true;
            bool saveToFile = false;//true;
            //NavMeshBuilder.OPTION_PARALLEL_THREADS = 1;
            //NavMeshBuilder.OPTION_REMOVE_SMALL_GRAPHS = false;
            
            CompiledNavMeshSet compiledMeshSet = null;

            if (generate)
            {
                TIMER = DateTime.Now;
                h32.LoadIntoGeoSpace(m_geoSpace);
                Debug.WriteLine("h32.LoadIntoGeoSpace TIME: " + (DateTime.Now - TIMER));

                TIMER = DateTime.Now;
                m_geoSpace.BuildTree();
                m_geoSpace.Validate();
                Debug.WriteLine("BuildTree TIME: " + (DateTime.Now - TIMER));

                float startX = 90;//133-120; //340 - 20;
                float endX = 200; //133+20; //360 + 20;
                float startY = 103 - 20; //220 - 20;
                float endY = 120;// 103+20; //240 + 20;

                int top = 400;
                int bot = 0;

                float step = 1f;
                var navMeshBuilder = new NavMeshBuilder(startX, startY, bot, endX, endY, top, step);

                TIMER = DateTime.Now;
                compiledMeshSet = navMeshBuilder.ScanFloor(m_geoSpace);
                Debug.WriteLine("ScanFloor TIME: " + (DateTime.Now - TIMER));

                if (saveToFile)
                {
                    // File roundtrip test
                    using (var fs = File.OpenWrite("c:\\temp2\\300200000.nav"))
                        compiledMeshSet.Save(fs);
                }
            }

            if (loadFromFile)
            {
                using (var fs = File.OpenRead(@"c:\temp2\out\210020000.nav"))  //"c:\\temp2\\300200000.nav"))
                    compiledMeshSet = CompiledNavMeshSet.Load(fs);
            }

            Debug.WriteLine("Total estimated compiled mesh size in bytes: " + compiledMeshSet.GetEstimatedFileSizeInBytes());

            var compiledMesh = //compiledMeshSet.FindSubgraphUnderPointWithSnap(126, 66, 145+2, maxFall: 10);
                               //compiledMeshSet.FindSubgraphUnderPointWithSnap(2484, 2614, 300, maxFall: 20);
                               compiledMeshSet.FindSubgraphUnderPointWithSnap(110, 110, 300, maxFall: 2220);
            if (compiledMesh == null)
                throw new InvalidOperationException("point was not over a floor");

            m_tempCompiledNavMeshSet = compiledMeshSet;

            TIMER = DateTime.Now;
            var floorLineVertices = RenderFloorLines(compiledMesh, Vector3.Zero, 0);
            Debug.WriteLine("MESH ASSEMBLY TIME: " + (DateTime.Now - TIMER));

            return floorLineVertices;
        }

        private List<VertexPositionColor> RenderFloorLines(CompiledNavMesh compiledMesh, Vector3 clipCenter, int clipSpan)
        {
            var floorLineVertices = new List<VertexPositionColor>();
            // DEBUG MARKERS

            //    floorLineVertices.Add(new VertexPositionColor(new Vector3(126, 65, 144.3f + 3), Color.Lime));
            //    floorLineVertices.Add(new VertexPositionColor(new Vector3(126, 65, 144.3f), Color.Lime));

            //    floorLineVertices.Add(new VertexPositionColor(new Vector3(127, 64, 145.09f + 3), Color.Red));
            //    floorLineVertices.Add(new VertexPositionColor(new Vector3(127, 64, 145.09f), Color.Red));

            //=============================================
            // TESTING COMPILED MESH GETNEIGHBOR

            //start...
            //    floorLineVertices.Add(new VertexPositionColor(new Vector3(133, 103, 150), Color.White));
            //  floorLineVertices.Add(new VertexPositionColor(new Vector3(133, 103, 144), Color.White));
            /*
            var curNode = compiledMesh.FindFloorUnderPoint(132.9f, 102.9f, 149, 20);
            Vector3 v = compiledMesh.WorldFromBlockIndex(curNode.blockIndex, 144);

            floorLineVertices.Add(new VertexPositionColor(v + new Vector3(0,0,5), Color.Lime));
            floorLineVertices.Add(new VertexPositionColor(v, Color.Lime));

            curNode = compiledMesh.GetNeighbor(curNode, 0, 1);
            v = compiledMesh.WorldFromBlockIndex(curNode.blockIndex, 144);
            floorLineVertices.Add(new VertexPositionColor(v + new Vector3(0, 0, 5), Color.Cyan));
            floorLineVertices.Add(new VertexPositionColor(v, Color.Lime));

            curNode = compiledMesh.GetNeighbor(curNode, 0, 1);
            v = compiledMesh.WorldFromBlockIndex(curNode.blockIndex, 144);
            floorLineVertices.Add(new VertexPositionColor(v + new Vector3(0, 0, 5), Color.Yellow));
            floorLineVertices.Add(new VertexPositionColor(v, Color.Lime));

            curNode = compiledMesh.GetNeighbor(curNode, 1, 1);
            v = compiledMesh.WorldFromBlockIndex(curNode.blockIndex, 144);
            floorLineVertices.Add(new VertexPositionColor(v + new Vector3(0, 0, 5), Color.White));
            floorLineVertices.Add(new VertexPositionColor(v, Color.Lime));
            */
            // END TEST
            //=============================================



            //=============================================
            // TEST A*

            //TestAStar(compiledMesh, new Vector3(141, 21, 145), new Vector3(140, 28, 144), 10, floorLineVertices);

            //TestAStar(compiledMesh, new Vector3(155, 56, 144), new Vector3(169, 23, 144), 10, floorLineVertices);

            //    TestAStar(compiledMesh, new Vector3(130, 101, 145), new Vector3(125, 106, 147), 10, floorLineVertices);
            // END A*
            //=============================================


            int clipX1 = 0;
            int clipY1 = 0;
            int clipX2 = compiledMesh.BlockWidth;
            int clipY2 = compiledMesh.BlockHeight;

            if (clipSpan != 0)
            {
                var node = compiledMesh.FindFloorUnderPoint(clipCenter.X, clipCenter.Y, clipCenter.Z, 50);
                if (node.blockIndex != 0 || node.directionFlags != 0)
                {
                    var pt = compiledMesh.BlockXYFromIndex(node.blockIndex);
                    clipX1 = Math.Max(0, pt.X - clipSpan);
                    clipY1 = Math.Max(0, pt.Y - clipSpan);
                    clipX2 = Math.Min(clipX2, pt.X + clipSpan);
                    clipY2 = Math.Min(clipY2, pt.Y + clipSpan);
                }
            }

            //var pt1 = compiledMesh.WorldFromBlockIndex(2206555, 115);
            //var pt2 = compiledMesh.WorldFromBlockIndex(2208535, 115);
            //var DBGdir = compiledMesh.getDirectionToNeighborByIndex(2206555, 2208535);
            //Util.DrawBoundingBox(BoundingBox.CreateFromPoints(new[] { new Vector3(pt1.X - 0.1f, pt1.Y - 0.1f, 115), new Vector3(pt1.X + 0.1f, pt1.Y + 0.1f, 122) }), floorLineVertices, Color.Lime);
            //Util.DrawBoundingBox(BoundingBox.CreateFromPoints(new[] { new Vector3(pt2.X - 0.1f, pt2.Y - 0.1f, 115), new Vector3(pt2.X + 0.1f, pt2.Y + 0.1f, 122) }), floorLineVertices, Color.Red);

            for (int bY = clipY1; bY < clipY2; bY++)
            {
                for (int bX = clipX1; bX < clipX2; bX++)
                {
                    float x = compiledMesh.X1 + bX * compiledMesh.Step;
                    float y = compiledMesh.Y1 + bY * compiledMesh.Step;

                    compiledMesh.ForeachHeightAtXY(bX, bY,
                        (directionFlags, z) =>
                        {
                            /*Color xcolor = Color.Lime;

                            //if (bX == 28 && bY == 0) Debugger.Break();

                            if (y > 221 && y < 223 && x > 346 && x < 348)
                                xcolor = Color.White;
                            else if (y > 221 && y < 223 && x > 347 && x < 349)
                                xcolor = Color.Blue;
                            else if (y >= 221 && y <= 223 && x >= 346 && x <= 348)
                                xcolor = Color.Red;*/


                            // DRAW ELEVATION MARKER
                            /*    floorLineVertices.Add(new VertexPositionColor(new Vector3(x - 0.1f, y, z), xcolor));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y + 0.1f, z), xcolor));

                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y + 0.1f, z), xcolor));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x + 0.1f, y, z), xcolor));

                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x + 0.1f, y, z), xcolor));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y - 0.1f, z), xcolor));

                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y - 0.1f, z), xcolor));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x - 0.1f, y, z), xcolor));
                                */

                            // EDGE CONNECTIONS
                            // draw lines through the compass points...

                            float halfstep = compiledMesh.Step;
                            float n;

                            n = compiledMesh.GetEdge(bX, bY, z, directionFlags, NavMeshUtil.DIRECTION_LEFT);
                            if (n >= 0)
                            {
                                var test = Color.Orange;
                                if (Math.Abs(z - n) >= 0.9f)
                                {
                                    test = Color.Red;
                                }

                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y, z), test));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x - halfstep, y, n), test));
                            }
                            n = compiledMesh.GetEdge(bX, bY, z, directionFlags, NavMeshUtil.DIRECTION_TL);
                            if (n >= 0)
                            {
                                var test = Color.Orange;
                                if (Math.Abs(z - n) / (new Vector2(x, y) - new Vector2(x - halfstep, y + halfstep)).Length() >= 1.4f)
                                {
                                    test = Color.Magenta;
                                }

                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y, z), test));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x - halfstep, y + halfstep, n), test));
                            }
                            n = compiledMesh.GetEdge(bX, bY, z, directionFlags, NavMeshUtil.DIRECTION_TOP);
                            if (n >= 0)
                            {
                                var test = Color.Orange;
                                if (Math.Abs(z - n) >= 0.9f)
                                {
                                    test = Color.Red;
                                }

                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y, z), test));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y + halfstep, n), test));
                            }
                            n = compiledMesh.GetEdge(bX, bY, z, directionFlags, NavMeshUtil.DIRECTION_TR);
                            if (n >= 0)
                            {
                                var test = Color.Orange;
                                if (Math.Abs(z - n) / (new Vector2(x, y) - new Vector2(x + halfstep, y + halfstep)).Length() >= 1.4f)
                                {
                                    test = Color.Purple;
                                }

                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x, y, z), test));
                                floorLineVertices.Add(new VertexPositionColor(new Vector3(x + halfstep, y + halfstep, n), test));
                            }
                        });
                }
            }
            return floorLineVertices;
        }
    }
}
