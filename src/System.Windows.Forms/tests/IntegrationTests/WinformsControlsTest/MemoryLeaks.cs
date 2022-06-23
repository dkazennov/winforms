// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;

namespace WinformsControlsTest
{
    public partial class StartForm : Form
    {
        private long _allocated;

        public StartForm()
        {
            InitializeComponent();
        }

        private void Scenario()
        {
            using EmptyComboForm testForm = new();
            testForm.ShowDialog(this);
        }

        private void CleanupScenario()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            long temp = _allocated;
            _allocated = GC.GetTotalMemory(true);
            temp = _allocated - temp;

            Text = temp.ToString();
        }

        private void ShowFormButton_Click(object sender, EventArgs e)
        {
            Scenario();
        }

        private void CollectButton_Click(object sender, EventArgs e)
        {
            CleanupScenario();
        }

    }

    public partial class EmptyComboForm : Form
    {
        public EmptyComboForm()
        {
            InitializeComponent();
            // This form and the folowing methods are not to be included in a PR or to be released.
            // The following code is for test purpose of issue 7329 only
            // and doesn't follow code style rules for the sake of convience.

            // Use one control at a time.
            // AddAndTestButton();
            AddCheckBox();
            // AddComboBox();
            // AddDateTimePicker();
            // AddGroupBox();
            // AddPropertyGrid();
            // AddTabControlAndTabPages();
            // AddMenuStrip();
            // AddStatusStrip();
            // AddHScrollBar();
            // AddVScrollBar();
            // AddLabel();
            // AddLinkLabel();
            // AddListView();
            // AddProgressBar();
            // AddRichTextBox();
            // AddMaskedTextBox();
            // AddTrackBar();
            // AddCheckedListBox();
            // AddRadioButton();
            // AddToolStrip();
            // ShowThreadExceptionDialog();
            // AddPanel();
            // AddTableLayoutPanel();
            // AddContextMenuStrip();
            // AddSplitter();
            // AddToolStripOverflow();
            // AddPictureBox();
            // AddWebBrowser();
            // AddControlForTesting();
        }

        private void AddAndTestButton()
        {
            AddButton();
        }

        private void AddControlForTesting()
        {
            PrintPreviewControl control = new();
            Controls.Add(control);
        }

        private void AddCheckBox()
        {
            CheckBox control = new();
            Controls.Add(control);
        }

        private void AddWebBrowser()
        {
            textBox1 = new TextBox();
            webBrowser1 = new WebBrowser();
            Label label1 = new();
            Button button1 = new();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(19, 14);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(42, 20);
            label1.TabIndex = 0;
            label1.Text = "URL :";
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(67, 11);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(200, 27);
            textBox1.TabIndex = 1;
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(210, 10);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(94, 29);
            button1.TabIndex = 3;
            button1.Text = "GO";
            button1.UseVisualStyleBackColor = true;
            button1.Click += new System.EventHandler(browserButton_Click);
            // 
            // webBrowser1
            // 
            webBrowser1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            webBrowser1.Location = new System.Drawing.Point(5, 45);
            webBrowser1.Margin = new System.Windows.Forms.Padding(5, 3, 5, 3);
            webBrowser1.MinimumSize = new System.Drawing.Size(27, 27);
            webBrowser1.Name = "webBrowser1";
            webBrowser1.Size = new System.Drawing.Size(791, 401);
            webBrowser1.TabIndex = 4;

            Controls.Add(button1);
            Controls.Add(textBox1);
            Controls.Add(label1);
            Controls.Add(webBrowser1);
        }

        private void AddPictureBox()
        {
            PictureBox pictureBox1 = new();
            pictureBox1.Location = new System.Drawing.Point(427, 88);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(125, 62);
            pictureBox1.TabIndex = 1;
            pictureBox1.TabStop = false;
            Controls.Add(pictureBox1);
        }

        private void AddToolStripOverflow()
        {
            ToolStrip toolStrip = AddToolStrip();
            ToolStripMenuItem menuItem = GetToolStripMenuItem("cut");
            ToolStripOverflow overflow = new(menuItem);
            toolStrip.Items.Add(menuItem);
        }

        private void AddSplitterContainer()
        {
            // 
            // splitContainer1
            //
            SplitContainer splitContainer1 = new();
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new System.Drawing.Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Paint += new PaintEventHandler(splitContainer1_Panel2_Paint);
            splitContainer1.Size = new System.Drawing.Size(800, 450);
            splitContainer1.SplitterDistance = 266;
            splitContainer1.TabIndex = 1;
            Controls.Add(splitContainer1);
        }

        private void AddSplitter()
        {
            // Control crashes in .Net 7
            TreeView treeView1 = new TreeView();
            ListView listView1 = new ListView();
            Splitter splitter1 = new Splitter();

            treeView1.Dock = DockStyle.Left;
            splitter1.Dock = DockStyle.Left;
            listView1.Dock = DockStyle.Fill;

            splitter1.BackColor = System.Drawing.Color.Red;
            splitter1.Location = new System.Drawing.Point(120, 0);
            splitter1.Size = new System.Drawing.Size(8, 237);
            splitter1.TabIndex = 1;
            splitter1.TabStop = false;

            treeView1.Nodes.Add("TreeView Node");
            listView1.Items.Add("ListView Item");

            Controls.AddRange(new Control[] { listView1, splitter1, treeView1 });
        }

        private void AddContextMenuStrip()
        {
            components = new System.ComponentModel.Container();
            ContextMenuStrip contextMenuStrip1 = new(components);
            ToolStripMenuItem toolStripMenuItem1 = new ToolStripMenuItem();

            contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new System.Drawing.Size(211, 32);

            contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[]
            {
                toolStripMenuItem1
            });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new System.Drawing.Size(143, 28);

            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new System.Drawing.Size(142, 24);
            toolStripMenuItem1.Text = "New item";
            toolStripMenuItem1.Click += new EventHandler(toolStripMenuItem1_Click);

            ContextMenuStrip = contextMenuStrip1;
        }

        private void AddToolStripContentPanel()
        {
            ToolStripContentPanel control = new();
            control.Controls.Add(AddButton());
            Controls.Add(control);
        }

        private void AddTableLayoutPanel()
        {
            TableLayoutPanel control = new();
            control.Controls.Add(AddButton());
            Controls.Add(control);
        }

        private void AddPanel()
        {
            Panel control = new Panel { BorderStyle = BorderStyle.Fixed3D };
            control.Controls.Add(AddButton());
            Controls.Add(control);
        }

        private void AddSplitContainer()
        {
            SplitContainer splitContainer = new();
            Controls.Add(splitContainer);
        }

        private void ShowThreadExceptionDialog()
        {
            ThreadExceptionDialog dialog = new(new Exception("Really long exception description string, because we want to see if it properly wraps around or is truncated."));
            dialog.ShowDialog(this);
        }

        private void AddComboBox()
        {
            comboBox = new ComboBox()
            {
                Text = "CheckBox",
                Location = new(20, 20)
            };   
            ListBox lb = new();
            comboBox.Items.Add(1);
            comboBox.Items.Add(2);
            comboBox.Items.Add(3);
            comboBox.Items.Add(4);
            Controls.Add(comboBox);
            Button removeButton = AddButton();
            removeButton.AccessibleName = "Remove item";
            removeButton.Name = "removeButton";
            removeButton.Text = "Remove last item";
            removeButton.Click += new EventHandler(RemoveButton_Click);
        }

        private Button AddButton()
        {
            Button button = new Button();
            button.AccessibleName = "Button item";
            button.Location = new System.Drawing.Point(44, 50);
            button.Name = "A button";
            button.Size = new System.Drawing.Size(75, 23);
            button.TabIndex = 1;
            button.Text = "Do nothing";
            button.UseVisualStyleBackColor = true;
            Controls.Add(button);
            return button;
        }

        private void AddRadioButton()
        {
            RadioButton radioButton = new RadioButton();
            Controls.Add(radioButton);
        }

        private void AddCheckedListBox()
        {
            CheckedListBox checkedListBox = new CheckedListBox();
            checkedListBox.Size = new System.Drawing.Size(120, 100);
            checkedListBox.Items.Add("a");
            checkedListBox.Items.Add("b");
            checkedListBox.Items.Add("c");
            checkedListBox.Items.Add("d");
            checkedListBox.Items.Add("e");
            checkedListBox.Items.Add("f");
            checkedListBox.Items.Add("g");
            checkedListBox.Items.Add("h");
            checkedListBox.Items.Add("i");
            Controls.Add(checkedListBox);
        }

        private void AddDateTimePicker()
        {
            System.Windows.Forms.DateTimePicker dateTimePicker = new();
            dateTimePicker.Value = DateTime.Now;
            Controls.Add(dateTimePicker);
        }

        private void AddGroupBox()
        {
            GroupBox groupBox = new GroupBox();
            Controls.Add(groupBox);
        }

        private void AddListView()
        {
            ListView listView = new ListView();
            var group = new ListViewGroup($"Group 1", HorizontalAlignment.Left) { CollapsedState = ListViewGroupCollapsedState.Expanded };
            var item1 = new ListViewItem("g1-1") { Group = group };
            var item2 = new ListViewItem("g1-2") { Group = group };
            var item3 = new ListViewItem("g1-3") { Group = group };

            listView.Groups.Add(group);
            listView.Items.AddRange(new[] { item1, item2, item3 });
            Controls.Add(listView);
        }

        private void AddMaskedTextBox()
        {
            MaskedTextBox maskedTextBox = new MaskedTextBox();
            Controls.Add(maskedTextBox);
        }

        private void AddProgressBar()
        {
            ProgressBar progressBar = new ProgressBar();
            Controls.Add(progressBar);
        }

        private void AddPropertyGrid()
        {
            System.Windows.Forms.PropertyGrid propertyGrid = new System.Windows.Forms.PropertyGrid();
            propertyGrid.Location = new System.Drawing.Point(15, 15);
            propertyGrid.Name = "propertyGrid";
            propertyGrid.Size = new System.Drawing.Size(200, 350);
            propertyGrid.TabIndex = 0;
            Controls.Add(propertyGrid);
        }

        private void AddTrackBar()
        {
            TrackBar trackBar = new TrackBar();
            Controls.Add(trackBar);
        }

        private void AddHScrollBar()
        {
            HScrollBar scrollBar = new();
            Controls.Add(scrollBar);
        }

        private void AddRichTextBox()
        {
            RichTextBox richTextBox = new RichTextBox();
            Controls.Add(richTextBox);
        }

        private void AddVScrollBar()
        {
            VScrollBar scrollBar = new();
            Controls.Add(scrollBar);
        }

        private void AddLabel()
        {
            Label label = new Label();
            label.Text = "TEST LABEL";
            Controls.Add(label);
        }

        private void AddLinkLabel()
        {
            linkLabel = new LinkLabel();
            linkLabel.Location = new System.Drawing.Point(15, 15);
            linkLabel.LinkClicked += new LinkLabelLinkClickedEventHandler(linkLabel_LinkClicked);
            linkLabel.Text = "Visit link";
            linkLabel.LinkColor = System.Drawing.Color.DarkBlue;
            Controls.Add(linkLabel);
        }

        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            linkLabel.LinkVisited = true;
            try
            {
                System.Diagnostics.Process.Start("http://www.microsoft.com");
            }
            catch (Exception exception)
            {
                
            }
        }

        private void AddStatusStrip()
        {
            StatusStrip statusStrip = new StatusStrip
            {
                BackColor = System.Drawing.Color.Blue,
                SizingGrip = false,
                Renderer = new ToolStripProfessionalRenderer(new ProfessionalColorTable()),
                Size = new System.Drawing.Size(200, 38),
                Location = new System.Drawing.Point(0, 0)
            };
            Controls.Add(statusStrip);
        }

        private void AddTabControlAndTabPages()
        {
            TabControl tabControl = new TabControl();
            TabPage page1 = new TabPage
            {
                Text = "TestText1",
                ImageIndex = 2
            };
            TabPage page2 = new TabPage
            {
                Text = "TestText2",
                ImageIndex = 2
            };
            tabControl.Controls.Add(page1);
            tabControl.Controls.Add(page2);
            Controls.Add(tabControl);
        }

        private ToolStrip AddToolStrip()
        {
            ToolStripMenuItem newToolStripMenuItem = GetToolStripMenuItem("new");
            ToolStripMenuItem editToolStripMenuItem = GetToolStripMenuItem("edit");

            toolStrip = new ToolStrip();
            toolStrip.Location = new System.Drawing.Point(0, 0);
            toolStrip.Name = "menuStrip";
            toolStrip.Padding = new Padding(12, 4, 0, 4);
            toolStrip.Size = new System.Drawing.Size(736, 46);
            toolStrip.TabIndex = 0;
            toolStrip.Items.AddRange(new ToolStripItem[]
            {
                newToolStripMenuItem,
                editToolStripMenuItem
            });
            toolStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            Controls.Add(toolStrip);
            return toolStrip;
        }

        private MenuStrip AddMenuStrip()
        {
            ToolStripMenuItem newToolStripMenuItem = GetToolStripMenuItem("new");
            ToolStripMenuItem editToolStripMenuItem = GetToolStripMenuItem("edit");

            menuStrip = new();
            menuStrip.Location = new System.Drawing.Point(0, 0);
            menuStrip.Name = "menuStrip";
            menuStrip.Padding = new Padding(12, 4, 0, 4);
            menuStrip.Size = new System.Drawing.Size(736, 46);
            menuStrip.TabIndex = 0;
            menuStrip.Items.AddRange(new ToolStripItem[]
            {
                newToolStripMenuItem,
                editToolStripMenuItem
            });
            menuStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            Controls.Add(menuStrip);
            MainMenuStrip = menuStrip;
            return menuStrip;
        }

        private ToolStripMenuItem GetToolStripMenuItem(string text)
        {
            ToolStripMenuItem toolStripMenuItem = new();
            toolStripMenuItem.Name = text + "ToolStripMenuItem";
            toolStripMenuItem.Size = new System.Drawing.Size(75, 38);
            toolStripMenuItem.Text = text;
            return toolStripMenuItem;
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {

        }

        private void splitContainer1_Panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void browserButton_Click(object sender, EventArgs e)
        {
            webBrowser1.Navigate(textBox1.Text);
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            comboBox.Items.RemoveAt(comboBox.Items.Count - 1);
        }

        LinkLabel linkLabel;
        ComboBox comboBox;
        ToolStrip toolStrip;
        MenuStrip menuStrip;
        private TextBox textBox1;
        private WebBrowser webBrowser1;
    }
}
