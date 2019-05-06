using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using monono2.Common;
using monono2.Common.FileFormats.Pak;

namespace monono2.ALGeoBuilder
{
    public class AionLevelsProcessor
    {
        // @Option(name = "-o", usage = "Path to the output folder. If not set ./out folder will be used", metaVar = "PATH")
        public string outputPath = "out";

        // @Option(name = "-v", usage = "Version Aion client installation (1 or 2). Default is 2", metaVar = "VER")
        public int aionClientVersion = 2;

        // @Option(name = "-lvl", usage = "Set exect level Id e.g. 110010000", metaVar = "LVLID")
        public string levelId = null;

        // generate .nav navmesh files.
        public bool generateNav = false;
        
        // if true, skip generating .nav files that already exist.
        public bool skipExistingNav = false;

        // Build and generate world and meshes .geo files. this is the main option required for generating
        // geo data.
        public bool generateGeo = false;

        // Create door models, 2 states each.
        public bool generateDoors = false;

        // @Option(name = "-no_h32", usage = "Do not include *.h32 data into *.geo file")
        public bool noH32 = false;

        // @Option(name = "-no_mesh", usage = "Do not generate mesh.geo file")
        public bool noMesh = false;
        
        // @Argument(usage="Path to Aion client installation. Required", metaVar="PATH", required=true)
        public string aionClientPath;

        // -----------------------------------------------------------------------------------------------------------
        private HashSet<string> m_requiredCgfs = new HashSet<string>();
        private HashSet<string> m_loadedCgfs = new HashSet<string>();
        private HashSet<string> m_emptyCgfs = new HashSet<string>();

        private List<string> collectMeshesPath(IEnumerable<string> rootFolders)
	    {
		    var res = new List<string>();
		    foreach (string folder in rootFolders)
		    {
			    // collect meshes
			    var meshFiles = Directory.EnumerateFiles(folder, "*pak", SearchOption.AllDirectories).Where(o => o.IndexOf("_Meshes", 0, StringComparison.OrdinalIgnoreCase) != -1).ToList();
			    res.AddRange(meshFiles);
		    }
		    return res;
	    }

        private class CgfMeshesToLoad
        {
            public List<string> meshFiles = null;
            public int[] meshUsage = null;
        }
        
        // meshs.geo
	    private void WriteToMeshsGeo(string cgfFileName, List<MeshData> meshesPack, 
            BinaryWriter meshesGeoDataStream, byte collisionIntention)
	    {
		    byte[] meshFileNameBytes = Encoding.ASCII.GetBytes(cgfFileName);

		    // mesh file name size
		    meshesGeoDataStream.Write((short)meshFileNameBytes.Count());
		    // file name
		    meshesGeoDataStream.Write(meshFileNameBytes);
		    // size
		    meshesGeoDataStream.Write((short)meshesPack.Count);

		    // save meshes
		    foreach (MeshData meshData in meshesPack)
		    {
                // vertices
			    meshesGeoDataStream.Write((int)meshData.vertices.Length);
			    foreach (Vector3 vector in meshData.vertices)
			    {
				    meshesGeoDataStream.Write((float) (vector.X /*/ 100.0*/));
				    meshesGeoDataStream.Write((float) (vector.Y /*/ 100.0*/));
				    meshesGeoDataStream.Write((float) (vector.Z /*/ 100.0*/));
			    }

                // triangles
			    meshesGeoDataStream.Write((int)meshData.indices.Length * 3);
			    foreach (MeshFace meshFace in meshData.indices)
			    {
				    meshesGeoDataStream.Write((short)meshFace.v0);
				    meshesGeoDataStream.Write((short)meshFace.v1);
				    meshesGeoDataStream.Write((short)meshFace.v2);
			    }
                
                // only loading collidable meshes, so setting this to 1 (physical).
                // adding support for doors/skills/etc will require deciding on a real value to use here.
                byte dummyMaterialId = 1; // must be nonzero...
                meshesGeoDataStream.Write((byte)dummyMaterialId); // Material ID

                // CollisionIntention flags:
                //  0x1 = physical// Physical collision
                //  0x2 = material// Mesh materials with skills
                //  0x4 = skill// Skill obstacles
                //  0x8 = walk // Walk/NoWalk obstacles
                // 0x10 = door // Doors which have a state opened/closed
                // 0x20 = event // Appear on event only
                // 0x40 = moveable // Ships, shugo boxes
                
                //byte intention = 1; // physical ...
                meshesGeoDataStream.Write((byte)collisionIntention);


                //VERBOSE Console.WriteLine("          Vertices: " + meshData.vertices.Length
                //	    + " Indexes: " + (meshData.indices.Length * 3) + " = "
                //	    + meshData.indices.Length + " * 3");
            }
        }
        
