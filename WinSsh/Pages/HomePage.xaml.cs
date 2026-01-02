using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using WinSsh.Profiles;
using WinSsh.Views;

namespace WinSsh.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        this.InitializeComponent();
        _ = LoadSampleData();
    }

    private async Task LoadSampleData()
    {
        var store = ProfilesStore.CreateDefault();
        var items = await store.LoadAsync();

        HomeItemsView.ItemsSource = items;
    }

    private void HomeItemsView_SelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        editAppBarButton.IsEnabled = HomeItemsView.SelectedItems.Count == 1;
        connectAppBarButton.IsEnabled = HomeItemsView.SelectedItems.Count == 1;
        deleteAppBarButton.IsEnabled = HomeItemsView.SelectedItems.Count == 1;
        duplicateAppBarButton.IsEnabled = HomeItemsView.SelectedItems.Count == 1;
    }

    private void connectAppBarButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (HomeItemsView.SelectedItem is not HostProfile selectedProfile)
            return;

        var mainWindow = MainWindow.Current;
        if (mainWindow == null)
            return;

        mainWindow.OpenConnectionForProfile(selectedProfile);
    }

    private async void editAppBarButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (HomeItemsView.SelectedItem is not HostProfile selectedProfile)
            return;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);

        var store = ProfilesStore.CreateDefault();
        var allProfiles = await store.LoadAsync();

        var profileDialog = new ProfileDialog(selectedProfile, hwnd, allProfiles)
        {
            XamlRoot = ((FrameworkElement)Content).XamlRoot
            , Width = 1200
            , MaxWidth = 1200
        };
        
        var result = await profileDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await store.UpdateProfile(profileDialog.Profile!);
        }
    }

    private async void addAppBarButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Current);

        var store = ProfilesStore.CreateDefault();
        var allProfiles = await store.LoadAsync();

        var profileDialog = new ProfileDialog(null, hwnd, allProfiles)
        {
            XamlRoot = ((FrameworkElement)Content).XamlRoot
            , Width = 1200
            , MaxWidth = 1200
        };
        
        var result = await profileDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            HostProfile newProfile = profileDialog.Profile!;
            await store.AddProfile(newProfile);
            await LoadSampleData();
        }
    }

    private async void deleteAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        if (HomeItemsView.SelectedItem is not HostProfile selectedProfile)
            return;

        var confirmDialog = new ContentDialog
        {
            Title = "Delete Profile",
            Content = $"Are you sure you want to delete '{selectedProfile.Name}'?\n\nThis action cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = ((FrameworkElement)Content).XamlRoot
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var store = ProfilesStore.CreateDefault();
            await store.DeleteProfile(selectedProfile);
            await LoadSampleData();
        }
    }

    private async void duplicateAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        if (HomeItemsView.SelectedItem is not HostProfile selectedProfile)
            return;

        var store = ProfilesStore.CreateDefault();
        var allProfiles = await store.LoadAsync();

        // Create a duplicate profile
        var duplicateProfile = new HostProfile
        {
            Id = Guid.NewGuid().ToString("n"),
            Symbol = selectedProfile.Symbol,
            Name = $"{selectedProfile.Name} (Copy)",
            Host = selectedProfile.Host,
            Port = selectedProfile.Port,
            Username = selectedProfile.Username,
            RequestTty = selectedProfile.RequestTty,
            AgentForwarding = selectedProfile.AgentForwarding,
            HostKeyChecking = selectedProfile.HostKeyChecking,
            JumpHostId = selectedProfile.JumpHostId,
            ExtraArgs = selectedProfile.ExtraArgs,
            InitialCols = selectedProfile.InitialCols,
            InitialRows = selectedProfile.InitialRows
        };

        // Duplicate SSH key if it exists
        if (!string.IsNullOrWhiteSpace(selectedProfile.IdentityFile))
        {
            duplicateProfile.IdentityFile = await SshKeyManager.DuplicateKeyAsync(selectedProfile.IdentityFile);
        }

        await store.AddProfile(duplicateProfile);
        await LoadSampleData();
    }
}
