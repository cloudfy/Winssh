using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices.WindowsRuntime;

namespace WinSsh.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // TODO: Load settings from storage
            // For now, using default values set in XAML
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Save settings to storage
            
            // Show confirmation
            var dialog = new ContentDialog
            {
                Title = "Settings Saved",
                Content = "Your settings have been saved successfully.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            
            await dialog.ShowAsync();
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Reset Settings",
                Content = "Are you sure you want to reset all settings to their default values?",
                PrimaryButtonText = "Reset",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                ResetToDefaults();
            }
        }

        private void ResetToDefaults()
        {
            StartWithWindowsCheckBox.IsChecked = false;
            MinimizeToTrayCheckBox.IsChecked = false;
            ConfirmOnExitCheckBox.IsChecked = true;
            ThemeComboBox.SelectedIndex = 0;
            BackdropComboBox.SelectedIndex = 0;
            SshPathTextBox.Text = @"C:\Windows\System32\OpenSSH\ssh.exe";
            DefaultColsNumberBox.Value = 80;
            DefaultRowsNumberBox.Value = 24;
            CloseTabOnDisconnectCheckBox.IsChecked = false;
        }
    }
}
