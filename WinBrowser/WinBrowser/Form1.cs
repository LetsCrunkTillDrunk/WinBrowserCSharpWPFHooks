using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinBrowser
{
    public partial class Form1 : Form
    {

        [StructLayout(LayoutKind.Sequential)]
        public struct KeyboardHookStruct
        {
            public readonly int VirtualKeyCode;
            public readonly int ScanCode;
            public readonly int Flags;
            public readonly int Time;
            public readonly IntPtr ExtraInfo;
        }

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowsHookEx(
           int idHook,
           LowLevelKeyboardProc callback,
           IntPtr hInstance,
           uint threadId);


        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hInstance);

        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, int wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        public const int VK_LCONTROL = 0xA2;
        public const int WM_KEYDOWN = 0x0100;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        const int WH_KEYBOARD_LL = 13;
        private LowLevelKeyboardProc proc;


        private static IntPtr hhook = IntPtr.Zero;
        private Dictionary<string, int> columnsForDrives = new Dictionary<string, int>();
        private Dictionary<string, int> columnsForFiles = new Dictionary<string, int>();
        private string[] stringsForDrives = { "Имя", "Тип", "Файловая система", "Свободно", "Занято" };
        private string[] stringsForFiles = { "Имя", "Тип", "Размер", "Дата создания", "Дата изменения" };

        private List<FileSystemInfo> fileSystemItems = new List<FileSystemInfo>();
        private string filePath = null;
        private string fileName = null;
        private string directoryPath = null;

        public string SearchWord { get; set; }
        public string SearchPath { get; set; }

        private object locker = new object();

        private List<FileSystemInfo> matches = new List<FileSystemInfo>();


        public Form1()
        {

            InitializeComponent();
            proc = hookProc;
            listView1.View = View.Details;
            foreach (string s in stringsForDrives)
            {
                columnsForDrives.Add(s, 100);
            }
            foreach (string s in stringsForFiles)
            {
                columnsForFiles.Add(s, 100);
            }
            LoadDrives();

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            SetHook();
        }

        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void LoadDrives()
        {
            DriveInfo[] disc = DriveInfo.GetDrives();
            if (disc.Length == 0)
            {
                MessageBox.Show("Диски не обнаружены", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            foreach (DriveInfo d in disc)
            {
                if (d.IsReady)
                {
                    int imageIndex = 0;
                    switch (d.DriveType)
                    {
                        case DriveType.CDRom:
                            {
                                imageIndex = 1;
                                break;
                            }
                        case DriveType.Removable:
                            {
                                imageIndex = 2;
                                break;
                            }
                        default:
                            {
                                imageIndex = 0;
                                break;
                            }
                    }
                    TreeNode tnDisk = new TreeNode(d.Name, imageIndex, imageIndex);
                    treeView1.Nodes.Add(tnDisk);
                    try
                    {
                        string[] dirsOnDisc = Directory.GetDirectories(d.RootDirectory.ToString());
                        if (dirsOnDisc.Length > 0)
                        {
                            TreeNode tempNode = new TreeNode("!");
                            tnDisk.Nodes.Add(tempNode);
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }

            if (listView1.Columns.Count != 0) listView1.Columns.Clear();

            foreach (KeyValuePair<string, int> k in columnsForDrives)
            {
                ColumnHeader head = new ColumnHeader();
                head.Text = k.Key;
                head.Width = k.Value;
                head.TextAlign = HorizontalAlignment.Left;
                listView1.Columns.Add(head);
            }

            foreach (DriveInfo d in disc)
            {
                if (d.IsReady)
                {
                    string totalSize = String.Format("{0:F2} Гб", (double)d.TotalSize / Math.Pow(1000, 3));
                    string freeSpace = String.Format("{0:F2} Гб", (double)d.TotalFreeSpace / Math.Pow(1000, 3));
                    int imageIndex = 0;
                    switch (d.DriveType)
                    {
                        case DriveType.CDRom:
                            {
                                imageIndex = 1;
                                break;
                            }
                        case DriveType.Removable:
                            {
                                imageIndex = 2;
                                break;
                            }
                        default:
                            {
                                imageIndex = 0;
                                break;
                            }
                    }
                    ListViewItem lv = new ListViewItem(d.Name, imageIndex);
                    lv.SubItems.Add(d.DriveType.ToString());
                    lv.SubItems.Add(d.DriveFormat.ToString());
                    lv.SubItems.Add(totalSize);
                    lv.SubItems.Add(freeSpace);

                    lv.Tag = d;

                    listView1.Items.Add(lv);
                }
            }
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            SetFileSystemItems(e.Node.FullPath);
            ShowFileSystemItems();
            toolStripTextBox1.Text = e.Node.FullPath;
        }

        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            TreeNode currentNode = e.Node;

            currentNode.Nodes.Clear();

            string[] dirs = Directory.GetDirectories(currentNode.FullPath);
            foreach (string d in dirs)
            {
                TreeNode t = new TreeNode(Path.GetFileName(d), 3, 4);
                currentNode.Nodes.Add(t);

                try
                {
                    string[] a = Directory.GetDirectories(d);
                    if (a.Length > 0)
                    {
                        t.Nodes.Add("!");
                    }
                }
                catch { }
            }
        }

        public bool SetFileSystemItems(string path)
        {

            try
            {
                string[] access = Directory.GetDirectories(path);
            }
            catch
            {
                MessageBox.Show("Невозможно прочитать каталог", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }


            if (fileSystemItems != null && fileSystemItems.Count != 0)
            {
                fileSystemItems.Clear();
            }


            string[] directories = Directory.GetDirectories(path);
            foreach (string directory in directories)
            {
                DirectoryInfo di = new DirectoryInfo(directory);
                fileSystemItems.Add(di);
            }

            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                fileSystemItems.Add(fi);
            }

            return true;
        }

        private void ShowFileSystemItems()
        {
            listView1.BeginUpdate();
            listView1.Items.Clear();

            if (fileSystemItems == null || fileSystemItems.Count == 0)
            {
                return;
            }

            SetColumns();

            ListViewItem lviFile = null;
            foreach (FileSystemInfo file in fileSystemItems)
            {

                lviFile = new ListViewItem();
                lviFile.Tag = file;
                lviFile.Text = file.Name;


                if (file is DirectoryInfo)
                {
                    lviFile.ImageIndex = 3;
                    lviFile.SubItems.Add("Каталог");
                    lviFile.SubItems.Add("");

                }

                else if (file is FileInfo)
                {
                    FileInfo f = (FileInfo)file;
                    lviFile.ImageIndex = 5;
                    lviFile.SubItems.Add("Файл");
                    lviFile.SubItems.Add(String.Format("{0:F2} Mб", (double)f.Length / Math.Pow(1000, 2))
);
                }
                lviFile.SubItems.Add(file.CreationTime.ToString());
                lviFile.SubItems.Add(file.LastWriteTime.ToString());

                listView1.Items.Add(lviFile);

                listView1.EndUpdate();
            }
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ListView.SelectedListViewItemCollection collection = listView1.SelectedItems;
            ListViewItem selection = collection[0];
            object tag = selection.Tag;
            if (tag is FileInfo)
            {
                Process.Start(((FileInfo)tag).FullName);
                return;
            }

            string path = null;
            if (tag is DriveInfo)
            {
                path = ((DriveInfo)tag).RootDirectory.ToString();
            }
            else if (tag is DirectoryInfo)
            {
                path = ((DirectoryInfo)tag).FullName;
            }

            if (SetFileSystemItems(path))
            {
                toolStripTextBox1.Text = path;
                ShowFileSystemItems();
                ShowPathInTree(path);
            }

        }
        private void ShowPathInTree(string path)
        {

            string[] directories = path.Split('\\');
            string root = Path.GetPathRoot(path);

            TreeNode currentNode = null;
            foreach (TreeNode treeNode in treeView1.Nodes)
            {
                if (treeNode.Text == root)
                {
                    treeNode.Expand();
                    currentNode = treeNode;
                    break;
                }
            }


            for (int i = 1; i < directories.Length; i++)
            {

                if (directories[i].Length == 0)
                {
                    continue;
                }


                foreach (TreeNode treeNode in currentNode.Nodes)
                {
                    if (treeNode.Text == directories[i])
                    {
                        treeNode.Expand();
                        currentNode = treeNode;
                    }
                }
            }

            treeView1.SelectedNode = currentNode;
        }
        private void SetColumns()
        {
            if (listView1.Columns.Count != 0)
            {
                listView1.Columns.Clear();
            }

            ColumnHeader column = null;
            foreach (KeyValuePair<string, int> item in columnsForFiles)
            {
                column = new ColumnHeader();
                column.Text = item.Key;
                column.Width = item.Value;
                listView1.Columns.Add(column);
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }

        private void закрытьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void jnjhftybtToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void крупныеЗначкиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.LargeIcon;
        }

        private void мелкиеЗначкиToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.SmallIcon;
        }

        private void таблицаToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.Details;
            listView1.FullRowSelect = true;
        }

        private void плиткаToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.Tile;
        }

        private void списокToolStripMenuItem_Click(object sender, EventArgs e)
        {
            listView1.View = View.List;
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            string path = toolStripTextBox1.Text;

            DirectoryInfo currentDirectory = new DirectoryInfo(path);

            DirectoryInfo parentDirectory = currentDirectory.Parent;

            if (parentDirectory != null)
            {
                SetFileSystemItems(parentDirectory.FullName);
                ShowFileSystemItems();
                toolStripTextBox1.Text = parentDirectory.FullName;
                ShowPathInTree(toolStripTextBox1.Text);
            }
            else
            {
                treeView1.Nodes.Clear();
                LoadDrives();
            }
        }

        public void SetHook()
        {

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                hhook = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public static void UnHook()
        {
            UnhookWindowsHookEx(hhook);
        }

        private IntPtr hookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {

            if (nCode < 0)
            {
                return CallNextHookEx(hhook, nCode, (int)wParam, lParam);
            }
            else if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                var khs = (KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));

                bool ctrlDown = (GetKeyState(VK_LCONTROL) != 0);

                if (ctrlDown && khs.VirtualKeyCode == 0x43)
                {
                    if (listView1.SelectedItems.Count < 1) return CallNextHookEx(hhook, nCode, (int)wParam, lParam);
                    ListView.SelectedListViewItemCollection items = listView1.SelectedItems;
                    ListViewItem item = items[0];
                    object file = item.Tag;
                    if (file is FileInfo)
                    {
                        filePath = ((FileInfo)file).FullName;
                        fileName = ((FileInfo)file).Name;

                    }
                    else if (file is DirectoryInfo)
                    {
                        directoryPath = ((DirectoryInfo)file).FullName;

                    }

                    IntPtr val = new IntPtr(1);
                    return val;
                }
                else if (ctrlDown && khs.VirtualKeyCode == 0x56)
                {
                    if (filePath != null)
                    {
                        File.Copy(filePath, Path.Combine(toolStripTextBox1.Text, fileName), true);
                        filePath = null;
                        fileName = null;
                        directoryPath = null;
                        if (SetFileSystemItems(toolStripTextBox1.Text))
                        {
                            ShowFileSystemItems();
                            ShowPathInTree(toolStripTextBox1.Text);
                        }
                    }
                    if (directoryPath != null)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            DirectoryInfo source = new DirectoryInfo(directoryPath);
                            DirectoryInfo target = new DirectoryInfo(toolStripTextBox1.Text);
                            CopyDirectories(source, target.CreateSubdirectory(source.Name));
                            if (SetFileSystemItems(toolStripTextBox1.Text))
                            {
                                ShowFileSystemItems();
                                ShowPathInTree(toolStripTextBox1.Text);
                            }
                            filePath = null;
                            fileName = null;
                            directoryPath = null;
                        }
                        );
                    }

                    IntPtr val = new IntPtr(1);
                    return val;
                }
                else if (ctrlDown && khs.VirtualKeyCode == 0x4D)
                {
                    try
                    {
                        if (filePath != null)
                        {

                            FileInfo file = new FileInfo(filePath);
                            file.IsReadOnly = false;
                            File.Move(filePath, Path.Combine(toolStripTextBox1.Text, fileName));
                            filePath = null;
                            fileName = null;
                            directoryPath = null;

                            if (SetFileSystemItems(toolStripTextBox1.Text))
                            {
                                ShowFileSystemItems();
                                ShowPathInTree(toolStripTextBox1.Text);
                            }

                        }
                        if (directoryPath != null)
                        {
                            Task.Factory.StartNew(() =>
                            {
                                DirectoryInfo source = new DirectoryInfo(directoryPath);
                                DirectoryInfo target = new DirectoryInfo(toolStripTextBox1.Text);
                                MoveDirectories(source, target.CreateSubdirectory(source.Name));
                                DeleteDirectories(new DirectoryInfo(directoryPath));
                                Directory.Delete(directoryPath);
                                if (SetFileSystemItems(toolStripTextBox1.Text))
                                {
                                    ShowFileSystemItems();
                                    ShowPathInTree(toolStripTextBox1.Text);
                                }
                                filePath = null;
                                fileName = null;
                                directoryPath = null;

                                if (SetFileSystemItems(toolStripTextBox1.Text))
                                {
                                    ShowFileSystemItems();
                                    ShowPathInTree(toolStripTextBox1.Text);
                                }
                            }
                             );
                        }
                    }
                    catch (Exception e1)
                    {
                        MessageBox.Show(e1.Message);
                    }

                    IntPtr val = new IntPtr(1);
                    return val;
                }
                else if (ctrlDown && khs.VirtualKeyCode == 0x58)
                {
                    if (listView1.SelectedItems.Count < 1) return CallNextHookEx(hhook, nCode, (int)wParam, lParam);
                    ListView.SelectedListViewItemCollection items = listView1.SelectedItems;
                    ListViewItem item = items[0];
                    object file = item.Tag;
                    try
                    {
                        if (file is FileInfo)
                        {
                            ((FileInfo)file).IsReadOnly = false;
                            File.Delete(((FileInfo)file).FullName);
                            if (SetFileSystemItems(toolStripTextBox1.Text))
                            {
                                ShowFileSystemItems();
                                ShowPathInTree(toolStripTextBox1.Text);
                            }
                        }
                        else if (file is DirectoryInfo)
                        {
                            Task.Factory.StartNew(() =>
                            {
                                DeleteDirectories((DirectoryInfo)file);
                                ((DirectoryInfo)file).Delete();
                                if (SetFileSystemItems(toolStripTextBox1.Text))
                                {
                                    ShowFileSystemItems();
                                    ShowPathInTree(toolStripTextBox1.Text);
                                }
                            }
                            );
                        }
                    }
                    catch (Exception e1)
                    {
                        MessageBox.Show(e1.Message);
                    }

                    IntPtr val = new IntPtr(1);
                    return val;
                }
                else
                {
                    return CallNextHookEx(hhook, nCode, (int)wParam, lParam);
                }

            }
            else
            {
                return CallNextHookEx(hhook, nCode, (int)wParam, lParam);
            }
        }
        private void DeleteDirectories(DirectoryInfo path)
        {
            foreach (DirectoryInfo dir in path.GetDirectories())
            {
                DeleteDirectories(dir);
                dir.Delete();

            }
            foreach (FileInfo file in path.GetFiles())
            {
                file.IsReadOnly = false;
                file.Delete();
            }
        }

        private void CopyDirectories(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyDirectories(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        private void MoveDirectories(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                MoveDirectories(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
            {
                file.IsReadOnly = false;
                file.MoveTo(Path.Combine(target.FullName, file.Name));
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnHook();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            SearchForm form = new SearchForm(this);
            DialogResult result = form.ShowDialog();
            if (result == DialogResult.Cancel) return;
            try
            {
                Task.Factory.StartNew(() => {
                    Display(new DirectoryInfo(SearchPath));
                    OnSearchUpdate();
                });
            }
            catch (Exception e1) { MessageBox.Show(e1.Message); }
        }

      
        private void Display(DirectoryInfo path)
        {
            try
            {
                foreach (DirectoryInfo dir in path.GetDirectories())
                {
                    if (dir.Name.ToLower().Contains(SearchWord.ToLower())) matches.Add(dir);
                    Display(dir);
                }
                foreach (FileInfo file in path.GetFiles())
                {
                    if (file.Name.ToLower().Contains(SearchWord.ToLower())) matches.Add(file);
                }
            }
            catch (Exception e1) {  }
        }
        private void OnSearchUpdate()
        {
            try
            {
                DialogResult result = MessageBox.Show("Получены результаты поиска. Вывести их в пограмму?", "Поиск окончен", MessageBoxButtons.YesNo);
                if (result == DialogResult.No)
                {
                    matches.Clear();
                    return;
                }
                listView1.BeginUpdate();
                listView1.Items.Clear();

                foreach (var item in matches.ToList())
                {

                    SetColumns();

                    ListViewItem lviFile = new ListViewItem();
                    lviFile.Tag = item;
                    lviFile.Text = item.Name;


                    if (item is DirectoryInfo)
                    {
                        lviFile.ImageIndex = 3;
                        lviFile.SubItems.Add("Каталог");
                        lviFile.SubItems.Add("");

                    }

                    else if (item is FileInfo)
                    {
                        FileInfo f = (FileInfo)item;
                        lviFile.ImageIndex = 5;
                        lviFile.SubItems.Add("Файл");
                        lviFile.SubItems.Add(String.Format("{0:F2} Mб", (double)f.Length / Math.Pow(1000, 2))
    );
                    }
                    lviFile.SubItems.Add(item.CreationTime.ToString());
                    lviFile.SubItems.Add(item.LastWriteTime.ToString());

                    listView1.Items.Add(lviFile);

                    listView1.EndUpdate();
                    matches.Clear();
                }
            }

            catch (Exception e1) { MessageBox.Show(e1.Message); }

        }
    }
}

