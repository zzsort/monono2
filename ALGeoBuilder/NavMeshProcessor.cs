using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using monono2.Common;
using monono2.Common.Navigation;

namespace monono2.ALGeoBuilder
{
    public static class NavMeshProcessor
    {
        // Note: temp buffer makes this class not thread safe
        private static List<Vector3> s_tempVertices = new List<Vector3>();

        // generates navmesh data for the selected levels.
        public static void GenerateAllNav(WorldIdXmlLoader worldIdXmlLoader,
            DirManager meshesDir, DirManager levelsDir, string levelId, 
            bool skipExistingNav, string outputPath)
        {
            int curFolderIndex = -1;
            foreach (var pairs in worldIdXmlLoader.FolderNamesById)
            {
                curFolderIndex++;

                string clientLevelId = pairs.Key;
                string levelFolder = pairs.Value;

                if (!string.IsNullOrEmpty(levelId) && levelId != clientLevelId)
                {
                    // skip excluded
                    continue;
                }

                string outputNavFilename = Path.Combine(outputPath, clientLevelId + ".nav");

                if (skipExistingNav && File.Exists(outputNavFilename))
                {
                    Console.WriteLine($"    ** Skipping (already exists): {clientLevelId} - {levelFolder}");
                    continue;
                }


                Console.WriteLine($"    Loading meshes for {clientLevelId} - {levelFolder}");

                var LEVEL_TIME = DateTime.Now;
                var TIMER = DateTime.Now;
                var geoSpace = new GeoSpace();

                // brushes
                var brushlst = LevelLoadHelper.CreateBrushLstLoader(levelsDir,
                    Path.Combine(levelFolder, "brush.lst"));
                if (brushlst != null)
                {
                    var cgfMap = LevelLoadHelper.CreateBrushLstCgfLoaderMap(meshesDir, brushlst);

                    foreach (var brush in brushlst.brushEntries)
                    {
                        CgfLoader cgf;
                        if (!cgfMap.TryGetValue(brush.meshIdx, out cgf))
                            continue;

                        Matrix brushMatrix = LevelLoadHelper.GetBrushMatrix(brush);
                        AddCgfToWorld(cgf, ref brushMatrix, brush.position, geoSpace);
                    }
                }

                // objects
                var ctx = LevelLoadHelper.LoadObjectsLst(meshesDir, levelsDir, levelFolder);
                if (ctx != null)
                {
                    foreach (var o in ctx.objects)
                    {
                        CgfLoader cgf;
                        if (!ctx.cgfMap.TryGetValue(o.ObjectId, out cgf))
                            continue;
                        
                        var xform = LevelLoadHelper.GetObjectMatrix(o);
                        AddCgfToWorld(cgf, ref xform, o.Position, geoSpace);
                    }
                }

                // terrain
                bool loadedH32 = false;
                string h32path = Path.Combine(levelFolder, @"terrain\land_map.h32");
                if (levelsDir.Exists(h32path)) {
                    using (var landMapStream = levelsDir.OpenFile(h32path))
                        new H32Loader(landMapStream).LoadIntoGeoSpace(geoSpace);
                    loadedH32 = true;
                }

                Console.WriteLine("      Data load time: " + (DateTime.Now - TIMER));

                if (brushlst == null && ctx == null && !loadedH32) {
                    Console.WriteLine("      ** Skipping (no level data found)");
                    continue;
                }

                // build geo
                    TIMER = DateTime.Now;
                geoSpace.BuildTree();
                geoSpace.Validate();
                Console.WriteLine("      Geo build time: " + (DateTime.Now - TIMER));

                // get size of level for scanning
                var bb = geoSpace.GetBoundingBox();
                float startX = Math.Max(0, bb.Min.X);
                float endX = bb.Max.X;
                float startY = Math.Max(0, bb.Min.Y);
                float endY = bb.Max.Y;
                int top = (int)Math.Ceiling(bb.Max.Z);
                int bot = Math.Max(0, (int)bb.Min.Z);

                // TODO - print bounding box
                // TODO - save log file

                if (endX <= startX || endY <= startY || top < bot)
                    throw new InvalidOperationException(
                        $"unexpected level size for {clientLevelId} {levelFolder} bb: {bb}");

                // compile mesh
                TIMER = DateTime.Now;
                float step = 1f;
                var navMeshBuilder = new NavMeshBuilder(startX, startY, bot, endX, endY, top, step);
                CompiledNavMeshSet compiledMeshSet = navMeshBuilder.ScanFloor(geoSpace);
                Console.WriteLine("      Raycast time: " + (DateTime.Now - TIMER));

                // save to file
                using (var fs = File.OpenWrite(outputNavFilename))
                    compiledMeshSet.Save(fs);

                Console.WriteLine($"      Level {clientLevelId} finished in {DateTime.Now - LEVEL_TIME}");
            }
        }

        private static void AddCgfToWorld(CgfLoader cgf, ref Matrix xform,
            Vector3 position, GeoSpace geoSpace)
        {
            s_tempVertices.Clear();

            // traverse nodes
            cgf.TraverseNodes((node, transform) =>
                NodeHandler(cgf, node, position, transform), ref xform);

            // Add collected vertices as a new mesh.
            geoSpace.AddCollidableMeshToTree(s_tempVertices);
        }

        private static void NodeHandler(CgfLoader cgf, NodeData node, Vector3 worldPosition,
            Matrix transform)
        {
            if (!cgf.IsNodeCollidable(node))
                return;

            foreach (var vi in node.Mesh.indices)
            {
                var v0 = Vector3.Transform(node.Mesh.vertices[vi.v0], transform);
                var v1 = Vector3.Transform(node.Mesh.vertices[vi.v1], transform);
                var v2 = Vector3.Transform(node.Mesh.vertices[vi.v2], transform);

                s_tempVertices.Add(v1);
                s_tempVertices.Add(v0);
                s_tempVertices.Add(v2);
            }
        }
    }
}
