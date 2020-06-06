using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using monono2.Common.FileFormats.Pak;

namespace monono2.Common
{
    public sealed class DirManager : IDisposable
    {
        private string m_rootPath;
        private SortedDictionary<string, PakReader> m_fileListing = new SortedDictionary<string, PakReader>();
        public DirManager(string rootPath)
        {
            SetRootPath(rootPath);
            LoadAll();
        }

        // load only subdirs under root. paths are still relative to rootPath.
        // subdirs must be physical/local dirs, not dirs inside pak.
        public DirManager(string rootPath, IEnumerable<string> subdirs)
        {
            SetRootPath(rootPath);
            foreach (var dir in subdirs)
                LoadSubDir(dir);
        }

        private void SetRootPath(string rootPath)
        {
            m_rootPath = rootPath.ToLower(new CultureInfo("en-US", false));
            if (!Directory.Exists(m_rootPath))
                throw new DirectoryNotFoundException();
        }

        public void DebugPrintFileListing(string matchPrefix = null, 
            string matchAny = null, string matchSuffix = null)
        {
            foreach (var kvp in m_fileListing)
            {
                if (!string.IsNullOrEmpty(matchPrefix) && !kvp.Key.StartsWith(matchPrefix, StringComparison.InvariantCultureIgnoreCase))
                    continue;
                if (!string.IsNullOrEmpty(matchAny) && kvp.Key.IndexOf(matchAny, StringComparison.InvariantCultureIgnoreCase) == -1)
                    continue;
                if (!string.IsNullOrEmpty(matchSuffix) && !kvp.Key.EndsWith(matchSuffix, StringComparison.InvariantCultureIgnoreCase))
                    continue;
                Log.Write(kvp.Value == null ? "LOCAL " : " PAK  ");
                Log.WriteLine(kvp.Key);
            }
        }

        public bool Exists(string path)
        {
            return m_fileListing.ContainsKey(NormalizePath(path));
        }

        public Stream OpenFile(string relativePath)
        {
            var path = NormalizePath(relativePath);
            var pr = m_fileListing[path];
            if (pr == null)
            {
                // local file
                return File.OpenRead(GetFullLocalPath(relativePath));
            }

            return new MemoryStream(pr.GetFile(MakePathRelativeToPak(path, pr)));
        }

        private string MakePathRelativeToPak(string relativePath, PakReader pr)
        {
            string relativeToPak = Path.GetDirectoryName(pr.OriginalPakPath).Substring(m_rootPath.Length);
            return relativePath.Substring(relativeToPak.Length).TrimStart(new[] { '\\', '/' });
        }

        // Load all subdirectories.
        // Do not use with LoadSubDir.
        private void LoadAll()
        {
            GenerateFileListing("");
        }

        // Load a specific subdirectory under the root.
        // Do not load the same directory twice!
        private void LoadSubDir(string subdir)
        {
            GenerateFileListing(subdir);
        }

        // Rules:
        // - Filenames are lowercase and use backslash.
        // - Filenames include directories and are relative to the root path.
        // - .pak files are excluded from the listing since they get expanded.
        // - Files contained in .pak files take precedence over local files.
        // - The filemap maps relative paths to a PakReader. If PakReader is null, path is a local file.
        private SortedDictionary<string, PakReader> GenerateFileListing(string subpath)
        {
            var paksToLoad = new List<string>();

            // load pak names and local files
            foreach (var path in Directory.EnumerateFiles(Path.Combine(m_rootPath, subpath), "*", SearchOption.AllDirectories))
            {
                if (Path.GetExtension(path).Equals(".pak", StringComparison.OrdinalIgnoreCase))
                {
                    paksToLoad.Add(path);
                    continue;
                }

                m_fileListing.Add(GetRelativePath(path), null);
            }

            // load filenames from pak, and map each to a PakReader.
            foreach (var pak in paksToLoad)
            {
                var pr = new PakReader(pak);
                foreach (var file in pr.Files.Keys)
                {
                    string relativePath = GetRelativePath(Path.Combine(Path.GetDirectoryName(pak), file));
                    PakReader existing;
                    if (m_fileListing.TryGetValue(relativePath, out existing))
                    {
                        if (existing == null)
                        {
                            m_fileListing[relativePath] = pr;
                        }
                        // TODO aion gamedata has colliding names... (especially in levels/common)
                        // how should that be handled? could check if models are the same. just ignoring for now... 
                        //else
                            //Log.WriteLine("duplicate filename encountered: " + relativePath);
                            //throw new InvalidOperationException("duplicate filename encountered: " + relativePath);
                    }
                    else
                    {
                        m_fileListing.Add(relativePath, pr);
                    }
                }
            }

            return m_fileListing;
        }

        private string NormalizePath(string path)
        {
            return path.ToLower(new CultureInfo("en-US", false)).Replace('/', '\\');
        }

        private string GetRelativePath(string path)
        {
            path = NormalizePath(path);
            if (!path.StartsWith(m_rootPath))
                throw new InvalidOperationException("root path doesn't match filename!");
            return path.Substring(m_rootPath.Length).TrimStart(new[] { '\\' });
        }

        private string GetFullLocalPath(string relativePath)
        {
            return Path.Combine(m_rootPath, relativePath);
        }
        
        // DirManager must be kept open while reading files.
        public void Close()
        {
            if (m_fileListing != null)
            {
                foreach (var pr in m_fileListing.Values.Distinct().Where(o => o != null))
                    pr.Close();
                m_fileListing.Clear();
                m_fileListing = null;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
