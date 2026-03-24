using RailCAD.CadInterface;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace RailCAD.Views
{
    /// <summary>
    /// Licence activation dialog for entering activation code and licence key
    /// </summary>
    internal partial class LicenceActivationDialog : Window
    {
        /// <summary>
        /// Gets the entered licence key from the dialog
        /// </summary>
        internal string LicenceKey { get; private set; }

        /// <summary>
        /// Gets or sets the activation code displayed to the user
        /// </summary>
        public string ActivationCode { get; set; }

        /// <summary>
        /// Initializes a new instance of the LicenceActivationDialog
        /// </summary>
        /// <param name="activationCode">The activation code to display</param>
        internal LicenceActivationDialog(string activationCode)
        {
            InitializeComponent();
            ActivationCode = activationCode;
            DataContext = this;
        }

        /// <summary>
        /// Handles the Copy Code button click event
        /// </summary>
        private void CopyCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ActivationCode);
                MessageBox.Show(Properties.Resources.LicenceCodeCopied, Properties.Resources.Information, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CadModel.WriteMessageStatic($"Error while copying: {ex.Message}");
                MessageBox.Show(Properties.Resources.Error, Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the Open Website button click event
        /// Opens the licence activation website in the default browser
        /// </summary>
        private void OpenWebsite_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://licence.railcad.cz/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CadModel.WriteMessageStatic($"Error while openning the web: {ex.Message}");
                MessageBox.Show(Properties.Resources.LicenceCannotOpenWebPleaseOpenManually,
                    Properties.Resources.Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handles the OK button click event
        /// </summary>
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            LicenceKey = LicenceKeyTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(LicenceKey))
            {
                MessageBox.Show(Properties.Resources.LicencePleaseEnterLicenceKey, Properties.Resources.MissingValue, MessageBoxButton.OK, MessageBoxImage.Warning);
                LicenceKeyTextBox.Focus();
                return;
            }
            else if (ActivationCode == LicenceKey)
            {
                MessageBox.Show(Properties.Resources.LicenceActivationCodeInsertIntoFormOnWeb, Properties.Resources.MissingValue, MessageBoxButton.OK, MessageBoxImage.Warning);
                LicenceKeyTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click event
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Handles text changed event to auto-resize the activation code TextBox
        /// </summary>
        private void ActivationCodeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                // Force measure to update the layout
                textBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }
        }
    }
}
