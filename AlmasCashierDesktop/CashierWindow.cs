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
