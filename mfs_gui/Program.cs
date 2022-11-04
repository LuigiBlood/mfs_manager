using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using mfs_library;
using System.Drawing;

namespace mfs_gui
{
    static class Program
    {
        static MainForm mainform;
        static string loadedfilepath;
        static MFSDisk disk;

        static char[] symbolsContainer = { '♦', '■', '●', '♥', '♠', '♣', '▼', '♪', '▲', '★' };

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            mainform = new MainForm();
            Application.Run(mainform);
        }

        public static bool IsDiskLoaded()
        {
            if (disk == null) return false;
            return (disk.Disk.RAMFileSystem == LeoDisk.FileSystem.MFS);
        }

        public static string GetDiskFilename()
        {
            return loadedfilepath;
        }

        public static bool LoadDisk(string filepath)
        {
            MFSDisk disk_temp = new MFSDisk(filepath);
            if (disk_temp.Disk.RAMFileSystem == LeoDisk.FileSystem.MFS)
            {
                loadedfilepath = filepath;
                disk = disk_temp;
                return true;
            }
            return false;
        }

        public static bool SaveDisk(string filepath = "")
        {
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
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
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
                return false;

            nodes = GetDirectoryTreeNode(MFSRAMUtil.GetDirectoryFromID(disk, 0));
            nodes.ImageIndex = 0;
            nodes.SelectedImageIndex = 0;
            return true;
        }

        static TreeNode GetDirectoryTreeNode(MFSDirectory pdir)
        {
            TreeNode node = new TreeNode(pdir.Name);
            node.Tag = pdir;
            node.ImageIndex = GetContainerColor(pdir.Name);
            node.SelectedImageIndex = node.ImageIndex;
            foreach (MFSDirectory dir in MFSRAMUtil.GetAllDirectoriesFromDirID(disk, pdir.DirectoryID))
            {
                node.Nodes.Add(GetDirectoryTreeNode(dir));
            }
            return node;
        }

        static int GetContainerColor(string name)
        {
            foreach (char c in name)
            {
                for (int i = 0; i < symbolsContainer.Length; i++)
                    if (c == symbolsContainer[i]) return i + 2;
            }
            return 1;
        }

        public static bool GetAllFilesFromDirectory(MFSDirectory dir, out ListViewItem[] items)
        {
            List<ListViewItem> list = new List<ListViewItem>();
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
            {
                items = null;
                return false;
            }

            foreach (MFSFile file in MFSRAMUtil.GetAllFilesFromDirID(disk, dir.DirectoryID))
            {
                ListViewItem item = new ListViewItem(file.Name + (file.Ext != "" ? "." + file.Ext : ""));
                item.Tag = file;
                item.ImageIndex = mainform.imageListLarge.Images.IndexOfKey(file.Ext);
                if (item.ImageIndex == -1)
                    item.ImageIndex = 0;
                list.Add(item);
            }

            items = list.ToArray();
            return true;
        }

        public static bool AddFileToDirectory(MFSDirectory dir, string filepath)
        {
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
            {
                return false;
            }

            bool error;
            if (!CanImportConvertFile(filepath))
            {
                byte[] filedata = File.ReadAllBytes(filepath);
                error = MFSRAMUtil.WriteFile(disk, filedata, Path.GetFileName(filepath), dir.DirectoryID);
            }
            else
            {
                byte[] filedata;
                string filename;

                if (ImportConvertFile(filepath, out filedata, out filename))
                {
                    error = MFSRAMUtil.WriteFile(disk, filedata, filename, dir.DirectoryID);
                }
                else
                {
                    error = false;
                }
            }

            return error;
        }

        public static bool AddFilesToDirectory(MFSDirectory dir, string[] filepaths)
        {
            foreach (string file in filepaths)
            {
                if (Program.AddFileToDirectory(dir, file) == false)
                {
                    MessageBox.Show("Could not import " + Path.GetFileName(file) + "\nCancelling file import.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                }
            }
            return true;
        }

        public static bool ImportConvertFile(string filepath, out byte[] bytes, out string filename)
        {
            if (!CanImportConvertFile(filepath))
            {
                bytes = null;
                filename = null;
                return false;
            }

            Bitmap input = new Bitmap(filepath);

            bytes = mfs_library.MA.MA2D1.ConvertToMA2D1(input);
            filename = Path.GetFileNameWithoutExtension(filepath) + ".MA2D1";

            input.Dispose();

            return true;
        }

        public static bool CanImportConvertFile(string filepath)
        {
            switch (Path.GetExtension(filepath).ToLower())
            {
                case ".png":
                case ".bmp":
                case ".jpg":
                case ".jpeg":
                    return true;
            }
            return false;
        }

        public static bool CanImportConvertFiles(string[] filepaths)
        {
            foreach (string s in filepaths)
            {
                switch (Path.GetExtension(s).ToLower())
                {
                    case ".png":
                    case ".bmp":
                    case ".jpg":
                    case ".jpeg":
                        return true;
                }
            }
            return false;
        }

        public static bool ExportConvertFile(MFSFile file, string filepath)
        {
            if (!CanExportConvertFile(file.GetEntryName())) return false;

            var input = LoadFileData(file);

            Bitmap output = mfs_library.MA.MA2D1.ConvertToBitmap(input);
            output.Save(filepath);
            output.Dispose();
            return true;
        }

        public static bool ExportConvertFiles(MFSFile[] file, string folderpath)
        {
            foreach (MFSFile f in file)
            {
                ExportConvertFile(f, folderpath + "\\" + f.GetEntryName() + ".png");
            }
            return true;
        }

        public static bool CanExportConvertFile(string filepath)
        {
            switch (Path.GetExtension(filepath).ToLower())
            {
                case ".ma2d1":
                    return true;
            }
            return false;
        }

        public static bool CanExportConvertFiles(string[] filepaths)
        {
            foreach (string s in filepaths)
            {
                switch (Path.GetExtension(s).ToLower())
                {
                    case ".ma2d1":
                        return true;
                }
            }
            return false;
        }

        public static byte[] LoadFileData(MFSFile file)
        {
            return MFSRAMUtil.ReadFile(disk, file);
        }

        public static bool SaveFiles(MFSFile[] files, string folderpath)
        {
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
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
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
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
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
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
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
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
            if (disk == null || disk.Disk.Format == LeoDisk.DiskFormat.Invalid)
            {
                return false;
            }

            foreach (MFSFile file in files)
            {
                MFSRAMUtil.MoveFile(disk, MFSRAMUtil.GetFullPath(disk, file), MFSRAMUtil.GetFullPath(disk, MFSRAMUtil.GetDirectoryFromID(disk, dir)));
            }

            return true;
        }

        public static int GetCapacitySize()
        {
            return MFSRAMUtil.GetCapacitySize(disk);
        }

        public static int GetUsedSpaceSize()
        {
            return MFSRAMUtil.GetTotalUsedSize(disk);
        }
    }
}