        private void AddToMeshesGeo(string cgfPath, CgfLoader cgf, BinaryWriter meshesGeoDataStream,
            byte collisionIntention)
	    {
            Matrix identity = Matrix.Identity;
            
            // process each unique filename only once.
            if (m_loadedCgfs.Contains(cgfPath))
            {
                throw new InvalidOperationException("input should have been deduped already");
            }
            m_loadedCgfs.Add(cgfPath);

			try
			{
                // collect not empty, collidable meshes.
                var meshesPack = new List<MeshData>();
                cgf.TraverseNodes((node, m) => LoadMeshFromCgfCallback(node, m, cgf, meshesPack), ref identity);

                if (meshesPack.Count > 0)
                {
                    WriteToMeshsGeo(cgfPath, meshesPack, meshesGeoDataStream, collisionIntention);
                }
                else
                {
                    m_emptyCgfs.Add(cgfPath);
                }
			}
			catch (Exception e)
			{
				Console.WriteLine("***     ERROR: Cannot process mesh: " + e);
			}
	    }
        
        private void LoadMeshFromCgfCallback(NodeData node, Matrix m, CgfLoader cgf, List<MeshData> meshesPack)
        {
            if (node.Mesh.vertices.Length <= 0 || node.Mesh.indices.Length <= 0)
            {
                //VERBOSE Console.WriteLine("        Skipping (empty): " + node.Mesh.vertices.Length + "/" + node.Mesh.indices.Length);
            }
            /*else if ((node.Mesh.vertices.Length & 0xffff0000) != 0
                    || ((node.Mesh.indices.Length * 3) & 0xffff0000) != 0)
            {
                Console.WriteLine("***     ERROR: Count of elements is bigger than MAX_SHORT");
                Console.WriteLine("        Skipping: " + node.Mesh.vertices.Length + "/"
                        + node.Mesh.indices.Length * 3 + " [" + node.Mesh.indices.Length + " * 3]");
            }*/
            else if (!cgf.IsNodeCollidable(node))
            {
                //VERBOSE Console.WriteLine("        Skipping (not collidable): " + node.Mesh.vertices.Length + "/" + node.Mesh.indices.Length);
            }
            else
            {
                // transform the node into object coordinates. indices stay the same.
                var xformMesh = new MeshData();
                xformMesh.vertices = new Vector3[node.Mesh.vertices.Length];
                Vector3.Transform(node.Mesh.vertices, ref m, xformMesh.vertices);
                xformMesh.indices = node.Mesh.indices;

                meshesPack.Add(xformMesh);
            }
        }
        
        private WorldIdXmlLoader LoadWorldIdXml()
        {
            var dataWorldDir = new DirManager(Path.Combine(aionClientPath, @"Data\World"));
            using (var stream = new AionXmlStreamReader(dataWorldDir.OpenFile("WorldId.xml"), false))
                return new WorldIdXmlLoader(stream);
        }

