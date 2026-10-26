namespace CustomerEntry
{
    /// <summary>Enters customers into a list, with a menu, a grid and a status bar.</summary>
    public partial class MainForm : Form
    {
        private static readonly string[] Countries = ["Israel", "United Kingdom", "United States", "Germany", "Japan"];

        private bool _unsaved;

        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            countryComboBox.Items.AddRange(Countries);
            countryComboBox.SelectedIndex = 0;
            UpdateStatus();
        }

        private void addButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                MessageBox.Show("Please enter the customer's name.", "Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameTextBox.Focus();
                return;
            }

            customersGrid.Rows.Add(
                nameTextBox.Text.Trim(),
                emailTextBox.Text.Trim(),
                countryComboBox.SelectedItem,
                (int)ageUpDown.Value,
                newsletterCheckBox.Checked ? "Yes" : "No");
            _unsaved = true;
            ClearFields();
            UpdateStatus();
        }

        private void clearButton_Click(object sender, EventArgs e) => ClearFields();

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            customersGrid.Rows.Clear();
            _unsaved = false;
            UpdateStatus();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Customer Entry 2.0\nA small Windows Forms sample.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void customersGrid_SelectionChanged(object sender, EventArgs e)
        {
            removeButton.Enabled = customersGrid.SelectedRows.Count > 0;
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in customersGrid.SelectedRows)
            {
                customersGrid.Rows.Remove(row);
            }

            _unsaved = true;
            UpdateStatus();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_unsaved && MessageBox.Show("Close without saving the customers you added?", "Customers", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        private void ClearFields()
        {
            nameTextBox.Clear();
            emailTextBox.Clear();
            ageUpDown.Value = 30;
            newsletterCheckBox.Checked = false;
            nameTextBox.Focus();
        }

        private void UpdateStatus()
        {
            statusLabel.Text = customersGrid.Rows.Count == 1 ? "1 customer" : $"{customersGrid.Rows.Count} customers";
        }
    }
}
