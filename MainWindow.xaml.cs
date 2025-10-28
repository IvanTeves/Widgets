using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Widgets
{
    public sealed partial class MainWindow : Window
    {
        private readonly HttpClient _http = new HttpClient();
        private IntPtr _hwnd;
        private AppWindow? _appWindow;
        private bool _isPinned;
        private bool _isHeaderHidden;

        public MainWindow()
        {
            InitializeComponent();
            InitializeWindowChrome();
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
                    _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
                    _appWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
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
            var pinBtn = FindEl<Button>("PinButton"); if (pinBtn != null) pinBtn.Content = pin ? "Unpin" : "Pin";
        }

        private void ApplyHeaderVisibility(bool hide)
        {
            var controls = FindEl<StackPanel>("ControlsPanel");
            var apiBorder = FindEl<Border>("ApiBorder");
            var hideBtn = FindEl<Button>("HideHeaderButton");
            var drag = DragBar;
            var restore = FindEl<Button>("RestoreHeaderButton");

            if (drag != null) drag.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (controls != null) controls.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (apiBorder != null) apiBorder.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
            if (restore != null) restore.Visibility = hide ? Visibility.Visible : Visibility.Collapsed;
            _isHeaderHidden = hide;
            if (hideBtn != null) hideBtn.Content = hide ? "Mostrar" : "Ocultar";
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
            }
            catch
            {
                // Fallback al runtime instalado del sistema si hay error
            }
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

        private void OpenWebButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
            try { Browser.Source = new Uri(url); } catch (Exception ex) { ApiResultText.Text = $"URL inválida: {ex.Message}"; }
        }

        private async void CallApiButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
            ApiResultText.Text = "Consultando...";
            try { using var resp = await _http.GetAsync(url); var body = await resp.Content.ReadAsStringAsync(); ApiResultText.Text = $"{(int)resp.StatusCode} {resp.ReasonPhrase}\n\n{body}"; }
            catch (Exception ex) { ApiResultText.Text = $"Error: {ex.Message}"; }
        }

        #region Win32 Drag helpers
        private const int WM_NCLBUTTONDOWN =0x00A1; private const int HTCAPTION =0x0002;
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        #endregion
    }
}
