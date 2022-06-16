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
            //Controls.Add(new TextBox() { Text = "TextBox" });
            //Controls.Add(new VScrollBar() { Value = 30, Location = new(20, 20) });
            comboBox = new ComboBox() { Text = "CheckBox", Location = new(20, 20) };
            Controls.Add(comboBox);
            ListBox lb = new();
            comboBox.Items.Add(1);
            comboBox.Items.Add(2);
            comboBox.Items.Add(3);
            comboBox.Items.Add(4);
            //Controls.Add(new VScrollBar());
            //Controls.Add(new MonthCalendar());
            //propertyGrid.SelectedObject = new Button();
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            comboBox.Items.RemoveAt(comboBox.Items.Count - 1);
        }

        ComboBox comboBox;
    }
}