        public void Process()
        {
            DateTime timer = DateTime.Now;

            if (!Directory.Exists(aionClientPath))
                throw new FileNotFoundException($"Aion client installation path [{aionClientPath}] doesn't exist or not a folder path");

            var meshesDir = new DirManager(aionClientPath,
                new[] { @"Levels\Common", @"objects\npc\event_object", @"objects\npc\level_object" });

            // Read world_maps.xml and WorldId.xml and find Levels to process
            Console.WriteLine("  Generating available levels list...");
            WorldIdXmlLoader worldIdXmlLoader = LoadWorldIdXml();
            Console.WriteLine("  Done.");

            var worldGeoBuilders = new List<WorldGeoFileBuilder>();

            var levelMeshData = new Dictionary<string, CgfMeshesToLoad>(); // key is level folder name

            Console.WriteLine("  Processing levels...");
            bool containsValidLevel = false;
            int curFolderIndex = -1;

            // Load levels dir... load all except common.
            var subdirs = Directory.EnumerateDirectories(Path.Combine(aionClientPath, "Levels"))
                .Select(fulldir => Path.GetFileName(fulldir))
                .Where(dir => !dir.Equals("common", StringComparison.InvariantCultureIgnoreCase));
            var levelsDir = new DirManager(Path.Combine(aionClientPath, "Levels"), subdirs);

            // special case to extract the login level...
            // login is like any other level, except it has no ID and isn't included in WorldId.xml.
            if (levelId == "login")
            {
                Console.WriteLine("*** Extracting login level ***");
                worldIdXmlLoader.FolderNamesById["login"] = "login";
            }

            if (generateGeo)
            {
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

                    Console.WriteLine($"    [{clientLevelId}] - ({curFolderIndex}/{worldIdXmlLoader.FolderNamesById.Count}) - {levelFolder} ...");

                    Console.WriteLine("      Parsing leveldata.xml ...");

                    if (!levelsDir.Exists(Path.Combine(levelFolder, "leveldata.xml")))
                    {
                        Console.WriteLine("        leveldata.xml not found, skipping level.");
                        continue;
                    }

                    // Note: this list is referenced later by index.
                    List<string> vegetationCgfFilenames;
                    Point mapSize;
                    using (var levelDataXml = levelsDir.OpenFile(Path.Combine(levelFolder, "leveldata.xml")))
                    {
                        var levelData = new LevelDataXmlLoader(levelDataXml);
                        vegetationCgfFilenames = levelData.VegetationCgfFilenames.Select(Util.NormalizeMeshFilename).ToList();
                        mapSize = levelData.MapWidthAndHeight;
                    }

                    var meshData = new CgfMeshesToLoad();

                    BrushLstLoader brushLst = null;
                    if (levelsDir.Exists(Path.Combine(levelFolder, "brush.lst")))
                    {
                        Console.WriteLine("      Parsing brush.lst ... ");
                        using (var stream = levelsDir.OpenFile(Path.Combine(levelFolder, "brush.lst")))
                            brushLst = new BrushLstLoader(stream);

                        // TODO - un-hardcode
                        if (brushLst.m_eventUsage[1])
                            Console.WriteLine("        * Supports event: X-Mas");
                        if (brushLst.m_eventUsage[2])
                            Console.WriteLine("        * Supports event: Halloween");
                        if (brushLst.m_eventUsage[3])
                            Console.WriteLine("        * Supports event: Brax Cafe");
                        if (brushLst.m_eventUsage[4])
                            Console.WriteLine("        * Supports event: Valentines");
                    }
                    else
                    {
                        Console.WriteLine("      brush.lst not found, skipping");
                    }


                    List<ObjectsLstItem> objectsLst = null;
                    if (levelsDir.Exists(Path.Combine(levelFolder, "objects.lst")))
                    {
                        Console.WriteLine("      Parsing objects.lst ... ");
                        using (var stream = levelsDir.OpenFile(Path.Combine(levelFolder, "objects.lst")))
                            objectsLst = ObjectsLstLoader.Load(stream, mapSize.X, mapSize.Y);
                    }
                    else
                    {
                        Console.WriteLine("      objects.lst not found, skipping");
                    }

                    // ------------------------------
                    meshData.meshFiles = new List<string>();

                    // brushes
                    if (brushLst != null)
                        meshData.meshFiles.AddRange(brushLst.brushInfoList.Select(o => o.filename));

                    // vegetation
                    meshData.meshFiles.AddRange(vegetationCgfFilenames);

                    // normalize names and dedupe. example entry: "levels/common/dark/natural/rocks/base/na_d_rockgngrass_05a.cgf"
                    meshData.meshFiles = meshData.meshFiles.Select(Util.NormalizeMeshFilename).Distinct().ToList();

                    meshData.meshUsage = new int[meshData.meshFiles.Count];
                    if (!noMesh)
                    {
                        levelMeshData.Add(levelFolder, meshData);
                    }

                    byte[] landMapH32 = null;

                    if (levelsDir.Exists(Path.Combine(levelFolder, @"terrain\land_map.h32")))
                    {
                        using (var stream = levelsDir.OpenFile(Path.Combine(levelFolder, @"terrain\land_map.h32")))
                        {
                            using (var ms = new MemoryStream())
                            {
                                stream.CopyTo(ms);
                                landMapH32 = ms.ToArray();
                            }
                        }
                    }

                    // keep track of all required cgfs
                    if (brushLst != null)
                        m_requiredCgfs.UnionWith(brushLst.brushInfoList.Select(o => Util.NormalizeMeshFilename(o.filename)));
                    if (objectsLst != null)
                        m_requiredCgfs.UnionWith(vegetationCgfFilenames.Select(Util.NormalizeMeshFilename));

                    // level data must be loaded first to find the required cgfs. then, cgfs must be loaded to determine if 
                    // they contain collision data or not. then we can write the geo file minus non-collidable meshes.
                    var w = new WorldGeoFileBuilder(clientLevelId, landMapH32, brushLst, vegetationCgfFilenames, objectsLst);

                    // these will be processed after meshs
                    worldGeoBuilders.Add(w);
                
                    containsValidLevel = true;

                    Console.WriteLine("    Done.");
                }
                Console.WriteLine("  Done.");

                // --------------------------------------------------------------------------------
                if (!noMesh && containsValidLevel)
                {
                    Console.WriteLine("  Generating meshs.geo ...");

                    int meshesSaved = 0;
                    string meshesGeoFile = Path.Combine(outputPath, "meshs.geo");
                    using (var meshesGeoDataStream = new BinaryWriter(File.Open(meshesGeoFile, FileMode.Create)))
                    {
                        foreach (string s in m_requiredCgfs)
                        {
                            string cgfPath = PakUtil.NormalizeFilename(s);
                            if (!meshesDir.Exists(cgfPath))
                            {
                                Console.WriteLine("    Cgf not found: " + cgfPath);
                                continue;
                            }
                            using (var cgfStream = meshesDir.OpenFile(cgfPath))
                                AddToMeshesGeo(cgfPath, new CgfLoader(cgfStream), meshesGeoDataStream,
                                    1/*collisionIntention=physical*/);
                            meshesSaved++;
                        }
                    }

                    Console.WriteLine("  Done. " + meshesSaved + "/" + m_requiredCgfs.Count + " meshes saved.");

                    // -----------------------------------
                    Console.WriteLine("  Writing world.geo files ...");
                    int wc = 0;
                    foreach (var w in worldGeoBuilders)
                    {
                        wc++;
                        Console.Write($"      Creating {w.ClientLevelId}.geo file [{wc}/{worldGeoBuilders.Count}]... ");

                        w.CreateWorldGeoFile(outputPath, noH32, m_loadedCgfs, m_emptyCgfs);
                        Console.WriteLine("   Done.");
                    }
                    Console.WriteLine("  Done.");
                }

                // ------------------------------------
                /*VERBOSE Console.WriteLine("  Check meshes that were not found ...");
			    foreach (var levelMeshes in levelMeshData)
			    {
				    Console.WriteLine("    " + levelMeshes.Key);
				    CgfMeshesToLoad brushLstMeshData = levelMeshes.Value;
				    for (int i = 0; i < brushLstMeshData.meshFiles.Count; i++)
				    {
					    if (brushLstMeshData.meshUsage[i] == 0 && !m_emptyCgfs.Contains(Util.NormalizeMeshFilename(brushLstMeshData.meshFiles[i])))
					    {
						    Console.WriteLine("      " + brushLstMeshData.meshFiles[i]);
					    }
				    }
			    }*/
                Console.WriteLine("  Done.");
            }

