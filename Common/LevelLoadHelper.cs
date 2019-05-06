using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace monono2.Common
{
    public static class LevelLoadHelper
    {
        public static BrushLstLoader CreateBrushLstLoader(DirManager levelDir, string relPath)
        {
            BrushLstLoader brushlst = null;
            if (levelDir.Exists(relPath))
            {
                using (var brushLstStream = levelDir.OpenFile(relPath))
                    brushlst = new BrushLstLoader(brushLstStream);
            }
            return brushlst;
        }

        public static Dictionary<int, CgfLoader> CreateBrushLstCgfLoaderMap(
            DirManager meshesDir, BrushLstLoader brushlst)
        {
            var cgfMap = new Dictionary<int, CgfLoader>();
            foreach (var brushInfo in brushlst.brushInfoList)
            {
                if (!meshesDir.Exists(brushInfo.filename))
                {
                    Log.WriteLine("**Model not found: " + brushInfo.filename);
                    continue;
                }

                using (var cgfStream = meshesDir.OpenFile(brushInfo.filename))
                {
                    var c = new CgfLoader(cgfStream);
                    cgfMap.Add(brushInfo.brushInfoIndex, c);
                }
            }
            return cgfMap;
        }

        public class ObjectsLstContext
        {
            public Dictionary<int, CgfLoader> cgfMap;
            public List<ObjectsLstItem> objects;
        }

        public static ObjectsLstContext LoadObjectsLst(DirManager meshesDir,
            DirManager levelDir, string levelRoot = "")
        {
            if (!levelDir.Exists(Path.Combine(levelRoot, "objects.lst")))
                return null;

            LevelDataXmlLoader levelDataXml;
            using (var levelDataStream = levelDir.OpenFile(Path.Combine(levelRoot, "leveldata.xml")))
                levelDataXml = new LevelDataXmlLoader(levelDataStream);

            var result = new ObjectsLstContext();
            result.cgfMap = new Dictionary<int, CgfLoader>();
            for (int i = 0; i < levelDataXml.VegetationCgfFilenames.Count; i++)
            {
                if (!meshesDir.Exists(levelDataXml.VegetationCgfFilenames[i]))
                    continue;

                using (var cgfStream = meshesDir.OpenFile(levelDataXml.VegetationCgfFilenames[i]))
                {
                    result.cgfMap[i] = new CgfLoader(cgfStream);
                }
            }
            
            using (var objectLstStream = levelDir.OpenFile(Path.Combine(levelRoot, "objects.lst")))
                result.objects = ObjectsLstLoader.Load(objectLstStream, 
                    levelDataXml.MapWidthAndHeight.X, levelDataXml.MapWidthAndHeight.Y);

            return result;
        }

        // get the world transform for a vegetation object
        public static Matrix GetObjectMatrix(ObjectsLstItem o)
        {
            return 
                Matrix.CreateScale(o.Scale) *
                Matrix.CreateRotationZ(MathHelper.ToRadians(-o.Heading)) *
                Matrix.CreateTranslation(o.Position);
        }

        // get the world transform for a brush
        public static Matrix GetBrushMatrix(BrushEntry brush)
        {
            Matrix m = brush.rotationMatrix; // copy
            Util.FlipMatrixDiagonal3x3(ref m);
            m *= Matrix.CreateTranslation(brush.position);
            return m;
        }

    }
}
