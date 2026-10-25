using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using CustomerEntry.Porting;
namespace CustomerEntry
{
    /// <summary>Enters customers into a list, with a menu, a grid and a status bar.</summary>
    public partial class MainForm : AppForm
    {
        private static readonly string[] Countries = ["Israel", "United Kingdom", "United States", "Germany", "Japan"];

        private bool _unsaved;

        public MainForm()
        {
            InitializeComponent();
            InitializePortedEvents();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            FormsItems.AddRange(countryComboBox.Items,Countries);
            countryComboBox.SelectedIndex = 0;
            UpdateStatus();
        }

        private async void addButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(nameTextBox.Text))
            {
                await MessageBox.ShowAsync("Please enter the customer's name.", "Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nameTextBox.Focus();
                return;
            }

            customersGridRows.Add(
                nameTextBox.Text.Trim(),
                emailTextBox.Text.Trim(),
                countryComboBox.SelectedItem,
                (int)(ageUpDown.Value??0),
                (newsletterCheckBox.IsChecked==true) ? "Yes" : "No");
            _unsaved = true;
            ClearFields();
            UpdateStatus();
        }

        private void clearButton_Click(object sender, EventArgs e) => ClearFields();

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            customersGridRows.Clear();
            _unsaved = false;
            UpdateStatus();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e) => Close();

        private async void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await MessageBox.ShowAsync("Customer Entry 2.0\nA small Windows Forms sample.", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void customersGrid_SelectionChanged(object sender, EventArgs e)
        {
            removeButton.IsEnabled = customersGrid.SelectedItems.Cast<GridRow>().ToList().Count > 0;
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            foreach (GridRow row in customersGrid.SelectedItems.Cast<GridRow>().ToList())
            {
                customersGridRows.Remove(row);
            }

            _unsaved = true;
            UpdateStatus();
        }

        private async System.Threading.Tasks.Task MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_unsaved && await MessageBox.ShowAsync("Close without saving the customers you added?", "Customers", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        private void ClearFields()
        {
            nameTextBox.Clear();
            emailTextBox.Clear();
            ageUpDown.Value = 30;
            newsletterCheckBox.IsChecked = false;
            nameTextBox.Focus();
        }

        private void UpdateStatus()
        {
            statusLabel.Text = customersGridRows.Count == 1 ? "1 customer" : $"{customersGridRows.Count} customers";
        }
    }
}