            // --------------------------------------------------------------------------------
            if (generateDoors)
            {
                Console.WriteLine("Generating door mesh data...");

                // Writes 2 files:
                // - door data, pairing level+entityid to position
                // - door mesh file, containing start and end variations of each door model.

                // Original AL stores models in data\geo\model\static_doors as .cga files.
                // These are not actually .cga files, but instead are the same format as .geo files.
                // These meshes only contained a single state.
                //
                // This following code generates door data in a new format:
                // - Each door model is loaded twice, in the start and end positions.
                // - Each model has _start or _end appended to the mesh name.
                // - All unique door meshes are stored in doors.geo, following the same format as
                //   meshes.geo. Meshes should be identified by original name + _start or _end.
                // - All door instances are stored in doors.dat and define world placement.
                //   static_doors.xml is still necessary as it provides unique info such as key item id.
                
                var doorModelNames = new HashSet<string>();

                using (var doorsDat = new BinaryWriter(File.Open(Path.Combine(outputPath, "doors.dat"), FileMode.Create)))
                {
                    doorsDat.Write(0x524F4F44);

                    // collect unique doors
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

                        Console.WriteLine($"    [{clientLevelId}] - ({curFolderIndex}/{worldIdXmlLoader.FolderNamesById.Count}) - {levelFolder} ...");
                        
                        List<DoorInfo> doorInfos;
                        try
                        {
                            doorInfos = DoorLoader.LoadDoorInfosForLevel(levelsDir, levelFolder);
                        }
                        catch
                        {
                            Console.WriteLine("        Level folder not found.");
                            continue;
                        }

                        foreach (var door in doorInfos)
                        {
                            string meshName = PakUtil.NormalizeFilename(door.object_AnimatedModel);
                            doorModelNames.Add(meshName);

                            doorsDat.Write((short)meshName.Length);
                            byte[] meshFileNameBytes = Encoding.ASCII.GetBytes(meshName);
                            doorsDat.Write(meshFileNameBytes);
                            doorsDat.Write(int.Parse(clientLevelId));
                            doorsDat.Write(door.EntityId);
                            doorsDat.Write(door.Pos.X);
                            doorsDat.Write(door.Pos.Y);
                            doorsDat.Write(door.Pos.Z);
                            // TODO investigate dir / use_dir. value correlates to z angle...
                            doorsDat.Write(door.Angles.X);
                            doorsDat.Write(door.Angles.Y);
                            doorsDat.Write(door.Angles.Z);
                        }
                    }
                }
                
