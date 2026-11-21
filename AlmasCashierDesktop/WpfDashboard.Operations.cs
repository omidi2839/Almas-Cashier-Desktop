using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlmasCashierDesktop;

// Transitional operational bridge: the native WPF dashboard and read-only data
// stay in place. Financial writes happen ONLY in the existing, authenticated
// HTTPS web forms, which preserve server-side RBAC, validation and CSRF.
// Device bearer tokens must never be sent to this WebView.
public partial class WpfDashboard
{
    private string portalBase = "https://sejadalmas.iq";
    private WebView2? operationsBrowser;
    private TextBlock? operationsStatus;
    private bool operationsReady;
    private int operationsGeneration;

    private static readonly IReadOnlyDictionary<string, string> OperationalPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["فاکتورهای الماس"] = "admin/almas_invoice_core",
            ["ثبت سفارش"] = "admin/almas_invoice_core/orders",
            ["دریافتی از فروشندگان"] = "admin/sales_commercial_rules/sales_requests",
            ["مدیریت صندوق"] = "admin/sales_commercial_rules/cashier",
            ["فروش عمده و صادراتی"] = "admin/almas_invoice_core/wholesale",
            ["خروج و ارسال کالا"] = "admin/sales_delivery_flow/deliveries",
            ["مرجوعی کالا"] = "admin/sales_delivery_flow/returns",
            ["نمای کلی"] = "admin/sales_accounting",
            ["حساب‌ها"] = "admin/sales_accounting/accounts",
            ["دریافت و پرداخت"] = "admin/sales_accounting/transactions",
            ["تحویل صندوق"] = "admin/sales_accounting/handovers",
            ["انتقال وجه"] = "admin/sales_accounting/transfers",
            ["سرفصل‌ها"] = "admin/sales_accounting/categories",
            ["داشبورد انبار"] = "admin/almas_warehouse",
            ["انبارها"] = "admin/almas_warehouse/warehouses",
            ["موجودی کالاها"] = "admin/almas_warehouse/stock",
            ["رزرو موجودی"] = "admin/almas_warehouse/reservations",
            ["اسناد انبار"] = "admin/almas_warehouse/documents",
            ["انبارگردانی"] = "admin/almas_warehouse/cycle_counts",
            ["گردش موجودی"] = "admin/almas_warehouse/ledger"
        };

    private bool HasOperationalPermission(string name) =>
        liveMode && OperationalPaths.ContainsKey(name) &&
        liveMenu.Any(m => m.Label == name);

    private bool IsPortalDestination(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var next) ||
            !Uri.TryCreate(portalBase, UriKind.Absolute, out var root))
            return false;

        return next.Scheme == Uri.UriSchemeHttps &&
               string.IsNullOrEmpty(next.UserInfo) &&
               next.Host.Equals(root.Host, StringComparison.OrdinalIgnoreCase) &&
               next.Port == root.Port;
    }

    private void HideOperations()
    {
        operationsGeneration++;
        OperationsView.Visibility = Visibility.Collapsed;
    }

    private void ShowOperations(string name) => _ = ShowOperationsAsync(name);

    private async Task ShowOperationsAsync(string name)
    {
        if (!HasOperationalPermission(name) ||
            !OperationalPaths.TryGetValue(name, out var path))
        {
            MessageBox.Show(this, "این بخش برای کلید دستگاه فعلی مجاز نیست.");
            return;
        }

        if (!ValidGateway(portalBase))
        {
            MessageBox.Show(this, "ابتدا نشانی امن پنل عملیاتی را در تنظیمات وارد کنید.");
            Navigate("تنظیمات");
            return;
        }

        var turn = ++operationsGeneration;
        section = name;
        PageTitle.Text = name;
        foreach (var item in nav)
            item.Value.Tag = item.Key == name ? "active" : null;

        DashboardView.Visibility = Visibility.Collapsed;
        OtherView.Visibility = Visibility.Collapsed;
        InvoiceWorkspace.Visibility = Visibility.Collapsed;
        OperationsView.Visibility = Visibility.Visible;

        try
        {
            await EnsureOperationsBrowserAsync();
            if (!liveMode || turn != operationsGeneration)
                return;

            var target = portalBase.TrimEnd('/') + "/" + path;
            operationsStatus!.Text = "پنل عملیاتی: " + name +
                "  •  ورود وب و مجوزهای سرور مستقل از کلید دستگاه هستند";
            operationsBrowser!.CoreWebView2.Navigate(target);
        }
        catch (Exception ex)
        {
            if (turn != operationsGeneration) return;
            if (operationsStatus != null)
                operationsStatus.Text = "راه‌اندازی مرورگر امن ناموفق بود: " + ex.GetType().Name;
            MessageBox.Show(this,
                "Microsoft Edge WebView2 Runtime باید روی ویندوز نصب باشد. " +
                "گواهی HTTPS و نشانی پنل را بررسی کنید.",
                "خطای پنل عملیاتی");
        }
    }

    private async Task EnsureOperationsBrowserAsync()
    {
        if (operationsReady)
            return;

        if (operationsBrowser == null)
        {
            var frame = new Grid();
            frame.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            frame.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var bar = new DockPanel { Background = Brushes.FloralWhite, LastChildFill = true };
            var refresh = new Button
            {
                Content = "بازخوانی صفحه",
                Style = (Style)FindResource("Secondary"),
                Margin = new Thickness(5),
                MinWidth = 120
            };
            refresh.Click += (_, _) => operationsBrowser?.CoreWebView2?.Reload();

            var login = new Button
            {
                Content = "ورود به پنل وب",
                Style = (Style)FindResource("Secondary"),
                Margin = new Thickness(5),
                MinWidth = 125
            };
            login.Click += (_, _) =>
            {
                if (operationsBrowser?.CoreWebView2 != null && ValidGateway(portalBase))
                    operationsBrowser.CoreWebView2.Navigate(
                        portalBase.TrimEnd('/') + "/admin/authentication");
            };

            DockPanel.SetDock(refresh, Dock.Left);
            DockPanel.SetDock(login, Dock.Left);
            bar.Children.Add(refresh);
            bar.Children.Add(login);

            operationsStatus = new TextBlock
            {
                Text = "فرم‌های واقعی سامانه؛ ورود مستقل با حساب کاربری وب",
                Margin = new Thickness(14, 8, 10, 8),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            bar.Children.Add(operationsStatus);
            frame.Children.Add(bar);

            operationsBrowser = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                DefaultBackgroundColor = System.Drawing.Color.White
            };
            Grid.SetRow(operationsBrowser, 1);
            frame.Children.Add(operationsBrowser);
            OperationsView.Children.Add(frame);
        }

        Directory.CreateDirectory(CashierConfig.Home);
        var env = await CoreWebView2Environment.CreateAsync(
            userDataFolder: Path.Combine(CashierConfig.Home, "operations-web-profile"));
        await operationsBrowser.EnsureCoreWebView2Async(env);
        operationsBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        operationsBrowser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
        operationsBrowser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;

        operationsBrowser.CoreWebView2.NavigationStarting += (_, e) =>
        {
            // No HTTP downgrade, cross-origin redirect or external navigation.
            if (!IsPortalDestination(e.Uri))
            {
                e.Cancel = true;
                if (operationsStatus != null)
                    operationsStatus.Text = "ناوبری خارج از دامنه HTTPS پنل مسدود شد؛ " +
                        "نشانی اصلی سامانه و تنظیمات نشست را بررسی کنید.";
            }
        };
        operationsBrowser.CoreWebView2.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (IsPortalDestination(e.Uri))
                operationsBrowser.CoreWebView2.Navigate(e.Uri);
        };
        operationsBrowser.CoreWebView2.NavigationCompleted += (_, e) =>
        {
            if (!e.IsSuccess && operationsStatus != null)
                operationsStatus.Text =
                    "بارگذاری پنل موفق نبود. وضعیت HTTPS، نشست و دسترسی وب را بررسی کنید.";
        };
        operationsReady = true;
    }

    private void ClearOperationsOnDisconnect()
    {
        HideOperations();
        // Cookies belong to the independent web login, never to the device key.
        // They are cleared when the device token is disconnected.
        if (operationsReady && operationsBrowser?.CoreWebView2 != null)
            operationsBrowser.CoreWebView2.CookieManager.DeleteAllCookies();
    }
}
