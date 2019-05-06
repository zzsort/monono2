using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ionic.Zip;

namespace monono2.ALGeoBuilder.Utils
{
    public static class ZipUtils
    {
        public static void zipDirectory(string directory, string zipfile)
        {
            using (ZipOutputStream zos = new ZipOutputStream(zipfile))
            {
                zip(directory, directory, zos);
            }
	    }

        private static void zip(string directory, string basefile, ZipOutputStream zos)
        {
            var files = Directory.EnumerateFiles(directory);
		    byte[] buffer = new byte[8192];
		    foreach (var file in files) {
			    if (Directory.Exists(file)) {
				    zip(file, basefile, zos);
                } else {
                    using (var instream = File.OpenRead(file))
                    {
                        zos.PutNextEntry(file.Substring(basefile.Length + 1));
                        while (true)
                        {
                            int read = instream.Read(buffer, 0, buffer.Length);
                            if (read <= 0)
                                break;
                            zos.Write(buffer, 0, read);
                        }
                    }
			    }
		    }
	    }

	    public static void unzip(string zipFile, string extractTo)
        {
            using (ZipFile archive = new ZipFile(zipFile))
            {
                foreach (var entry in archive.Entries)
                {
                    string file = Path.Combine(extractTo, entry.FileName);

                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    if (!entry.IsDirectory || File.Exists(file))
                    {
                        using (var instream = new BinaryReader(entry.InputStream))
                            using (var outstream = new BinaryWriter(File.Open(file, FileMode.Create)))
                            {
                                instream.BaseStream.CopyTo(outstream.BaseStream);
                            }
                    }
                }
            }
	    }

	    public static void unzipEntry(string zipFile, string filter, Stream outputStream)
        {
            using (ZipFile archive = new ZipFile(zipFile))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) // search only for file
                        continue;

                    String entryName = entry.FileName;
                    if (!Regex.IsMatch(entryName, filter))
                        continue;

                    using (var instream = new BinaryReader(entry.InputStream))
                        using (var outstream = new BinaryWriter(outputStream))
                            instream.BaseStream.CopyTo(outstream.BaseStream);

                    return;
                }
                throw new FileNotFoundException("There is no file in zip archive that match mask: " + filter);
            }
	    }

	    public static void unzipEntry(string zipFile, Dictionary<string, Stream> filterStreamMap, IAionStringComparer comparer)
        {
            using (ZipFile archive = new ZipFile(zipFile))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory) // search only for file
                        continue;

                    String entryName = entry.FileName;
                    String filter = null;
                    Stream outputStream = null;
                    foreach (var filterStream in filterStreamMap)
                    {
                        if (comparer.compare(entryName, filterStream.Key))
                        {
                            filter = filterStream.Key;
                            outputStream = filterStream.Value;
                            break;
                        }
                    }
                    if (filter == null)
                        continue;

                    filterStreamMap.Remove(filter);

                    
                    using (var instream = new BinaryReader(entry.OpenReader()))
                        using (var outstream = new BinaryWriter(outputStream))
                        {
                            instream.BaseStream.CopyTo(outstream.BaseStream);
                        }

                    if (filterStreamMap.Count == 0)
                        break;
                }
                
                if (filterStreamMap.Count != 0)
                    throw new FileNotFoundException("There are no files in zip archive that match masks: " + string.Join(",", filterStreamMap.Keys));
            }
	    }
    }
}
