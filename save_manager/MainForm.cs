using mfs_library;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Linq.Expressions;

namespace save_manager
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void buttonBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofs = new OpenFileDialog();
            ofs.Title = "Open Base 64DD Disk / RAM File...";
            ofs.Multiselect = false;
            ofs.Filter = "All Supported Files (*.ndd, *.ndr, *.ram, *.n64, *.z64, *.disk)|*.ndd;*.ndr;*.ram;*.n64;*.z64;*.disk|64DD RAM Area Image (*.ram)|*.ram|64DD Disk Image (*.ndd, *.ndr, *.disk)|*.ndd;*.ndr;*.disk|N64 Cartridge Port Image (*.n64, *.z64)|*.n64;*.z64|All files|*.*";
            if (ofs.ShowDialog() == DialogResult.OK)
            {
                var error = Program.Load(ofs.FileName);
                if (error == Program.DiskError.NotDiskFile)
                {
                    MessageBox.Show("This file is not a disk file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (error == Program.DiskError.NoRAMArea)
                {
                    MessageBox.Show("This disk does not contain a RAM Area.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (error == Program.DiskError.FileNotExist)
                {
                    MessageBox.Show("This file does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                textBoxBaseFile.Text = Program.Disk.Filename;
            }
        }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            if (Program.Disk == null)
            {
                MessageBox.Show("Please load a disk file first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Export RAM File...";
            sfd.Filter = "64DD RAM Area Image (*.ram)|*.ram";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                bool saveError = false;
                try
                {
                    Program.ExportRAM(sfd.FileName);
                }
                catch
                {
                    saveError = true;
                }

                if (!saveError)
                {
                    MessageBox.Show("File has been saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void buttonImport_Click(object sender, EventArgs e)
        {
            if (Program.Disk == null)
            {
                MessageBox.Show("Please load a disk file first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            OpenFileDialog ofs = new OpenFileDialog();
            ofs.Title = "Import 64DD Disk / RAM File...";
            ofs.Multiselect = false;
            ofs.Filter = "All Supported Files (*.ndd, *.ndr, *.ram, *.n64, *.z64, *.disk)|*.ndd;*.ndr;*.ram;*.n64;*.z64;*.disk|64DD RAM Area Image (*.ram)|*.ram|64DD Disk Image (*.ndd, *.ndr, *.disk)|*.ndd;*.ndr;*.disk|N64 Cartridge Port Image (*.n64, *.z64)|*.n64;*.z64|All files|*.*";
            if (ofs.ShowDialog() == DialogResult.OK)
            {
                var error = Program.ImportRAM(ofs.FileName);
                if (error == Program.DiskError.NotDiskFile)
                {
                    MessageBox.Show("This file is not a disk file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (error == Program.DiskError.NoRAMArea)
                {
                    MessageBox.Show("This disk does not contain a RAM Area.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (error == Program.DiskError.FileNotExist)
                {
                    MessageBox.Show("This file does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                else if (error == Program.DiskError.Mismatch)
                {
                    MessageBox.Show("Both disks' RAM Area format do not match.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }


                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Save Modified Disk...";
                switch (Program.Disk.Format)
                {
                    case LeoDisk.DiskFormat.SDK:
                    case LeoDisk.DiskFormat.MAME:
                        sfd.Filter = "64DD Disk Image (*.ndd, *.ndr, *.disk)|*.ndd;*.ndr;*.disk";
                        break;
                    case LeoDisk.DiskFormat.RAM:
                        sfd.Filter = "64DD RAM Area Image (*.ram)|*.ram";
                        break;
                    case LeoDisk.DiskFormat.N64:
                        sfd.Filter = "N64 Cartridge Port Image (*.n64, *.z64)|*.n64;*.z64";
                        break;
                    default:
                        return;
                }

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    bool saveError = false;
                    try
                    {
                        Program.Disk.Save(sfd.FileName);
                    }
                    catch
                    {
                        saveError = true;
                    }

                    if (!saveError)
                    {
                        MessageBox.Show("File has been saved.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }
    }
}
