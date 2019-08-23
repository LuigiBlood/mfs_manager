using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using mfs_manager;

namespace mfs_gui
{
    static class Program
    {
        static MainForm mainform;
        static string loadedfilepath;
        static MFSDisk disk;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            mainform = new MainForm();
            Application.Run(mainform);
        }

        public static bool LoadDisk(string filepath)
        {
            disk = new MFSDisk(filepath);
            if (disk.Format != MFS.DiskFormat.Invalid)
            {
                loadedfilepath = filepath;
                return true;
            }
            loadedfilepath = "";
            disk = null;
            return false;
        }

        public static bool SaveDisk(string filepath = "")
        {
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
                return false;

            if (filepath == "")
                filepath = loadedfilepath;

            disk.Save(filepath);

            loadedfilepath = filepath;
            return true;
        }

        public static bool GetDirectoryNode(out TreeNode nodes)
        {
            nodes = new TreeNode();
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
                return false;

            nodes = GetDirectoryTreeNode(MFSRAMUtil.GetDirectoryFromID(disk, 0));

            return true;
        }

        static TreeNode GetDirectoryTreeNode(MFSDirectory pdir)
        {
            TreeNode node = new TreeNode(pdir.Name);
            node.Tag = pdir;
            foreach (MFSDirectory dir in MFSRAMUtil.GetAllDirectoriesFromDirID(disk, pdir.DirectoryID))
            {
                node.Nodes.Add(GetDirectoryTreeNode(dir));
            }
            return node;
        }

        public static bool GetAllFilesFromDirectory(MFSDirectory dir, out ListViewItem[] items)
        {
            List<ListViewItem> list = new List<ListViewItem>();
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
            {
                items = null;
                return false;
            }

            foreach (MFSFile file in MFSRAMUtil.GetAllFilesFromDirID(disk, dir.DirectoryID))
            {
                ListViewItem item = new ListViewItem(file.Name + (file.Ext != "" ? "." + file.Ext : ""));
                item.Tag = file;
                item.ImageIndex = 0;
                list.Add(item);
            }

            items = list.ToArray();
            return true;
        }

        public static bool AddFileToDirectory(MFSDirectory dir, string filepath)
        {
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
            {
                return false;
            }
            FileStream file = new FileStream(filepath, FileMode.Open);
            byte[] filedata = new byte[file.Length];
            file.Read(filedata, 0, (int)file.Length);

            MFSRAMUtil.WriteFile(disk, filedata, Path.GetFileName(filepath), dir.DirectoryID);

            file.Close();

            return true;
        }

        public static byte[] LoadFileData(MFSFile file)
        {
            return MFSRAMUtil.ReadFile(disk, file);
        }

        public static bool SaveFiles(MFSFile[] files, string folderpath)
        {
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
            {
                return false;
            }

            foreach (MFSFile file in files)
            {
                byte[] filedata = MFSRAMUtil.ReadFile(disk, file);
                FileStream fileout = new FileStream(folderpath + "\\" + file.Name + (file.Ext != "" ? "." + file.Ext : ""), FileMode.Create);
                fileout.Write(filedata, 0, filedata.Length);
                fileout.Close();
            }
            return true;
        }

        public static bool DeleteFiles(MFSFile[] files)
        {
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
            {
                return false;
            }

            foreach (MFSFile file in files)
            {
                MFSRAMUtil.DeleteFile(disk, file);
            }
            return true;
        }

        public static bool SaveFile(MFSFile file, string filepath)
        {
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
            {
                return false;
            }

            byte[] filedata = MFSRAMUtil.ReadFile(disk, file);
            FileStream fileout = new FileStream(filepath, FileMode.Create);
            fileout.Write(filedata, 0, filedata.Length);
            fileout.Close();

            return true;
        }

        public static bool CopyFiles(MFSFile[] files, ushort dir)
        {
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
            {
                return false;
            }

            foreach (MFSFile file in files)
            {
                MFSRAMUtil.WriteFile(disk, MFSRAMUtil.ReadFile(disk, file), file.Name + (file.Ext != "" ? "." + file.Ext : ""), dir);
            }

            return true;
        }

        public static bool MoveFiles(MFSFile[] files, ushort dir)
        {
            if (disk == null || disk.Format == MFS.DiskFormat.Invalid)
            {
                return false;
            }

            foreach (MFSFile file in files)
            {
                MFSRAMUtil.MoveFile(disk, MFSRAMUtil.GetFullEntryPath(disk, file), MFSRAMUtil.GetFullEntryPath(disk, MFSRAMUtil.GetDirectoryFromID(disk, dir)));
            }

            return true;
        }
    }
}
