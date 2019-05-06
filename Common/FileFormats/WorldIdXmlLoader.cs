using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace monono2.Common
{
    public class WorldIdEntry
    {
        public string Id;
        public string FolderName;
    }

    public class WorldIdXmlLoader
    {
        public Dictionary<string, string> FolderNamesById;
        private Dictionary<string, string> IdsByFolderName;

        public WorldIdXmlLoader(Stream stream)
        {
            Load(XDocument.Load(stream));
        }

        public WorldIdXmlLoader(string path)
        {
            // read client maps
            Load(XDocument.Load(path));
        }

        private void Load(XDocument xdoc)
        {
            // read client maps
            var list = xdoc.Root.Elements("data");

            var hash = new HashSet<string>();
            FolderNamesById = new Dictionary<string, string>();
            foreach (var node in list)
            {
                // validate
                if (string.IsNullOrWhiteSpace(node.Value))
                    throw new InvalidOperationException("worldid.xml contains data entry with empty level name");
                if (hash.Contains(node.Value))
                    throw new InvalidOperationException("worldid.xml contains duplicate level: " + node.Value);
                hash.Add(node.Value);
                // end validate

                FolderNamesById[node.Attribute("id").Value] = node.Value;
            }

            IdsByFolderName = new Dictionary<string, string>();
            foreach (var kvp in FolderNamesById)
            {
                IdsByFolderName.Add(kvp.Value.ToLowerInvariant(), kvp.Key);
            }
        }

        public string GetLevelId(string folderName)
        {
            return IdsByFolderName[folderName];
        }
    }
}
