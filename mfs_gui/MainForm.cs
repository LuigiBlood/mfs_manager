using System;
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
using mfs_manager;

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

        public MainForm()
        {
            InitializeComponent();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofs = new OpenFileDialog();
            ofs.Filter = "All Supported Files (*.ndd, *.ndr, *.ram)|*.ndd;*.ndr;*.ram|64DD RAM Area Image (*.ram)|*.ram|64DD Disk Image (*.ndd, *.ndr)|*.ndd;*.ndr|All files|*.*";
            ofs.Title = "Open Disk Image...";
            ofs.Multiselect = false;
            if (ofs.ShowDialog() == DialogResult.OK)
            {
                treeView.Nodes.Clear();
                listView.Items.Clear();
                if (Program.LoadDisk(ofs.FileName))
                {
                    TreeNode node;
                    if (Program.GetDirectoryNode(out node))
                    {
                        treeView.Nodes.Add(node);
                        treeView.Nodes[0].Expand();
                        treeView.SelectedNode = treeView.Nodes[0];
                    }
                    this.Text = "64DD MFS Manager - [" + Program.GetDiskFilename() + "]";
                }
                else
                {
                    MessageBox.Show("Could not load " + ofs.SafeFileName, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    current_dir = null;
                    clipboardfiles = null;
                    this.Text = "64DD MFS Manager";
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Program.IsDiskLoaded())
                Program.SaveDisk();
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
                this.Text = "64DD MFS Manager - [" + Program.GetDiskFilename() + "]";
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to exit 64DD MFS Manager?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
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
            foreach (string file in files)
            {
                Program.AddFileToDirectory(current_dir, file);
            }
            UpdateTreeView(current_dir);
        }

        private void contextMenuStripFile_Opening(object sender, CancelEventArgs e)
        {
            foreach (ToolStripItem test in contextMenuStripFile.Items)
            {
                test.Enabled = false;
            }
            if (listView.SelectedItems.Count > 0)
            {
                extractToolStripMenuItem.Enabled = true;
                cutToolStripMenuItem.Enabled = true;
                copyToolStripMenuItem.Enabled = true;
                deleteToolStripMenuItem.Enabled = true;
                if (listView.SelectedItems.Count == 1)
                    renameToolStripMenuItem.Enabled = true;
            }
            if (clipboardfiles != null && clipboardfiles.Length > 0)
                pasteToolStripMenuItem.Enabled = true;
        }

        private void extractToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 1)
            {
                //More than one file
                FolderSelectDialog fsd = new FolderSelectDialog();
                fsd.Title = "Export Files...";
                if (fsd.ShowDialog())
                {
                    List<MFSFile> files = new List<MFSFile>();
                    foreach (ListViewItem item in listView.SelectedItems)
                    {
                        files.Add((MFSFile)item.Tag);
                    }
                    if (Program.SaveFiles(files.ToArray(), fsd.FileName))
                        MessageBox.Show("Files are extracted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else if (listView.SelectedItems.Count == 1)
            {
                //Only one file
                MFSFile file = (MFSFile)listView.SelectedItems[0].Tag;
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

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
            {
                List<MFSFile> files = new List<MFSFile>();
                foreach (ListViewItem item in listView.SelectedItems)
                {
                    files.Add((MFSFile)item.Tag);
                }
                Program.DeleteFiles(files.ToArray());
                UpdateTreeView(current_dir);
            }
        }

        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<MFSFile> files = new List<MFSFile>();
            foreach (ListViewItem item in listView.SelectedItems)
            {
                files.Add((MFSFile)item.Tag);
            }

            clipboardfiles = files.ToArray();
            clipboardtype = cliptype.Cut;
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<MFSFile> files = new List<MFSFile>();
            foreach (ListViewItem item in listView.SelectedItems)
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
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 1)
            {
                listView.SelectedItems[0].BeginEdit();
            }
        }

        private void listView_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            MFSFile file = (MFSFile)listView.Items[e.Item].Tag;
            if (e.Label != null)
            {
                file.Name = Path.GetFileNameWithoutExtension(e.Label);
                string _ext = Path.GetExtension(e.Label);
                if (_ext.StartsWith("."))
                    file.Ext = _ext.Substring(1);
            }
            UpdateTreeView(current_dir);
        }

        //Other
        private void UpdateTreeView(MFSDirectory dir)
        {
            ListViewItem[] items;
            listView.Items.Clear();
            if (Program.GetAllFilesFromDirectory(dir, out items))
            {
                listView.Items.AddRange(items);
            }
        }
    }
}
