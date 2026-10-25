namespace CustomerEntry
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            newToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            exitToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            customerGroupBox = new GroupBox();
            nameLabel = new Label();
            nameTextBox = new TextBox();
            emailLabel = new Label();
            emailTextBox = new TextBox();
            countryLabel = new Label();
            countryComboBox = new ComboBox();
            ageLabel = new Label();
            ageUpDown = new NumericUpDown();
            newsletterCheckBox = new CheckBox();
            addButton = new Button();
            clearButton = new Button();
            customersGrid = new DataGridView();
            nameColumn = new DataGridViewTextBoxColumn();
            emailColumn = new DataGridViewTextBoxColumn();
            countryColumn = new DataGridViewTextBoxColumn();
            ageColumn = new DataGridViewTextBoxColumn();
            newsletterColumn = new DataGridViewTextBoxColumn();
            removeButton = new Button();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            toolTip1 = new ToolTip(components);
            menuStrip1.SuspendLayout();
            customerGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)ageUpDown).BeginInit();
            ((System.ComponentModel.ISupportInitialize)customersGrid).BeginInit();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(784, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { newToolStripMenuItem, toolStripSeparator1, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            // 
            // newToolStripMenuItem
            // 
            newToolStripMenuItem.Name = "newToolStripMenuItem";
            newToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newToolStripMenuItem.Size = new Size(141, 22);
            newToolStripMenuItem.Text = "&New";
            newToolStripMenuItem.Click += newToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(138, 6);
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(141, 22);
            exitToolStripMenuItem.Text = "E&xit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(44, 20);
            helpToolStripMenuItem.Text = "&Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(107, 22);
            aboutToolStripMenuItem.Text = "&About";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // customerGroupBox
            // 
            customerGroupBox.Controls.Add(addButton);
            customerGroupBox.Controls.Add(clearButton);
            customerGroupBox.Controls.Add(newsletterCheckBox);
            customerGroupBox.Controls.Add(ageUpDown);
            customerGroupBox.Controls.Add(ageLabel);
            customerGroupBox.Controls.Add(countryComboBox);
            customerGroupBox.Controls.Add(countryLabel);
            customerGroupBox.Controls.Add(emailTextBox);
            customerGroupBox.Controls.Add(emailLabel);
            customerGroupBox.Controls.Add(nameTextBox);
            customerGroupBox.Controls.Add(nameLabel);
            customerGroupBox.Location = new Point(12, 35);
            customerGroupBox.Name = "customerGroupBox";
            customerGroupBox.Size = new Size(300, 260);
            customerGroupBox.TabIndex = 1;
            customerGroupBox.TabStop = false;
            customerGroupBox.Text = "New customer";
            // 
            // nameLabel
            // 
            nameLabel.AutoSize = true;
            nameLabel.Location = new Point(15, 30);
            nameLabel.Name = "nameLabel";
            nameLabel.Size = new Size(42, 15);
            nameLabel.TabIndex = 0;
            nameLabel.Text = "Name:";
            // 
            // nameTextBox
            // 
            nameTextBox.Location = new Point(90, 27);
            nameTextBox.Name = "nameTextBox";
            nameTextBox.PlaceholderText = "Full name";
            nameTextBox.Size = new Size(190, 23);
            nameTextBox.TabIndex = 1;
            // 
            // emailLabel
            // 
            emailLabel.AutoSize = true;
            emailLabel.Location = new Point(15, 63);
            emailLabel.Name = "emailLabel";
            emailLabel.Size = new Size(39, 15);
            emailLabel.TabIndex = 2;
            emailLabel.Text = "Email:";
            // 
            // emailTextBox
            // 
            emailTextBox.Location = new Point(90, 60);
            emailTextBox.Name = "emailTextBox";
            emailTextBox.Size = new Size(190, 23);
            emailTextBox.TabIndex = 3;
            // 
            // countryLabel
            // 
            countryLabel.AutoSize = true;
            countryLabel.Location = new Point(15, 96);
            countryLabel.Name = "countryLabel";
            countryLabel.Size = new Size(53, 15);
            countryLabel.TabIndex = 4;
            countryLabel.Text = "Country:";
            // 
            // countryComboBox
            // 
            countryComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            countryComboBox.FormattingEnabled = true;
            countryComboBox.Location = new Point(90, 93);
            countryComboBox.Name = "countryComboBox";
            countryComboBox.Size = new Size(190, 23);
            countryComboBox.TabIndex = 5;
            // 
            // ageLabel
            // 
            ageLabel.AutoSize = true;
            ageLabel.Location = new Point(15, 129);
            ageLabel.Name = "ageLabel";
            ageLabel.Size = new Size(31, 15);
            ageLabel.TabIndex = 6;
            ageLabel.Text = "Age:";
            // 
            // ageUpDown
            // 
            ageUpDown.Location = new Point(90, 127);
            ageUpDown.Maximum = new decimal(new int[] { 120, 0, 0, 0 });
            ageUpDown.Minimum = new decimal(new int[] { 18, 0, 0, 0 });
            ageUpDown.Name = "ageUpDown";
            ageUpDown.Size = new Size(80, 23);
            ageUpDown.TabIndex = 7;
            ageUpDown.Value = new decimal(new int[] { 30, 0, 0, 0 });
            // 
            // newsletterCheckBox
            // 
            newsletterCheckBox.AutoSize = true;
            newsletterCheckBox.Location = new Point(90, 162);
            newsletterCheckBox.Name = "newsletterCheckBox";
            newsletterCheckBox.Size = new Size(141, 19);
            newsletterCheckBox.TabIndex = 8;
            newsletterCheckBox.Text = "Send the newsletter";
            newsletterCheckBox.UseVisualStyleBackColor = true;
            // 
            // addButton
            // 
            addButton.Location = new Point(90, 205);
            addButton.Name = "addButton";
            addButton.Size = new Size(90, 30);
            addButton.TabIndex = 9;
            addButton.Text = "&Add";
            toolTip1.SetToolTip(addButton, "Add the customer to the list");
            addButton.UseVisualStyleBackColor = true;
            addButton.Click += addButton_Click;
            // 
            // clearButton
            // 
            clearButton.Location = new Point(190, 205);
            clearButton.Name = "clearButton";
            clearButton.Size = new Size(90, 30);
            clearButton.TabIndex = 10;
            clearButton.Text = "&Clear";
            clearButton.UseVisualStyleBackColor = true;
            clearButton.Click += clearButton_Click;
            // 
            // customersGrid
            // 
            customersGrid.AllowUserToAddRows = false;
            customersGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            customersGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            customersGrid.Columns.AddRange(new DataGridViewColumn[] { nameColumn, emailColumn, countryColumn, ageColumn, newsletterColumn });
            customersGrid.Location = new Point(330, 40);
            customersGrid.Name = "customersGrid";
            customersGrid.ReadOnly = true;
            customersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            customersGrid.Size = new Size(442, 330);
            customersGrid.TabIndex = 2;
            customersGrid.SelectionChanged += customersGrid_SelectionChanged;
            // 
            // nameColumn
            // 
            nameColumn.HeaderText = "Name";
            nameColumn.Name = "nameColumn";
            nameColumn.Width = 120;
            // 
            // emailColumn
            // 
            emailColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            emailColumn.HeaderText = "Email";
            emailColumn.Name = "emailColumn";
            // 
            // countryColumn
            // 
            countryColumn.HeaderText = "Country";
            countryColumn.Name = "countryColumn";
            // 
            // ageColumn
            // 
            ageColumn.HeaderText = "Age";
            ageColumn.Name = "ageColumn";
            ageColumn.Width = 50;
            // 
            // newsletterColumn
            // 
            newsletterColumn.HeaderText = "Newsletter";
            newsletterColumn.Name = "newsletterColumn";
            newsletterColumn.Width = 80;
            // 
            // removeButton
            // 
            removeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            removeButton.Enabled = false;
            removeButton.Location = new Point(652, 380);
            removeButton.Name = "removeButton";
            removeButton.Size = new Size(120, 30);
            removeButton.TabIndex = 3;
            removeButton.Text = "&Remove selected";
            removeButton.UseVisualStyleBackColor = true;
            removeButton.Click += removeButton_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip1.Location = new Point(0, 419);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(784, 22);
            statusStrip1.TabIndex = 4;
            statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(39, 17);
            statusLabel.Text = "Ready";
            // 
            // MainForm
            // 
            AcceptButton = addButton;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 441);
            Controls.Add(statusStrip1);
            Controls.Add(removeButton);
            Controls.Add(customersGrid);
            Controls.Add(customerGroupBox);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(700, 400);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Customer Entry";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            customerGroupBox.ResumeLayout(false);
            customerGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)ageUpDown).EndInit();
            ((System.ComponentModel.ISupportInitialize)customersGrid).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem newToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private GroupBox customerGroupBox;
        private Label nameLabel;
        private TextBox nameTextBox;
        private Label emailLabel;
        private TextBox emailTextBox;
        private Label countryLabel;
        private ComboBox countryComboBox;
        private Label ageLabel;
        private NumericUpDown ageUpDown;
        private CheckBox newsletterCheckBox;
        private Button addButton;
        private Button clearButton;
        private DataGridView customersGrid;
        private DataGridViewTextBoxColumn nameColumn;
        private DataGridViewTextBoxColumn emailColumn;
        private DataGridViewTextBoxColumn countryColumn;
        private DataGridViewTextBoxColumn ageColumn;
        private DataGridViewTextBoxColumn newsletterColumn;
        private Button removeButton;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private ToolTip toolTip1;
    }
}
