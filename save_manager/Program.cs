using mfs_library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace save_manager
{
    internal static class Program
    {
        /// <summary>
        /// Point d'entrée principal de l'application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public static LeoDisk Disk;

        public enum DiskError
        {
            Success,
            NotDiskFile,
            NoRAMArea,
            FileNotExist,
            Mismatch
        }

        public static DiskError Load(string filepath)
        {
            LeoDisk _disk;
            var error = Load(filepath, out _disk);

            if (error == DiskError.Success)
                Disk = _disk;

            return error;
        }

        public static DiskError Load(string filepath, out LeoDisk _disk)
        {
            _disk = null;
            if (!File.Exists(filepath))
                return DiskError.FileNotExist;

            _disk = new LeoDisk(filepath);
            if (_disk.Format == LeoDisk.DiskFormat.Invalid)
                return DiskError.NotDiskFile;
            if (_disk.DiskType >= 6)
                return DiskError.NoRAMArea;

            return DiskError.Success;
        }

        public static void ExportRAM(string filepath)
        {
            if (Disk == null) return;

            File.WriteAllBytes(filepath, Disk.GetRAMAreaArray());
        }

        public static DiskError ImportRAM(string filepath)
        {
            LeoDisk _disk;
            var error = Load(filepath, out _disk);

            if (error != DiskError.Success) return error;
            if (_disk.DiskType != Disk.DiskType) return DiskError.Mismatch;

            for (var lba = Leo.RamStartLBA[_disk.DiskType]; lba < Leo.SIZE_LBA; lba++)
            {
                Disk.WriteLBA(lba, _disk.ReadLBA(lba));
            }

            return DiskError.Success;
        }
    }
}
