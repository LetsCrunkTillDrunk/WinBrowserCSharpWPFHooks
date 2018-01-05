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

namespace WinBrowser
{
    public partial class SearchForm : Form
    {
        private Form1 form;
        public SearchForm(Form1 f)
        {
            InitializeComponent();
            form = f;
        }

        private void SearchForm_Load(object sender, EventArgs e)
        {
            DriveInfo[] disks = DriveInfo.GetDrives();
            foreach (var d in disks)
            {
                comboBox1.Items.Add(d.Name);
            }
            comboBox1.SelectedIndex = 0;
            textBox1.Text = comboBox1.Items[0].ToString();
        }

        private void comboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            textBox1.Text = comboBox1.SelectedItem.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == String.Empty || textBox2.Text == String.Empty) return;
            form.SearchPath = textBox1.Text;
            form.SearchWord = textBox2.Text;
            this.DialogResult = DialogResult.OK;
        }
    }
}
