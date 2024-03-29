﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FolderSelect;
using mfs_library;
using static System.Net.WebRequestMethods;

namespace mfs_gui
{
    public partial class MainForm : Form
    {
        enum cliptype
        {
            Copy, Cut
        }

        MFSDirectory current_dir;
        MFSFile[] clipboardfiles;
        cliptype clipboardtype;
        bool changed;

        public MainForm()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofs = new OpenFileDialog();
            ofs.Filter = "All Supported Files (*.ndd, *.ndr, *.ram, *.n64, *.z64, *.disk)|*.ndd;*.ndr;*.ram;*.n64;*.z64;*.disk|64DD RAM Area Image (*.ram)|*.ram|64DD Disk Image (*.ndd, *.ndr, *.disk)|*.ndd;*.ndr;*.disk|N64 Cartridge Port Image (*.n64, *.z64)|*.n64;*.z64|All files|*.*";
            ofs.Title = "Open Disk Image...";
            ofs.Multiselect = false;
            if (ofs.ShowDialog() == DialogResult.OK)
            {
                if (Program.LoadDisk(ofs.FileName))
                {
                    treeViewMFS.Nodes.Clear();
                    listViewMFS.Items.Clear();

                    current_dir = null;
                    clipboardfiles = null;

                    TreeNode node;
                    if (Program.GetDirectoryNode(out node))
                    {
                        treeViewMFS.Nodes.Add(node);
                        treeViewMFS.Nodes[0].Expand();
                        treeViewMFS.SelectedNode = treeViewMFS.Nodes[0];
                    }
                }
                else
                {
                    MessageBox.Show("Could not load " + ofs.SafeFileName, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                UpdateFormText();
                UpdateStatusBar();
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.IsDiskLoaded())
            {
                Program.SaveDisk();
                changed = false;
            }
            UpdateFormText();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Program.IsDiskLoaded())
                return;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save as...";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                Program.SaveDisk(sfd.FileName);
                changed = false;
            }
            UpdateFormText();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("64DD MFS Manager (GUI) " + Application.ProductVersion + " by LuigiBlood", "About...", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (changed && MessageBox.Show("Are you sure you want to exit 64DD MFS Manager?\nYour changes will be lost.", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                e.Cancel = true;
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            current_dir = (MFSDirectory)e.Node.Tag;
            UpdateTreeView(current_dir);
        }

        private void listView_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (current_dir != null)
                e.Effect = DragDropEffects.Copy;
        }

        private void listView_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            Program.AddFilesToDirectory(current_dir, files);
            UpdateTreeView(current_dir);
            changed = true;
            UpdateFormText();
        }

        private void contextMenuStripFile_Opening(object sender, CancelEventArgs e)
        {
            foreach (ToolStripItem test in contextMenuStripFile.Items)
            {
                test.Enabled = false;
            }
            if (listViewMFS.SelectedItems.Count > 0)
            {
                extractToolStripMenuItem.Enabled = true;
                cutToolStripMenuItem.Enabled = true;
                copyToolStripMenuItem.Enabled = true;
                deleteToolStripMenuItem.Enabled = true;
                if (listViewMFS.SelectedItems.Count == 1)
                    renameToolStripMenuItem.Enabled = true;

                List<string> files = new List<string>();
                foreach (ListViewItem item in listViewMFS.SelectedItems)
                {
                    files.Add(((MFSFile)item.Tag).GetEntryName());
                }
                if (Program.CanExportConvertFiles(files.ToArray()))
                {
                    convertFilesToolStripMenuItem.Enabled = true;
                }

            }
            if (current_dir != null)
                importToolStripMenuItem.Enabled = true;
            if (clipboardfiles != null && clipboardfiles.Length > 0)
                pasteToolStripMenuItem.Enabled = true;
        }

        private void importToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofs = new OpenFileDialog();
            ofs.Filter = "All files|*.*";
            ofs.Title = "Import Files...";
            ofs.Multiselect = true;

