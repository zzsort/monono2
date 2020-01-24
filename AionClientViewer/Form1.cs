using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using monono2.Common;
using monono2.Common.EncryptedHtml;
using monono2.Common.FileFormats.Pak;

namespace monono2.AionClientViewer
{
    public partial class Form1 : Form
    {
        private TreeView m_treeView;
        private AionLevelViewerControl m_vc;
        private ImageViewerControl m_imageViewer;
        private TextBox m_textViewer;
        private string m_clientRoot;
        private MenuStrip m_menuStrip;

        public Form1()
        {
            InitializeComponent();

            m_menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("File");
            var fileOpenItem = new ToolStripMenuItem("Open Aion client directory...");
            fileOpenItem.Click += FileOpenItem_Click;
            fileMenu.DropDownItems.Add(fileOpenItem);
            m_menuStrip.Items.Add(fileMenu);
            Controls.Add(m_menuStrip);

            m_treeView = new TreeView();
            m_treeView.Location = new Point(0, m_menuStrip.Bottom);
            m_treeView.Size = new Size(300, 800);
            m_treeView.ImageList = new ImageList();
            m_treeView.ImageList.Images.Add(Properties.Resources.DefaultImage);//0
            m_treeView.ImageList.Images.Add(Properties.Resources.DirImage);//1
            m_treeView.ImageList.Images.Add(Properties.Resources.PakImage);//2
            m_treeView.ImageList.Images.Add(Properties.Resources.TextImage);//3
            m_treeView.ImageList.Images.Add(Properties.Resources.ImageImage);//4
            m_treeView.ImageList.Images.Add(Properties.Resources.LevelImage);//5
            m_treeView.HideSelection = false;

            m_treeView.AfterSelect += M_treeView_AfterSelect;
            m_treeView.BeforeExpand += M_treeView_BeforeExpand;
            m_treeView.NodeMouseDoubleClick += M_treeView_NodeMouseDoubleClick;
            m_treeView.KeyPress += M_treeView_KeyPress;

            m_treeView.ItemDrag += M_treeView_ItemDrag;

            Controls.Add(m_treeView);

            m_imageViewer = new ImageViewerControl();
            m_imageViewer.Hide();
            Controls.Add(m_imageViewer);
            
            m_textViewer = new TextBox();
            m_textViewer.Font = new Font(FontFamily.GenericMonospace, 10);
            m_textViewer.Multiline = true;
            m_textViewer.ScrollBars = ScrollBars.Both;
            m_textViewer.Hide();
            Controls.Add(m_textViewer);

            DoResize();
            AllowDrop = true;

            var dir = ClientViewerSettings.GetClientDir();
            if (dir == null)
            {
                dir = GetClientDirFromOpenDialog();
            }
            LoadClientDir(dir);
        }

        private void FileOpenItem_Click(object sender, EventArgs e)
        {
            LoadClientDir(GetClientDirFromOpenDialog());
        }

        private string GetClientDirFromOpenDialog()
        {
            var dlg = new FolderSelect.FolderSelectDialog();
            if (!dlg.ShowDialog())
                return null;
            return dlg.FileName;
        }

        private void LoadClientDir(string dir)
        {
            if (dir == null)
                return;
            m_treeView.Nodes.Clear();
            try
            {
                LoadDirectoryStructure(dir);
                LoadLevels();
                m_treeView.SelectedNode = m_treeView.Nodes[0];
                try
                {
                    ClientViewerSettings.SetClientDir(dir);
                }
                catch { }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error loading dir: " + e.Message);
            }
        }

        protected override void OnDragEnter(DragEventArgs drgevent)
        {
            if (drgevent.Data.GetFormats().Contains("FileNameW"))
                drgevent.Effect = DragDropEffects.Copy;
            base.OnDragEnter(drgevent);
        }
        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            var filename = ((string[])drgevent.Data.GetData("FileNameW"))[0];
            PreviewFileFromFileInfo(new FileInfo(filename));
            base.OnDragDrop(drgevent);
        }

