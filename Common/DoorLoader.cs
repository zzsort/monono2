using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace monono2.Common
{
    public class DoorInfo
    {
        public int EntityId;
        public string Name;
        public Vector3 Pos;
        public Vector3 Angles; // rotation
        public string object_AnimatedModel; // cga filename
        public bool bClickable;
        public bool bCloseable;
        public bool bOneWay;
        public bool bOpened;

        public Matrix GetMatrix()
        {
            return Matrix.CreateFromYawPitchRoll(
                    MathHelper.ToRadians(Angles.X), 
                    MathHelper.ToRadians(Angles.Y), 
                    MathHelper.ToRadians(Angles.Z))
                * Matrix.CreateTranslation(Pos);
        }
    }
    
    public static class DoorLoader
    {
        // if levelFolder is empty, levelDir points to a single level directory.
        // otherwise, levelDir points to the levels\ directory, and levelFolder chooses a specific level directory.
        public static List<DoorInfo> LoadDoorInfosForLevel(DirManager levelDir, string levelFolder = "")
        {
            var result = new List<DoorInfo>();
            using (var stream = new AionXmlStreamReader(levelDir.OpenFile(Path.Combine(levelFolder, "mission_mission0.xml")), false))
            {
                var xdoc = XDocument.Load(stream);
                foreach (var e in xdoc.Root.Element("Objects").Elements("Entity"))
                {
                    if (e.Attribute("EntityClass").Value != "Door")
                    {
                        continue;
                    }

                    var doorInfo = new DoorInfo();
                    doorInfo.EntityId = int.Parse(e.Attribute("EntityId").Value);
                    doorInfo.Name = e.Attribute("Name").Value;

                    // TODO "dir" is a value in degrees which matches "angles"... 
                    // do these always represent the same rotation? check broken door models... add check...
                    // TODO are x and y in angles always zero?
                    doorInfo.Angles = e.Attribute("Angles") != null ? Util.ParseVector(e.Attribute("Angles").Value) : Vector3.Zero;
                    doorInfo.Pos = Util.ParseVector(e.Attribute("Pos").Value);

                    var properties = e.Element("Properties");
                    doorInfo.object_AnimatedModel = properties.Attribute("object_AnimatedModel").Value;

                    var server = properties.Element("Server");
                    doorInfo.bClickable = server.Attribute("bClickable").Value == "1";
                    doorInfo.bCloseable = server.Attribute("bCloseable").Value == "1";
                    doorInfo.bOneWay = server.Attribute("bOneWay").Value == "1";
                    doorInfo.bOpened = server.Attribute("bOpened").Value == "1";

                    result.Add(doorInfo);
                }
            }
            return result;
        }

        public static void ValidateDoorModels(DirManager meshesDir, string cgaPath)
        {
            // check assumptions:
            // TODO - file should exist
            // TODO - should have .cga extension
            // TODO - check for existence of .anm or .cal or .caf files?
            // TODO - cga should be valid
            // TODO - check if any scale or rotation controllers have non default values.
            // TODO - check position controllers, assume default values, or count = 2
        }
    }
}