            if (ofs.ShowDialog() == DialogResult.OK)
            {
                string[] files = ofs.FileNames;
                Program.AddFilesToDirectory(current_dir, files);
                UpdateTreeView(current_dir);
                changed = true;
                UpdateFormText();
            }
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewMFS.SelectedItems.Count > 1)
            {
                //More than one file
                FolderSelectDialog fsd = new FolderSelectDialog();
                fsd.Title = "Export Files...";
                if (fsd.ShowDialog())
                {
                    List<MFSFile> files = new List<MFSFile>();
                    foreach (ListViewItem item in listViewMFS.SelectedItems)
                    {
                        files.Add((MFSFile)item.Tag);
                    }
                    if (Program.SaveFiles(files.ToArray(), fsd.FileName))
                        MessageBox.Show("Files are extracted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (listViewMFS.SelectedItems.Count == 1)
            {
                //Only one file
                MFSFile file = (MFSFile)listViewMFS.SelectedItems[0].Tag;
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Export File...";
                sfd.FileName = file.Name + (file.Ext != "" ? "." + file.Ext : "");
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (Program.SaveFile(file, sfd.FileName))
                        MessageBox.Show("File is extracted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void convertFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewMFS.SelectedItems.Count > 1)
            {
                //More than one file
                FolderSelectDialog fsd = new FolderSelectDialog();
                fsd.Title = "Convert File(s)...";
                if (fsd.ShowDialog())
                {
                    List<MFSFile> files = new List<MFSFile>();
                    foreach (ListViewItem item in listViewMFS.SelectedItems)
                    {
                        files.Add((MFSFile)item.Tag);
                    }
                    if (Program.ExportConvertFiles(files.ToArray(), fsd.FileName))
                        MessageBox.Show("Files are converted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (listViewMFS.SelectedItems.Count == 1)
            {
                //Only one file
                MFSFile file = (MFSFile)listViewMFS.SelectedItems[0].Tag;
                if (!Program.CanExportConvertFile(file.GetEntryName()))
                {
                    return;
                }
                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Convert File...";
                sfd.FileName = file.GetEntryName() + ".png";
                sfd.Filter = "Image File (*.png)|*.png";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (Program.ExportConvertFile(file, sfd.FileName))
                        MessageBox.Show("File is converted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewMFS.SelectedItems.Count > 0)
            {
                List<MFSFile> files = new List<MFSFile>();
                foreach (ListViewItem item in listViewMFS.SelectedItems)
                {
                    files.Add((MFSFile)item.Tag);
                }
                Program.DeleteFiles(files.ToArray());
                UpdateTreeView(current_dir);
                changed = true;
                UpdateFormText();
            }
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<MFSFile> files = new List<MFSFile>();
            foreach (ListViewItem item in listViewMFS.SelectedItems)
            {
                files.Add((MFSFile)item.Tag);
            }

            clipboardfiles = files.ToArray();
            clipboardtype = cliptype.Cut;
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<MFSFile> files = new List<MFSFile>();
            foreach (ListViewItem item in listViewMFS.SelectedItems)
            {
                files.Add((MFSFile)item.Tag);
            }

            clipboardfiles = files.ToArray();
            clipboardtype = cliptype.Copy;
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (clipboardtype == cliptype.Copy)
                Program.CopyFiles(clipboardfiles, current_dir.DirectoryID);
            else
                Program.MoveFiles(clipboardfiles, current_dir.DirectoryID);
            UpdateTreeView(current_dir);
            changed = true;
            UpdateFormText();
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listViewMFS.SelectedItems.Count == 1)
            {
                listViewMFS.SelectedItems[0].BeginEdit();
            }
        }

        private void listView_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            MFSFile file = (MFSFile)listViewMFS.Items[e.Item].Tag;
            if (e.Label != null)
            {
                string _name = Path.GetFileNameWithoutExtension(e.Label);
                string _ext = Path.GetExtension(e.Label);
                if (_ext.StartsWith("."))
                    _ext = _ext.Substring(1);

                if (file.Ext != _ext)
                {
                    if (MessageBox.Show("Are you sure to change the extension of the file? It may not work as intended.", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                        return;
                    changed = true;
                }

                if (file.Name != _name)
                    changed = true;

                file.Ext = _ext;
                file.Name = _name;
            }
            UpdateTreeView(current_dir);
            UpdateFormText();
        }

        //Other
        private void UpdateTreeView(MFSDirectory dir)
        {
            ListViewItem[] items;
            listViewMFS.Items.Clear();
            if (Program.GetAllFilesFromDirectory(dir, out items))
            {
                listViewMFS.Items.AddRange(items);
            }
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            statusStrip1.Items.Clear();
            if (Program.IsDiskLoaded())
            {
                statusStrip1.Items.Add("Free Space: " + Math.Floor((Program.GetCapacitySize() - Program.GetUsedSpaceSize()) / 100000f) / 10f + " MB");
            }
        }

        private void UpdateFormText()
        {
            if (Program.IsDiskLoaded())
            {
                this.Text = "64DD MFS Manager - " + (changed ? "*" : "") + "[" + Program.GetDiskFilename() + "]";
            }
            else
            {
                this.Text = "64DD MFS Manager";
            }
        }
    }
}
