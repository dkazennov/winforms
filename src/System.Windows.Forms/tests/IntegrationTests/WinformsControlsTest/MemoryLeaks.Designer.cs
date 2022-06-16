// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WinformsControlsTest
{
    partial class StartForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.showFormButton = new System.Windows.Forms.Button();
            this.collectButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // showFormButton
            // 
            this.showFormButton.AccessibleName = "ShowForm";
            this.showFormButton.Location = new System.Drawing.Point(44, 30);
            this.showFormButton.Name = "showFormButton";
            this.showFormButton.Size = new System.Drawing.Size(99, 23);
            this.showFormButton.TabIndex = 0;
            this.showFormButton.Text = "Show Form";
            this.showFormButton.UseVisualStyleBackColor = true;
            this.showFormButton.Click += new System.EventHandler(this.ShowFormButton_Click);
            // 
            // collectButton
            // 
            this.collectButton.AccessibleName = "GCCollect";
            this.collectButton.Location = new System.Drawing.Point(44, 80);
            this.collectButton.Name = "collectButton";
            this.collectButton.Size = new System.Drawing.Size(75, 23);
            this.collectButton.TabIndex = 1;
            this.collectButton.Text = "GC.Collect";
            this.collectButton.UseVisualStyleBackColor = true;
            this.collectButton.Click += new System.EventHandler(this.CollectButton_Click);
            
            // 
            // StartForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(240, 204);
            this.Controls.Add(this.collectButton);
            this.Controls.Add(this.showFormButton);
            this.Name = "StartForm";
            this.Text = "StartForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button showFormButton;
        private System.Windows.Forms.Button collectButton;
    }

    partial class EmptyComboForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.removeButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // comboBox1
            // 
            this.propertyGrid.Location = new System.Drawing.Point(15, 15);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.Size = new System.Drawing.Size(200, 350);
            this.propertyGrid.TabIndex = 0;
            // 
            // removeButton
            // 
            this.removeButton.AccessibleName = "Remove item";
            this.removeButton.Location = new System.Drawing.Point(44, 50);
            this.removeButton.Name = "removeButton";
            this.removeButton.Size = new System.Drawing.Size(75, 23);
            this.removeButton.TabIndex = 1;
            this.removeButton.Text = "Remove last item";
            this.removeButton.UseVisualStyleBackColor = true;
            this.removeButton.Click += new System.EventHandler(this.RemoveButton_Click);
            // 
            // EmptyComboForm
            // 
            this.AccessibleName = "testForm";
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(346, 401);
            //this.Controls.Add(this.propertyGrid);
            this.Controls.Add(this.removeButton);
            this.Name = "EmptyComboForm";
            this.Text = "EmptyComboForm";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PropertyGrid propertyGrid;
        private System.Windows.Forms.Button removeButton;
    }
}