                m_loadedCgfs = new HashSet<string>();
                m_emptyCgfs = new HashSet<string>();

                // generate door meshes.
                // a door model may be referenced many times, so models are saved separately from instances.
                int meshesSaved = 0;
                string doorsGeoFile = Path.Combine(outputPath, "doors.geo");
                using (var doorMeshesGeoDataStream = new BinaryWriter(File.Open(doorsGeoFile, FileMode.Create)))
                {
                    foreach (var cgfPath in doorModelNames.OrderBy(o => o))
                    {
                        if (!meshesDir.Exists(cgfPath))
                        {
                            Console.WriteLine("    Door Cgf/Cga not found: " + cgfPath);
                            continue;
                        }

                        // TODO - parameterize m_loadedCgfs and m_emptyCgfs...
                        using (var cgfStream = meshesDir.OpenFile(cgfPath))
                        {
                            var cgfFirstState = new CgfLoader(cgfStream);
                            var cgfSecondState = cgfFirstState.CloneAtTime(999999);
                                
                            byte collisionIntention = 1 + (1<<4); // physical + door
                            AddToMeshesGeo(cgfPath + "_start", cgfFirstState, doorMeshesGeoDataStream, collisionIntention);
                            AddToMeshesGeo(cgfPath + "_end", cgfSecondState, doorMeshesGeoDataStream, collisionIntention);
                        }
                        meshesSaved++;
                    }
                }

                // doors should have collision data, otherwise they won't be very effective.
                foreach (var empty in m_emptyCgfs)
                {
                    Console.WriteLine("Warning: door has no collision data: " + empty);
                }
                Console.WriteLine("Door meshes: " + meshesSaved);
            }

            // --------------------------------------------------------------------------------
            if (generateNav)
            {
                Console.WriteLine("  Generating NavMesh .nav data...");
                var start = DateTime.Now;
                NavMeshProcessor.GenerateAllNav(worldIdXmlLoader, meshesDir, levelsDir,
                    levelId, skipExistingNav, outputPath);
                Console.WriteLine("    NavMesh processing time: " + (DateTime.Now - start));
            }

            TimeSpan timerEnd = DateTime.Now - timer;
		    Console.WriteLine("  Processing time: " + timerEnd);
	    }
    }
}
