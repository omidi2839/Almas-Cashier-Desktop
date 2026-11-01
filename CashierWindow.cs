using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AlmasCashierDesktop;

internal sealed class CashierWindow : Form
{
    private readonly (string Label, string Path)[] _sections =
    {
        ("فاکتورها", "admin/almas_invoice_core"),
        ("دریافتی از فروشندگان", "admin/sales_commercial_rules/sales_requests"),
        ("مدیریت صندوق", "admin/sales_commercial_rules/cashier"),
        ("خروج و ارسال کالا", "admin/sales_delivery_flow"),
        ("مرجوعی کالا", "admin/sales_delivery_flow/returns")
    };
    private readonly Dictionary<string, Button> _nav = new();
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _caption = new() { Text = "در حال اتصال ...", ForeColor = Color.FromArgb(53, 72, 101), AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
    private readonly Label _security = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(174, 67, 33) };
    private readonly Panel _overlay = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(247, 250, 253), Visible = true };
    private readonly Label _overlayText = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(31, 52, 84) };
    private readonly Button _connect = new() { Text = "تنظیم اتصال", AutoSize = false, Width = 110, Height = 36 };
    private readonly Button _logout = new() { Text = "خروج از حساب", AutoSize = false, Width = 110, Height = 36 };
    private readonly Button _refresh = new() { Text = "بازخوانی ↻", AutoSize = false, Width = 105, Height = 36 };
    private CashierConfig _config = CashierConfig.Load();
    private CoreWebView2Environment? _environment;
    private bool _authorized;
    private bool _checking;
    private bool _configured;

    public CashierWindow()
    {
        Text = "صندوق فروش الماس — نسخه 0.1.0";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MinimumSize = new Size(1110, 700);
        Width = 1480; Height = 870;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10, FontStyle.Regular);
        BackColor = Color.FromArgb(245, 248, 252);
        RightToLeft = RightToLeft.Yes;
        BuildLayout();
        Shown += async (_, _) => await StartBrowserAsync();
    }

    private void BuildLayout()
    {
        var sidebar = new Panel { Dock = DockStyle.Right, Width = 246, BackColor = Color.FromArgb(17, 38, 76) };
        Controls.Add(sidebar);
        var brand = new Label
        {
            Text = "◆  صندوق فروش الماس\n     ALMAS CASHIER",
            Dock = DockStyle.Top, Height = 122, Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter
        };
        sidebar.Controls.Add(brand);
        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, Padding = new Padding(12, 28, 12, 14)
        };
        sidebar.Controls.Add(menu);
        sidebar.Controls.SetChildIndex(brand, 0);
        sidebar.Controls.SetChildIndex(menu, 1);
        foreach (var section in _sections)
        {
            string localPath = section.Path;
            var button = new Button
            {
                Text = section.Label, Width = 214, Height = 58, Margin = new Padding(0, 0, 0, 10),
                BackColor = Color.FromArgb(27, 50, 89), ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter, Enabled = false,
                Tag = localPath
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => NavigateSection(localPath);
            _nav.Add(localPath, button);
            menu.Controls.Add(button);
        }

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 249, 252) };
        Controls.Add(body);
        body.BringToFront();
        sidebar.BringToFront();
        var header = new Panel { Dock = DockStyle.Top, Height = 73, BackColor = Color.White, Padding = new Padding(16, 13, 16, 12) };
        body.Controls.Add(header);
        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        header.Controls.Add(heading);
        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        heading.Controls.Add(tools, 0, 0);
        heading.Controls.Add(_caption, 1, 0);
        _caption.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        tools.Controls.Add(_connect);
        tools.Controls.Add(_refresh);
        tools.Controls.Add(_logout);
        foreach (Button b in new[] { _connect, _logout, _refresh })
        {
            b.BackColor = Color.FromArgb(244, 247, 252);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(219, 225, 233);
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(5, 3, 5, 3);
        }
        _logout.Enabled = false;
        _refresh.Enabled = false;
        _connect.Click += (_, _) => ChangeServer();
        _logout.Click += (_, _) => Logout();
        _refresh.Click += (_, _) => { if (_configured) _web.Reload(); };
        var warningPanel = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Color.FromArgb(255, 251, 244) };
        _security.Dock = DockStyle.Fill;
        warningPanel.Controls.Add(_security);
        body.Controls.Add(warningPanel);
        warningPanel.BringToFront();
        header.BringToFront();
        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        body.Controls.Add(content);
        content.BringToFront();
        header.BringToFront(); warningPanel.BringToFront();
        content.Controls.Add(_web);
        _overlay.Controls.Add(_overlayText);
        content.Controls.Add(_overlay);
        _overlay.BringToFront();
        _overlayText.Font = new Font("Segoe UI", 13, FontStyle.Regular);
        SetMessage("در حال آماده‌سازی محیط صندوق ...");
        UpdateSecurityLabel();
    }

    private void UpdateSecurityLabel()
    {
        _security.Text = _config.ServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? "اتصال سرور: " + _config.ServerUrl
            : "هشدار: آدرس سرور از HTTP استفاده می‌کند. ورود با رمز عبور روی HTTP امن نیست: " + _config.ServerUrl;
    }

    private void SetMessage(string message)
    {
        _overlayText.Text = message;
        _overlay.Visible = true;
        _overlay.BringToFront();
    }

    private void ShowBrowser()
    {
        _overlay.Visible = false;
        _web.Visible = true;
        _web.BringToFront();
    }

    private async Task StartBrowserAsync()
    {
        try
        {
            if (!_configured)
            {
                _environment = await CoreWebView2Environment.CreateAsync(userDataFolder: CashierConfig.WebViewFolder);
                await _web.EnsureCoreWebView2Async(_environment);
                _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _web.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                _web.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                _web.CoreWebView2.NavigationStarting += BrowserNavigating;
                _web.CoreWebView2.NavigationCompleted += BrowserNavigated;
                _web.CoreWebView2.NewWindowRequested += NewWindowRequested;
                await _web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(DesktopCssScript);
                _configured = true;
            }
            _authorized = false;
            UpdateNavState();
            _caption.Text = "ورود صندوقدار";
            SetMessage("در حال بازکردن ورود سامانه ...");
            _web.CoreWebView2.Navigate(Link("admin/authentication"));
        }
        catch (Exception ex)
        {
            SetMessage("WebView2 قابل اجرا نیست. Microsoft Edge WebView2 Runtime را نصب کنید.\n" + ex.Message);
            MessageBox.Show(this, "خطا در راه‌اندازی WebView2:\n" + ex.Message, "خطای برنامه", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string Link(string relative) => _config.ServerUrl.TrimEnd('/') + "/" + relative.TrimStart('/');

    private bool SameServer(Uri uri)
    {
        var root = new Uri(_config.ServerUrl);
        return uri.Scheme == root.Scheme && uri.Host.Equals(root.Host, StringComparison.OrdinalIgnoreCase) && uri.Port == root.Port;
    }

    private bool IsAuthentication(string path) => path.StartsWith("/admin/authentication", StringComparison.OrdinalIgnoreCase);
    private bool IsPortalRoot(string path) => path == "/admin" || path == "/admin/" || path == "/admin/dashboard" || path == "/";

    private bool IsAllowedPath(string path)
    {
        path = path.TrimEnd('/');
        foreach (var (_, relative) in _sections)
        {
            var allowed = "/" + relative;
            if (path.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(allowed + "/", StringComparison.OrdinalIgnoreCase)) return true;
        }
        // The invoice PDF and download routes use the same invoice controller.
        return false;
    }

    private void BrowserNavigating(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) { e.Cancel = true; return; }
        if (!SameServer(uri) || !(IsAuthentication(uri.AbsolutePath) || IsPortalRoot(uri.AbsolutePath) || IsAllowedPath(uri.AbsolutePath)))
        {
            e.Cancel = true;
            MessageBox.Show(this, "این قسمت خارج از محیط مجاز صندوق است. دسترسی‌ها باید از داخل سامانه برای صندوقدار تنظیم شوند.", "مسیر غیرمجاز", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (IsAuthentication(uri.AbsolutePath))
        {
            _authorized = false;
            UpdateNavState();
            _caption.Text = "ورود صندوقدار";
        }
        else if (!_authorized) SetMessage("در حال بررسی مجوز صندوقدار ...");
    }

    private async void BrowserNavigated(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!_configured) return;
        if (!e.IsSuccess)
        {
            SetMessage("اتصال به سرور برقرار نشد. آدرس و وضعیت شبکه را بررسی کنید.");
            return;
        }
        if (!Uri.TryCreate(_web.Source?.ToString(), UriKind.Absolute, out var uri)) return;
        if (IsAuthentication(uri.AbsolutePath))
        {
            _caption.Text = "ورود صندوقدار";
            ShowBrowser();
            return;
        }
        if (!_authorized)
        {
            await VerifyCashierAsync(IsPortalRoot(uri.AbsolutePath));
            return;
        }
        if (IsPortalRoot(uri.AbsolutePath))
        {
            NavigateSection(_sections[0].Path);
            return;
        }
        UpdateActiveNav(uri.AbsolutePath);
        ShowBrowser();
    }

    private async Task VerifyCashierAsync(bool cameFromHome)
    {
        if (_checking) return;
        _checking = true;
        SetMessage("در حال بررسی مجوز صندوقدار در سرور ...");
        try
        {
            // Same-origin request uses the authenticated Perfex session cookie.
            // Only the server can approve a cashier ID; no credential is saved by the desktop app.
            const string script = "(async()=>{try{const r=await fetch('/admin/almas_cashier_portal/status',{credentials:'same-origin',headers:{'Accept':'application/json'},cache:'no-store'});return JSON.stringify({status:r.status,body:await r.text()});}catch(e){return JSON.stringify({status:0,body:String(e)});}})()";
            string raw = await _web.CoreWebView2.ExecuteScriptAsync(script);
            using var output = JsonDocument.Parse(raw);
            string inside = output.RootElement.GetString() ?? "";
            using var response = JsonDocument.Parse(inside);
            int status = response.RootElement.GetProperty("status").GetInt32();
            string body = response.RootElement.GetProperty("body").GetString() ?? "";
            if (status == 200)
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.TryGetProperty("allowed", out var can) && can.GetBoolean())
                {
                    _authorized = true;
                    UpdateNavState();
                    if (cameFromHome)
                        _web.CoreWebView2.Navigate(Link(_sections[0].Path));
                    else
                    {
                        UpdateActiveNav(new Uri(_web.Source.ToString()).AbsolutePath);
                        ShowBrowser();
                    }
                    return;
                }
            }
            SetMessage("ورود به صندوق تأیید نشد.\nماژول almas_cashier_portal را نصب کنید و شناسه صندوقدار را در تنظیمات آن مجاز کنید.\nمجوزهای پنج بخش نیز باید در Perfex برای همان کاربر برقرار باشند.");
            _caption.Text = "عدم تأیید دسترسی صندوقدار";
        }
        catch (Exception)
        {
            SetMessage("تأیید دسترسی ممکن نشد.\nماژول سمت سرور نصب یا پاسخ JSON آن معتبر نیست.\nاز مدیر سیستم بخواهید بخش /admin/almas_cashier_portal/settings را بررسی کند.");
        }
        finally { _checking = false; }
    }

    private void UpdateNavState()
    {
        foreach (var button in _nav.Values) button.Enabled = _authorized;
        _logout.Enabled = _configured;
        _refresh.Enabled = _configured;
    }

    private void UpdateActiveNav(string path)
    {
        string? activeKey = null;
        foreach (var item in _nav)
            if ((path.Equals("/" + item.Key, StringComparison.OrdinalIgnoreCase) || path.StartsWith("/" + item.Key + "/", StringComparison.OrdinalIgnoreCase)) &&
                (activeKey == null || item.Key.Length > activeKey.Length)) activeKey = item.Key;
        foreach (var item in _nav)
            item.Value.BackColor = item.Key == activeKey ? Color.FromArgb(230, 113, 14) : Color.FromArgb(27, 50, 89);
        foreach (var item in _sections)
            if (item.Path == activeKey) { _caption.Text = item.Label; break; }
    }

    private void NavigateSection(string path)
    {
        if (!_authorized) return;
        _web.CoreWebView2.Navigate(Link(path));
    }

    private void Logout()
    {
        if (!_configured) return;
        _authorized = false;
        UpdateNavState();
        SetMessage("در حال خروج از سامانه ...");
        _web.CoreWebView2.Navigate(Link("admin/authentication/logout"));
    }

    private void ChangeServer()
    {
        using var dialog = new ServerDialog(_config.ServerUrl);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _config.ServerUrl = dialog.ServerUrl;
        _config.Save();
        UpdateSecurityLabel();
        if (_configured) { _authorized = false; UpdateNavState(); _web.CoreWebView2.Navigate(Link("admin/authentication")); }
    }

    private async void NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var url) || !SameServer(url) || !IsAllowedPath(url.AbsolutePath))
            {
                e.Handled = true;
                return;
            }
            var popup = new Form { Text = "چاپ و اسناد صندوق الماس", WindowState = FormWindowState.Maximized, MinimumSize = new Size(900, 650) };
            var viewer = new WebView2 { Dock = DockStyle.Fill };
            popup.Controls.Add(viewer);
            popup.Show(this);
            await viewer.EnsureCoreWebView2Async(_environment);
            viewer.CoreWebView2.Settings.AreDevToolsEnabled = false;
            viewer.CoreWebView2.NavigationStarting += (_, nav) =>
            {
                if (!Uri.TryCreate(nav.Uri, UriKind.Absolute, out var dest) || !SameServer(dest) || !IsAllowedPath(dest.AbsolutePath)) nav.Cancel = true;
            };
            e.NewWindow = viewer.CoreWebView2;
            e.Handled = true;
        }
        catch { e.Handled = true; }
        finally { deferral.Complete(); }
    }

    private const string DesktopCssScript = @"