        private void M_treeView_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                TryOpenLevelViewerForSelectedNode();
                e.Handled = true;
            }
        }

        private void M_treeView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var node = e.Item as TreeNode;
            if (node == null)
                return;
            
            // TODO - drag start immediately copies the file, even if dragging within the app. should only copy if needed.
            // TODO - drag directory

            List<string> selection = new List<string>();

            string tempfile = null;
            string tempdir = null;
            var file = node.Tag as FileInfo;
            if (file != null)
            {
                // just copy the file
                selection.Add(file.FullName);
            }
            else
            {
                var pei = node.Tag as PakEntryInfo;
                if (pei != null)
                {
                    if (string.IsNullOrWhiteSpace(pei.EntryFilename) || pei.EntryFilename.EndsWith("\\") ||
                        pei.EntryFilename.EndsWith("/"))
                        return;

                    tempdir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    tempfile = Path.Combine(tempdir, pei.EntryFilename.Replace("/", "\\"));
                    tempdir = Path.GetDirectoryName(tempfile);

                    Directory.CreateDirectory(tempdir);

                    var pr = new PakReader(pei.PakFile);
                    File.WriteAllBytes(tempfile, pr.GetFile(pei.EntryFilename));
                    selection.Add(tempfile);
                }
            }
            
            if (!selection.Any())
                return;

            DataObject data = new DataObject(DataFormats.FileDrop, selection.ToArray());
            DoDragDrop(data, DragDropEffects.Copy);
            
            if (tempfile != null)
            {
                try
                {
                    File.Delete(tempfile);
                    Directory.Delete(tempdir);
                }
                catch { }
            }
        }

        private class PakEntryInfo
        {
            public string PakFile;
            public string EntryFilename;
        }

        private void M_treeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node == null)
                return;
            PopulatePakTreeNode(e.Node);
        }

        private void PopulatePakTreeNode(TreeNode node)
        {
            var file = node.Tag as FileInfo;
            if (file == null)
                return;

            var ext = Path.GetExtension(file.Name);
            if (ext.Equals(".pak", StringComparison.InvariantCultureIgnoreCase))
            {
                if (node.Nodes.Count == 1 && node.Nodes[0].Text == "Loading...")
                {
                    node.Nodes.Clear();

                    using (var pr = new PakReader(node.FullPath))
                    {
                        var filenames = pr.Files.Keys.OrderBy(o => o);

                        // collect directories
                        var subdirs = new List<string>();
                        foreach (var pakFilename in filenames)
                        {
                            var lastPathSep = pakFilename.LastIndexOf('/');
                            if (lastPathSep == -1)
                                continue;
                            subdirs.Add(pakFilename.Substring(0, lastPathSep));
                        }

                        // build the directory structure
                        var dirs = new Dictionary<string, TreeNode>();
                        foreach (var s in subdirs.Distinct().OrderBy(o => o))
                        {
                            var curNode = node;
                            string curFullDirName = "";
                            foreach (var dirName in s.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                curFullDirName += (curFullDirName == "" ?  "" : "/") + dirName;
                                if (curNode.Nodes.ContainsKey(dirName))
                                {
                                    curNode = curNode.Nodes[dirName];
                                    continue;
                                }
                                var dirNode = new TreeNode(dirName);
                                dirNode.Name = dirName;
                                dirNode.ImageIndex = 1;
                                dirNode.SelectedImageIndex = 1;
                                curNode.Nodes.Add(dirNode);
                                curNode = dirNode;

                                dirs.Add(curFullDirName, dirNode);
                            }
                        }

                        // insert the files
                        foreach (var f in filenames)
                        {
                            TreeNode dir;
                            var fileNode = new TreeNode();
                            var lastSlash = f.LastIndexOf('/');
                            if (lastSlash == -1)
                            {
                                dir = node;
                                fileNode.Text = f;
                            }
                            else
                            {
                                dir = dirs[f.Substring(0, lastSlash)];
                                fileNode.Text = f.Substring(lastSlash + 1);
                            }
                            SetNodeIconFromFilename(fileNode, f);
                            fileNode.Tag = new PakEntryInfo { PakFile = file.FullName, EntryFilename = f };
                            dir.Nodes.Add(fileNode);
                        }
                    }
                }
            }
        }

        private void M_treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var node = m_treeView.SelectedNode;
            if (node == null)
                return;
            var file = node.Tag as FileInfo;
            if (file != null)
                PreviewFileFromFileInfo(file);
            
            var fileInPak = node.Tag as PakEntryInfo;
            if (fileInPak != null)
                PreviewFileFromPakEntryInfo(fileInPak);
        }

        private static string[] s_extText = new[] { ".txt", ".log", ".ini", ".lua", ".toc", ".ext", ".vcxproj",
            ".filters", ".cmd", ".bat", ".cal", ".csv", ".nfo" };
        private static string[] s_extHtml = new[] { ".html", ".htm" };
        private static string[] s_extXml = new[] { ".xml" };
        private static string[] s_extImage = new[] { ".jpg", ".png", ".bmp", ".tmb", ".ico", ".dds",
            ".gif", ".tif", ".tga", ".pcx" };
        private static string[] s_extCgf = new[] { ".cgf", ".cga" };

        public enum PreviewType { None, Pak, Text, Html, Xml, Image, Cgf }
        public PreviewType GetPreviewTypeFromFilename(string filename)
        {
            string ext = Path.GetExtension(filename);
            if (ext.Equals(".pak", StringComparison.InvariantCultureIgnoreCase))
                return PreviewType.Pak;
            if (s_extText.Contains(ext, StringComparer.InvariantCultureIgnoreCase))
                return PreviewType.Text;
            if (s_extHtml.Contains(ext, StringComparer.InvariantCultureIgnoreCase))
                return PreviewType.Html;
            if (s_extXml.Contains(ext, StringComparer.InvariantCultureIgnoreCase))
                return PreviewType.Xml;
            if (s_extImage.Contains(ext, StringComparer.InvariantCultureIgnoreCase))
                return PreviewType.Image;
            if (s_extCgf.Contains(ext, StringComparer.InvariantCultureIgnoreCase))
                return PreviewType.Cgf;
            return PreviewType.None;
        }

        private void PreviewFileFromFileInfo(FileInfo file)
        {
            switch (GetPreviewTypeFromFilename(file.Name))
            {
                case PreviewType.Pak:
                    {
                        var text = GetPakFileListing(file.FullName);
                        ShowTextViewer(text);
                    }
                    break;
                case PreviewType.Text:
                    {
                        var text = File.ReadAllText(file.FullName);
                        ShowTextViewer(text);
                    }
                    break;
                case PreviewType.Html:
                    {
                        using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                        {
                            var text = EncryptedHtml.DecodeToString(file.Name, fs);
                            ShowTextViewer(text);
                        }
                    }
                    break;
                case PreviewType.Xml:
                    {
                        using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                        {
                            var text = new StreamReader(new AionXmlStreamReader(fs, true)).ReadToEnd();
                            ShowTextViewer(text);
                        }
                    }
                    break;
                case PreviewType.Image:
                    {
                        using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                        {
                            ShowImageViewer(fs);
                        }
                    }
                    break;
                case PreviewType.Cgf:
                    {
                        using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                        {
                            ShowCgfViewer(new CgfLoader(fs));
                        }
                    }
                    break;
                default:
                    // unknown
                    break;
            }
        }

        private void PreviewFileFromPakEntryInfo(PakEntryInfo pakEntry)
        {
            using (var pr = new PakReader(pakEntry.PakFile))
            {
                switch (GetPreviewTypeFromFilename(pakEntry.EntryFilename))
                {
                    case PreviewType.Text:
                        using (var ms = new MemoryStream(pr.GetFile(pakEntry.EntryFilename)))
                        {
                            var text = new StreamReader(ms).ReadToEnd();
                            ShowTextViewer(text);
                        }
                        break;
                    case PreviewType.Html:
                        using (var ms = new MemoryStream(pr.GetFile(pakEntry.EntryFilename)))
                        {
                            try
                            {
                                var text = EncryptedHtml.DecodeToString(pakEntry.EntryFilename, ms);
                                ShowTextViewer(text);
                            }
                            catch {
                                // maybe not encrypted, show raw text
                                ms.Seek(0, SeekOrigin.Begin);
                                var text = new StreamReader(ms).ReadToEnd();
                                ShowTextViewer(text);
                            }
                        }
                        break;
                    case PreviewType.Xml:
                        using (var ms = new MemoryStream(pr.GetFile(pakEntry.EntryFilename)))
                        {
                            var text = new StreamReader(new AionXmlStreamReader(ms, true)).ReadToEnd();
                            ShowTextViewer(text);
                        }
                        break;
                    case PreviewType.Image:
                        using (var ms = new MemoryStream(pr.GetFile(pakEntry.EntryFilename)))
                        {
                            ShowImageViewer(ms);
                        }
                        break;
                    case PreviewType.Cgf:
                        using (var ms = new MemoryStream(pr.GetFile(pakEntry.EntryFilename)))
                        {
                            ShowCgfViewer(new CgfLoader(ms));
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        
        private string GetPakFileListing(string filename)
        {
            using (var pr = new PakReader(filename))
            {
                return string.Join("\r\n", pr.Files.Keys.OrderBy(o => o));
            }
        }

        private void ShowTextViewer(string text)
        {
            HideLevelViewer(); // TODO cleanly hide other viewer controls...
            HideImageViewer();
            m_textViewer.Text = ConvertLineEndings(text);
            m_textViewer.Show();
            DoResize();
        }

        private string ConvertLineEndings(string text)
        {
            var sb = new StringBuilder(text.Length * 3 / 2);
            for (int i = 0; i < text.Length; i++)
            {
                if (i + 1 < text.Length && text[i] == '\r' && text[i + 1] == '\n')
                {
                    sb.Append(Environment.NewLine);
                    i++;
                    continue;
                }
                else if (text[i] == '\n' || text[i] == '\r')
                {
                    sb.Append(Environment.NewLine);
                    continue;
                }
                sb.Append(text[i]);
            }
            return sb.ToString();
        }

        private void HideTextViewer()
        {
            m_textViewer.Hide();
        }

        private void LoadDirectoryStructure(string dir)
        {
            m_clientRoot = dir;
            var root = RecurseDirectoryLoad(m_clientRoot);
            root.Text = m_clientRoot;
            root.ToolTipText = m_clientRoot;
            root.ImageIndex = 1;
            root.SelectedImageIndex = 1;
            m_treeView.Nodes.Add(root);
            root.Expand();
        }

        private void SetNodeIconFromFilename(TreeNode node, string filename)
        {
            var previewType = GetPreviewTypeFromFilename(filename);
            if (previewType == PreviewType.Pak)
            {
                node.ImageIndex = 2;
                node.SelectedImageIndex = 2;
            }
            else if (previewType == PreviewType.Text ||
                previewType == PreviewType.Xml ||
                previewType == PreviewType.Html)
            {
                node.ImageIndex = 3;
                node.SelectedImageIndex = 3;
            }
            else if (previewType == PreviewType.Image)
            {
                node.ImageIndex = 4;
                node.SelectedImageIndex = 4;
            }
            else
            {
                node.ImageIndex = 0;
                node.SelectedImageIndex = 0;
            }
        }

        private TreeNode RecurseDirectoryLoad(string path)
        {
            var thisNode = new TreeNode(Path.GetFileName(path));
            foreach (var dir in new DirectoryInfo(path).EnumerateDirectories())
            {
                var node = RecurseDirectoryLoad(dir.FullName);
                node.ImageIndex = 1;
                node.SelectedImageIndex = 1;
                thisNode.Nodes.Add(node);
            }
            foreach (var file in new DirectoryInfo(path).EnumerateFiles())
            {
                var node = new TreeNode(file.Name);
                var previewType = GetPreviewTypeFromFilename(file.Name);
                if (previewType == PreviewType.Pak)
                {
                    node.Nodes.Add("Loading...");
                }
                SetNodeIconFromFilename(node, file.Name);
                node.Tag = file;
                thisNode.Nodes.Add(node);
            }
            return thisNode;
        }

        private void ShowImageViewer(Stream s)
        {
            HideLevelViewer();
            HideTextViewer();

            try
            {
                m_imageViewer.SetImage(s);
            }
            catch (Exception e)
            {
                HideImageViewer();
                ShowTextViewer("Error loading image: " + e);
                return;
            }
            DoResize();
            m_imageViewer.Show();
            Invalidate();
        }

        private void HideImageViewer()
        {
            m_imageViewer.Hide();
        }

        private void ShowCgfViewer(CgfLoader cgf)
        {
            HideLevelViewer();
            HideImageViewer();
            HideTextViewer();

            m_vc = new AionLevelViewerControl(null, null, cgf);
            m_vc.Location = new Point(m_treeView.Width, m_menuStrip.Bottom);
            var clientHeight = ClientSize.Height - m_menuStrip.Height;
            m_vc.Size = new Size(ClientSize.Width - m_treeView.Width, clientHeight);
            m_vc.OnUpdateTitle += OnLevelViewerControlTitleUpdated;
            Controls.Add(m_vc);
            m_vc.Focus();
            Invalidate();
        }

        private void OnLevelViewerControlTitleUpdated(object sender, string title)
        {
            // show camera pos, etc...
            SetSecondaryTitle(title);
        }

        private void ChangeLevel(string levelFolder)
        {
            SetTitleBase(Path.Combine(m_clientRoot, "Levels", levelFolder));

            HideLevelViewer();
            HideImageViewer();
            HideTextViewer();

            m_vc = new AionLevelViewerControl(m_clientRoot, levelFolder, null);
            m_vc.Location = new Point(m_treeView.Width, m_menuStrip.Bottom);
            var clientHeight = ClientSize.Height - m_menuStrip.Height;
            m_vc.Size = new Size(ClientSize.Width - m_treeView.Width, clientHeight);
            m_vc.OnUpdateTitle += OnLevelViewerControlTitleUpdated;
            Controls.Add(m_vc);
            m_vc.Focus();
            Invalidate();
            m_vc.m_game.SetProjection(ClientSize.Width - m_treeView.Width, clientHeight);
        }

        private string m_titleBase = "";
        private void SetTitleBase(string text)
        {
            m_titleBase = text;
            Text = text;
        }

        private void SetSecondaryTitle(string text)
        {
            Text = m_titleBase + " - " + text;
        }

        private void HideLevelViewer()
        {
            if (m_vc != null)
            {
                Controls.Remove(m_vc);
                m_vc.Dispose();
                m_vc = null;
            }
        }

        private void M_treeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TryOpenLevelViewerForSelectedNode();
        }

        private void TryOpenLevelViewerForSelectedNode()
        {
            if (m_treeView.SelectedNode.Name == "level-viewer")
            {
                ChangeLevel(m_treeView.SelectedNode.Text);
            }
        }

        private void LoadLevels()
        {
            var levelsNode = m_treeView.Nodes.Add("Levels");

            foreach (var path in Directory.EnumerateDirectories(Path.Combine(m_clientRoot, "levels")))
            {
                using (var dir = new DirManager(path))
                {
                    if (dir.Exists("leveldata.xml"))
                    {
                        var n = levelsNode.Nodes.Add("level-viewer", Path.GetFileName(path), 5, 5);
                    }
                }
            }
            levelsNode.Expand();
        }

        protected override void OnResize(EventArgs e)
        {
            DoResize();
        }

        private void DoResize()
        {
            if (m_treeView == null)
                return;

            var clientHeight = ClientSize.Height - m_menuStrip.Height;
            m_treeView.Height = clientHeight;
            FakeResizeLevelViewer(ClientSize.Width - m_treeView.Width, clientHeight);

            m_textViewer.Location = new Point(m_treeView.Right, m_menuStrip.Bottom);
            m_textViewer.Size = new Size(ClientSize.Width - m_treeView.Width, clientHeight);

            m_imageViewer.Location = new Point(m_treeView.Right, m_menuStrip.Bottom);
            m_imageViewer.Size = new Size(ClientSize.Width - m_treeView.Width, clientHeight);
        }
        
        private void FakeResizeLevelViewer(int newWidth, int newHeight)
        {
            if (m_vc == null)
                return;

            // monogame/forms distorts the aspect after a resize...
            // this works around the issue by reattaching the game to a fresh control.

            Controls.Remove(m_vc);
            var old = m_vc;

            old.m_game.SetProjection(newWidth, newHeight);
            m_vc = new AionLevelViewerControl(old.m_game);
            m_vc.Location = new Point(m_treeView.Right, m_menuStrip.Bottom);
            m_vc.Size = new Size(newWidth, newHeight);
            m_vc.OnUpdateTitle += OnLevelViewerControlTitleUpdated;

            Controls.Add(m_vc);

            old.Dispose();
        }
    }
}
