using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xna.Framework;

namespace monono2.Common
{
    public class LevelDataXmlLoader
    {
        public List<string> VegetationCgfFilenames { get; private set; }
        public float WaterLevel { get; private set; }
        public Point MapWidthAndHeight { get; private set; }

        public LevelDataXmlLoader(string filename)
        {
            using (var fs = File.OpenRead(filename))
            {
                Load(fs);
            }
        }

        public LevelDataXmlLoader(Stream levelDataXml)
        {
            Load(levelDataXml);
        }

        private void Load(Stream levelDataXml)
        {
            var xdoc = XDocument.Load(levelDataXml);
            LoadVegetationCgfFilenames(xdoc);
            LoadWaterLevel(xdoc);
            LoadMapWidthAndHeight(xdoc);
        }
        
        private void LoadVegetationCgfFilenames(XDocument xdoc)
        {
            VegetationCgfFilenames = new List<string>();
            foreach (var e in xdoc.Root.Element("Vegetation").Elements())
            {
                VegetationCgfFilenames.Add(e.Attribute("FileName").Value);
            }
        }

        private void LoadWaterLevel(XDocument xdoc)
        {
            WaterLevel = float.Parse(xdoc.Root.Element("LevelInfo").Attribute("WaterLevel").Value, CultureInfo.InvariantCulture);
        }
        
        private void LoadMapWidthAndHeight(XDocument xdoc)
        {
            int w = int.Parse(xdoc.Root.Element("LevelInfo").Attribute("HeightmapXSize").Value);
            int h = int.Parse(xdoc.Root.Element("LevelInfo").Attribute("HeightmapYSize").Value);
            MapWidthAndHeight = new Point(w, h);
        }
    }
}