(() => {
 if (/^\/admin\/authentication/i.test(location.pathname)) return;
 const css = `
 #header, .navbar.navbar-default.header, #side-menu, #mobile-menu-toggle,
 .admin-side-menu, .sidebar, .sidebar-nav, #menu, .mobile-menu-toggle,
 .navbar-header, #top_search, .header-nav, #setup-menu-wrapper,
 body > .navbar, .screen-options-btn { display:none !important; }
 #wrapper { margin: 0 !important; padding:0 !important; width:100% !important; }
 #wrapper .content { margin:0 !important; padding:12px 14px !important; }
 .main-content, .main-panel, .content-wrapper { margin-right:0 !important; margin-left:0 !important; }
 #aic .aic-main-head, #aic .aic-kpis {display:none !important;}
 a[href*='/invoice_form_customizer/bulk_import'],
 a[href*='/invoice_form_customizer/bulk_payments'],
 a[href*='/bulk_import'],a[href*='/bulk_payments'] {display:none !important;}
 #aic .aic-toolbar-divider {display:none !important;}
 body {background:#f5f8fc !important;}
 `;
 const add = () => {if(document.querySelector('#almas-desktop-css')) return;
 const el=document.createElement('style'); el.id='almas-desktop-css'; el.textContent=css;
 (document.head||document.documentElement).appendChild(el);};
 add();document.addEventListener('DOMContentLoaded', add);
 })();";
}
