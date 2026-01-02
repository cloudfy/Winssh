using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinSsh.Profiles;

namespace WinSsh.Views;

public sealed partial class ProfileDialog : ContentDialog
{
    public HostProfile? Profile { get; private set; }

    private readonly IntPtr _hwnd;
    private readonly List<HostProfile> _allProfiles;
    private readonly bool _editMode = false;

    public ProfileDialog(HostProfile? profile, IntPtr hwnd, List<HostProfile> allProfiles)
    {
        InitializeComponent();
        Profile = profile;
        if (profile is not null)
            _editMode = true;
        _hwnd = hwnd;
        _allProfiles = allProfiles;

        NameBox.Text = profile?.Name;
        HostBox.Text = profile?.Host;
        UserBox.Text = profile?.Username;
        PortBox.Text = profile?.Port.ToString();
        KeyBox.Text = profile?.IdentityFile ?? "";
        ExtraArgsBox.Text = profile?.ExtraArgs ?? "";

        TtyCheck.IsChecked = profile?.RequestTty;
        AgentFwdCheck.IsChecked = profile?.AgentForwarding;

        // Select host key checking
        var value = string.IsNullOrWhiteSpace(profile?.HostKeyChecking) ? "accept-new" : profile.HostKeyChecking;
        foreach (ComboBoxItem item in HostKeyCheckBox.Items)
        {
            if ((item.Content?.ToString() ?? "") == value)
            {
                HostKeyCheckBox.SelectedItem = item;
                break;
            }
        }

        // Populate jump host dropdown
        PopulateJumpHostComboBox(profile);

        PrimaryButtonClick += OnPrimary;
    }

    private void PopulateJumpHostComboBox(HostProfile? currentProfile)
    {
        JumpHostComboBox.Items.Clear();

        // Add "None" option
        var noneItem = new ComboBoxItem { Content = "None", Tag = null };
        JumpHostComboBox.Items.Add(noneItem);

        // Add all profiles except the current one (to avoid circular references)
        foreach (var p in _allProfiles.Where(p => p.Id != currentProfile?.Id))
        {
            var item = new ComboBoxItem
            {
                Content = $"{p.Name} ({p.Username}@{p.Host})",
                Tag = p.Id
            };
            JumpHostComboBox.Items.Add(item);

            // Select if this is the current jump host
            if (p.Id == currentProfile?.JumpHostId)
            {
                JumpHostComboBox.SelectedItem = item;
            }
        }

        // Default to "None" if nothing selected
        if (JumpHostComboBox.SelectedItem == null)
        {
            JumpHostComboBox.SelectedItem = noneItem;
        }
    }

    private void OnPrimary(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(NameBox.Text) ||
            string.IsNullOrWhiteSpace(HostBox.Text) ||
            string.IsNullOrWhiteSpace(UserBox.Text))
        {
            args.Cancel = true;
            return;
        }

        if (!int.TryParse(PortBox.Text.Trim(), out var port) || port < 1 || port > 65535)
        {
            args.Cancel = true;
            return;
        }

        if (Profile is null) // create new
            Profile = new HostProfile();

        Profile.Name = NameBox.Text.Trim();
        Profile.Host = HostBox.Text.Trim();
        Profile.Username = UserBox.Text.Trim();
        Profile.Port = port;
        Profile.ExtraArgs = string.IsNullOrWhiteSpace(ExtraArgsBox.Text) ? null : ExtraArgsBox.Text.Trim();

        Profile.RequestTty = TtyCheck.IsChecked == true;
        Profile.AgentForwarding = AgentFwdCheck.IsChecked == true;

        if (HostKeyCheckBox.SelectedItem is ComboBoxItem sel)
            Profile.HostKeyChecking = sel.Content?.ToString() ?? "accept-new";
        else
            Profile.HostKeyChecking = "accept-new";

        // Save jump host selection
        if (JumpHostComboBox.SelectedItem is ComboBoxItem jumpHostItem)
        {
            Profile.JumpHostId = jumpHostItem.Tag as string;
        }
        if (string.IsNullOrEmpty(KeyBox.Text) == false && _editMode == false)
        {
            var sshFile = SshKeyManager.ImportKey(KeyBox.Text.Trim());
            Profile.IdentityFile = sshFile;

        }
    }

    private async void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, _hwnd);

        picker.FileTypeFilter.Add("*"); // keys may have no extension

        var file = await picker.PickSingleFileAsync();
        if (file != null)
            KeyBox.Text = file.Path;
    }

    private void PanelSelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem.Tag.ToString() == "general")
        {
            GeneralPanel.Visibility = Visibility.Visible; 
            AdvancedPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            GeneralPanel.Visibility = Visibility.Collapsed;
            AdvancedPanel.Visibility = Visibility.Visible;
        }
    }
}
