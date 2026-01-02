using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinSsh.Profiles;
using WinSsh.Views;

namespace WinSsh;

public sealed partial class MainWindow : Window
{
    public static MainWindow? Current { get; private set; }

    private readonly ProfilesStore _store;
    private List<HostProfile> _profiles = new();
    private HostProfile? _selected;
    private Dictionary<NavigationViewItem, Pages.ConnectionPage> _activeConnections = new();

    public List<HostProfile> Profiles => _profiles;


    public MainWindow()
    {
        this.InitializeComponent();

        Current = this;

        this.ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);

        _store = ProfilesStore.CreateDefault();

        _ = LoadProfilesAsync();
        
        navFrame.Navigate(typeof(Pages.HomePage));
    }

    private async Task LoadProfilesAsync()
    {
        _profiles = await _store.LoadAsync();
        ProfilesList.ItemsSource = _profiles;
        if (_profiles.Count > 0) ProfilesList.SelectedIndex = 0;
    }

    private void RefreshProfilesList()
    {
        ProfilesList.ItemsSource = null;
        ProfilesList.ItemsSource = _profiles;
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = ProfilesList.SelectedItem as HostProfile;
    }


    private async void EditProfile_Click(object sender, RoutedEventArgs e)
    {
        await EditSelectedProfileAsync();
    }

    private async Task EditSelectedProfileAsync()
    {
        if (_selected is null) return;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        var dlg = new ProfileDialog(_selected, hwnd, _profiles)
        {
            XamlRoot = ((FrameworkElement)Content).XamlRoot
        };

        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            RefreshProfilesList();
            await _store.SaveAsync(_profiles);
        }
    }


    public void OpenConnectionForProfile(HostProfile profile)
    {
        var connectionItem = new NavigationViewItem
        {
            Content = profile.Name,
            Tag = profile,
            Icon = new SymbolIcon(Symbol.Globe)
        };

        AddConnectionMenuItem(connectionItem);
        NavigateToConnection(connectionItem);
    }

    public void AddConnectionMenuItem(NavigationViewItem item)
    {
        connectionsViewItem.MenuItems.Add(item);
    }

    public void RemoveConnectionMenuItem(NavigationViewItem item)
    {
        connectionsViewItem.MenuItems.Remove(item);
        
        // Clean up the connection page
        if (_activeConnections.TryGetValue(item, out var page))
        {
            _activeConnections.Remove(item);
        }
    }

    public void NavigateToConnection(NavigationViewItem item)
    {
        navFrame.Visibility = Visibility.Visible;
        terminalContent.Visibility = Visibility.Collapsed;
        navView.SelectedItem = item;
        
        // Reuse existing connection page if it exists, otherwise create a new one
        if (!_activeConnections.ContainsKey(item))
        {
            var page = new Pages.ConnectionPage();
            _activeConnections[item] = page;
            
            // Initialize the connection
            if (item.Tag is HostProfile profile)
            {
                _ = page.InitializeConnectionAsync(item, profile);
            }
        }
        
        // Set the page as the frame's content
        navFrame.Content = _activeConnections[item];
    }

    public void NavigateToHome()
    {
        navView.SelectedItem = navView.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == "HomePage");
    }


    private void ProfilesList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_selected is null) return;
        OpenConnectionForProfile(_selected);
    }

    private void navView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            
            switch (tag)
            {
                case "HomePage":
                    navFrame.Visibility = Visibility.Visible;
                    terminalContent.Visibility = Visibility.Collapsed;
                    navFrame.Navigate(typeof(Pages.HomePage));
                    break;
                case "Connections":
                    navFrame.Visibility = Visibility.Collapsed;
                    terminalContent.Visibility = Visibility.Visible;
                    break;
                default:
                    if (item.Tag is HostProfile)
                    {
                        NavigateToConnection(item);
                    }
                    break;
            }
        }
    }

    private void navView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            navFrame.Visibility = Visibility.Visible;
            terminalContent.Visibility = Visibility.Collapsed;
            navFrame.Navigate(typeof(Pages.SettingsPage));
        }
    }

    private void titleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        navView.IsPaneOpen = !navView.IsPaneOpen;
    }
}
