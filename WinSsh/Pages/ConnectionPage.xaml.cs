using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WinSsh.Common;
using WinSsh.Profiles;
using WinSsh.Terminal;

namespace WinSsh.Pages
{
    public sealed partial class ConnectionPage : Page
    {
        private const string SshExe = @"C:\Windows\System32\OpenSSH\ssh.exe";

        private HostProfile? _profile;
        private NavigationViewItem? _navigationItem;
        private TerminalSession? _session;
        private bool _isInitialized;

        public ConnectionPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is NavigationViewItem item && item.Tag is HostProfile profile)
            {
                await InitializeConnectionAsync(item, profile);
            }
        }

        public async Task InitializeConnectionAsync(NavigationViewItem navigationItem, HostProfile profile)
        {
            if (_isInitialized) return;

            _profile = profile;
            _navigationItem = navigationItem;
            ConnectionNameTextBlock.Text = profile.Name;

            await InitializeTerminalAsync();
            _isInitialized = true;
        }

        private async Task InitializeTerminalAsync()
        {
            if (_profile == null) return;

            await WaitForLoadedAsync(TerminalWebView);

            try
            {
                await TerminalWebView.EnsureCoreWebView2Async();

                var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "terminal");
                TerminalWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "appassets",
                    assetsPath,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                TerminalWebView.Source = new Uri("https://appassets/terminal.html");

                _session = new TerminalSession();
                HookSessionToWebView(_session);

                TerminalWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                var allProfiles = MainWindow.Current?.Profiles;
                var cmd = SshCommandBuilder.BuildCommandLine(SshExe, _profile, allProfiles);
                await _session.StartSshAsync(cmd, _profile.InitialCols, _profile.InitialRows);

                TerminalWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "focus" }));
            }
            catch (Exception ex)
            {
                var dlg = new ContentDialog
                {
                    Title = "WebView2 failed to initialize",
                    Content = ex.ToString(),
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
            }
        }

        private void HookSessionToWebView(TerminalSession session)
        {
            session.OutputText += text =>
            {
                _ = TerminalWebView.DispatcherQueue.TryEnqueue(() =>
                {
                    var msg = JsonSerializer.Serialize(new { type = "output", data = text });
                    TerminalWebView.CoreWebView2.PostWebMessageAsJson(msg);
                });
            };

            session.InteractivePrompt += async (message) =>
            {
                string? answer = null;

                await TerminalWebView.DispatcherQueue.EnqueueAsync(async () =>
                {
                    var answerBox = new TextBox { PlaceholderText = "Type response (e.g. yes / no / password)", MinWidth = 420 };

                    var dlg = new ContentDialog
                    {
                        Title = "SSH Prompt",
                        Content = new StackPanel
                        {
                            Spacing = 10,
                            Children =
                            {
                                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                                answerBox
                            }
                        },
                        PrimaryButtonText = "Send",
                        SecondaryButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
                    };

                    var r = await dlg.ShowAsync();
                    answer = (r == ContentDialogResult.Primary) ? answerBox.Text : null;
                });

                return answer;
            };
        }

        private async void OnWebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (_session == null) return;

            using var doc = JsonDocument.Parse(args.WebMessageAsJson);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            if (type == "input")
            {
                var data = root.GetProperty("data").GetString() ?? "";
                await _session.WriteInputAsync(data);
            }
            else if (type == "resize")
            {
                var cols = root.GetProperty("cols").GetInt32();
                var rows = root.GetProperty("rows").GetInt32();
                _session.Resize(cols, rows);
            }
        }

        private static Task WaitForLoadedAsync(FrameworkElement element)
        {
            if (element.IsLoaded) return Task.CompletedTask;

            var tcs = new TaskCompletionSource();
            RoutedEventHandler? handler = null;
            handler = (_, __) =>
            {
                element.Loaded -= handler;
                tcs.SetResult();
            };
            element.Loaded += handler;
            return tcs.Task;
        }

        private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_profile == null || _session == null) return;

            await _session.CloseAsync();

            _session = new TerminalSession();
            HookSessionToWebView(_session);

            var allProfiles = MainWindow.Current?.Profiles;
            var cmd = SshCommandBuilder.BuildCommandLine(SshExe, _profile, allProfiles);
            await _session.StartSshAsync(cmd, _profile.InitialCols, _profile.InitialRows);

            TerminalWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "focus" }));
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            await CloseSessionAsync();

            if (_navigationItem == null)
                return;

            var mainWindow = MainWindow.Current;
            if (mainWindow == null)
                return;

            mainWindow.RemoveConnectionMenuItem(_navigationItem);
            mainWindow.NavigateToHome();
        }

        private async Task CloseSessionAsync()
        {
            if (_session != null)
            {
                await _session.CloseAsync();
                _session = null;
            }
        }
    }
}
