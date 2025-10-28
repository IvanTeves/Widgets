using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace Widgets
{
    public sealed partial class MainWindow : Window
    {
        private IntPtr _hwnd;
        private AppWindow? _appWindow;
        private bool _isPinned;
        private bool _isHeaderHidden;
        private const string HomeUrl = "http://10.230.20.2:9088/data/perspective/client/Chart/widgets";
        private readonly Dictionary<string, string> _links = new()
        {
            {"Link1", "http://10.230.20.2:9088/data/perspective/client/Chart/component"},
            {"Link2", "http://10.230.20.2:9088/data/perspective/client/Chart/component"},
            {"Link3", "https://www.github.com"},
            {"Link4", "https://www.nuget.org"},
            {"Link5", "https://learn.microsoft.com"},
        };

        public MainWindow()
        {
            InitializeComponent();
            InitializeWindowChrome();
            // Mostrar barra (botones) y ocultar solo accesos rápidos
            ApplyHeaderVisibility(false);
            var links = FindEl<StackPanel>("LinksPanel");
            if (links != null) links.Visibility = Visibility.Collapsed;
        }

        private T? FindEl<T>(string name) where T : class => (this.Content as FrameworkElement)?.FindName(name) as T;

        private void InitializeWindowChrome()
        {
            // Obtener HWND y AppWindow
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            // Quitar borde y barra de título del sistema para aspecto de widget
            if (_appWindow?.Presenter is OverlappedPresenter overlapped)
            {
                try
                {
                    overlapped.SetBorderAndTitleBar(false, false);
                    overlapped.IsResizable = true; // permite redimensionar desde bordes invisibles
                    // Quitar fondo del título del sistema por completo
                    _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0,0,0,0);
                    _appWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0,0,0,0);
                    // Tamaño inicial tipo teléfono (portrait)
                    _appWindow.Resize(new SizeInt32(420,820));
                }
                catch { }
            }

            // Extender contenido hasta la barra del sistema (aunque está oculta) y usar nuestra barra
            try { this.ExtendsContentIntoTitleBar = true; this.SetTitleBar(DragBar); } catch { }
        }

        private void ApplyPin(bool pin)
        {
            if (_appWindow?.Presenter is OverlappedPresenter overlapped)
            {
                try { overlapped.IsAlwaysOnTop = pin; } catch { }
            }
            _isPinned = pin;
            var pinBtn = FindEl<Button>("PinButton");
            if (pinBtn != null)
            {
                ToolTipService.SetToolTip(pinBtn, pin ? "Desanclar" : "Anclar");
                var pinIcon = FindEl<FontIcon>("PinIcon");
                if (pinIcon != null) pinIcon.Glyph = pin ? "\uE719" : "\uE718"; // filled vs outline
            }
        }

        private void ApplyHeaderVisibility(bool hide)
        {
            var drag = DragBar;
            var links = FindEl<StackPanel>("LinksPanel");
            var restore = FindEl<Button>("RestoreHeaderButton");
            var hideBtn = FindEl<Button>("HideHeaderButton");
            var hideIcon = FindEl<FontIcon>("HideIcon");

            if (drag != null) drag.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (links != null) links.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (restore != null) restore.Visibility = hide ? Visibility.Visible : Visibility.Collapsed;
            _isHeaderHidden = hide;
            if (hideBtn != null) ToolTipService.SetToolTip(hideBtn, hide ? "Mostrar encabezado" : "Ocultar encabezado");
            if (hideIcon != null) hideIcon.Glyph = hide ? "\uE8A3" : "\uE8F4"; // eye vs eye-off
        }

        // Iniciar WebView2 con runtime fijo si existe
        private async void Browser_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var fixedRuntime = Path.Combine(exeDir, "WebView2Runtime");
                if (Directory.Exists(fixedRuntime))
                {
                    var userData = Path.Combine(exeDir, "WebView2UserData");
                    Directory.CreateDirectory(userData);
                    Environment.SetEnvironmentVariable("WEBVIEW2_BROWSER_EXECUTABLE_FOLDER", fixedRuntime, EnvironmentVariableTarget.Process);
                    Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userData, EnvironmentVariableTarget.Process);
                }
                await Browser.EnsureCoreWebView2Async();
                // Navegar a la pantalla de inicio solicitada
                try { Browser.Source = new Uri(HomeUrl); } catch { }
            }
            catch { }
        }

        private void DragBar_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint((UIElement)sender).Properties.IsLeftButtonPressed)
            {
                try { ReleaseCapture(); SendMessage(_hwnd, WM_NCLBUTTONDOWN, HTCAPTION,0); } catch { }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) { try { Close(); } catch { } }
        private void PinButton_Click(object sender, RoutedEventArgs e) => ApplyPin(!_isPinned);
        private void HideHeaderButton_Click(object sender, RoutedEventArgs e) => ApplyHeaderVisibility(!_isHeaderHidden);
        private void RestoreHeaderButton_Click(object sender, RoutedEventArgs e) => ApplyHeaderVisibility(false);

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string key && _links.TryGetValue(key, out var url))
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;
                try { Browser.Source = new Uri(url); } catch { }
            }
        }

        #region Win32 Drag helpers
        private const int WM_NCLBUTTONDOWN =0x00A1; private const int HTCAPTION =0x0002;
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        #endregion
    }
}
