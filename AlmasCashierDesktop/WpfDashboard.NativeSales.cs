using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace AlmasCashierDesktop;

// Native WPF invoice workspace backed by the authorized Almas Desktop Bridge API.
// NO embedded browser, mock financial records, client-calculated official price,
// or financial POST. Financial writes require a dedicated server-authoritative API.
public partial class WpfDashboard
{
    private sealed record NativePick(int Id, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed class NativeInvoiceRow
    {
        public int Id { get; init; }
        public string Number { get; init; } = "";
        public string Customer { get; init; } = "";
        public string Seller { get; init; } = "";
        public string Date { get; init; } = "";
        public string Status { get; init; } = "";
        public decimal Total { get; init; }
        public decimal Paid { get; init; }
        public int CurrencyId { get; init; }
        public bool Reserved { get; init; }
        public bool Delivered { get; init; }
        public bool Returned { get; init; }
        public bool WarehouseExit { get; init; }
        public bool ReservationConflict { get; init; }
        public bool MissingReservation { get; init; }
        public bool InvalidDate { get; init; }
        public string TotalText => Money(Total);
        public string PaidText => Money(Paid);
        public string BalanceText => Money(Math.Max(0, Total - Paid));
        public string Flow
        {
            get
            {
                var states = new[]
                {
                    Returned ? "مرجوعی" : "", Delivered ? "تحویل مشتری" : "",
                    WarehouseExit ? "خروج انبار" : "", Reserved ? "رزرو فعال" : "",
                    ReservationConflict ? "مغایرت رزرو" : "",
                    MissingReservation ? "رزرو ثبت‌نشده" : "",
                    InvalidDate ? "تاریخ نامعتبر" : ""
                };
                var result = string.Join(" | ", states.Where(x => x != ""));
                return result.Length > 0 ? result : "—";
            }
        }
    }

    private sealed record NativeProductPick(int Id, string Name, decimal? Stock, string Code)
    {
        public override string ToString() => Name;
    }

    private sealed class NativeDraftLine
    {
        public int ItemId { get; init; }
        public string Product { get; init; } = "";
        public decimal Quantity { get; init; }
        public decimal Discount { get; init; }
        public string QuantityText => Money(Quantity);
        public string DiscountText => Money(Discount) + "٪";
    }

    private DataGrid? nativeInvoiceGrid;
    private TextBox? nativeSearch;
    private DatePicker? nativeFrom;
    private DatePicker? nativeTo;
    private ComboBox? nativeStatus;
    private ComboBox? nativeSeller;
    private ComboBox? nativeType;
    private ComboBox? nativePer;
    private ComboBox? nativeSort;
    private ComboBox? nativeSortDirection;
    private TextBlock? nativeCount;
    private TextBlock? nativeNotice;
    private Button? nativePrevious;
    private Button? nativeNext;
    private Button? nativeCreate;
    private int nativePage = 1, nativePages = 1, nativeGeneration;
    private bool nativeCanCreate;
    private readonly ObservableCollection<NativeInvoiceRow> nativeRows = new();
    private readonly List<NativePick> nativeSellerChoices = new();
    private readonly List<NativePick> nativeTypeChoices = new();

    private static int Number(JsonElement element, string key)
    {
        return int.TryParse(ReadText(element, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static string Clean(string text) => text.Length <= 220 ? text : text[..220];

    private async Task OpenNativeInvoiceListAsync()
    {
        if (!liveMode) return;
        var version = ++nativeGeneration;
        section = "فاکتورهای الماس";
        PageTitle.Text = section;
        foreach (var item in nav) item.Value.Tag = item.Key == section ? "active" : null;

        DashboardView.Visibility = Visibility.Collapsed;
        OtherView.Visibility = Visibility.Collapsed;
        InvoiceWorkspace.Visibility = Visibility.Visible;

        var page = new DockPanel { Margin = new Thickness(10), LastChildFill = true };
        InvoiceWorkspace.Content = page;
        void Header(UIElement element) { DockPanel.SetDock(element, Dock.Top); page.Children.Add(element); }

        var summary = new TextBlock
        {
            Text = "فاکتورهای الماس • اطلاعات واقعی از مرکز فروش • فرم مستقل ویندوز",
            Foreground = Brushes.SaddleBrown, FontSize = 16, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 4, 6, 14)
        };
        Header(summary);

        var actions = new WrapPanel();
        nativeCreate = NativeButton("＋ صدور فاکتور", () => OpenNativeInvoiceCreate(), true);
        nativeCreate.IsEnabled = false;
        actions.Children.Add(nativeCreate);
        actions.Children.Add(NativeButton("به‌روزرسانی فهرست", () => _ = LoadNativeInvoicePageAsync()));
        actions.Children.Add(NativeDisabledButton("ثبت دریافت", "نیازمند API رسمی پرداخت و کنترل صندوق"));
        actions.Children.Add(NativeDisabledButton("ویرایش / استرداد", "نیازمند API رسمی با کنترل رزرو، پرداخت و تحویل"));
        Header(NativePanel(actions));

        var filters = new WrapPanel();
        nativeSearch = new TextBox { MinWidth = 200, ToolTip = "شماره فاکتور یا مشتری", FlowDirection = FlowDirection.RightToLeft };
        nativeFrom = new DatePicker { MinWidth = 116 };
        nativeTo = new DatePicker { MinWidth = 116 };
        nativeSeller = new ComboBox { MinWidth = 145 };
        nativeType = new ComboBox { MinWidth = 150 };
        nativeStatus = new ComboBox { MinWidth = 140, ItemsSource = new[]
        {
            "همه وضعیت‌ها", "پرداخت‌شده", "پرداخت‌نشده", "پرداخت جزئی",
            "رزرو شده", "تحویل مشتری", "مرجوعی", "لغوشده", "تاریخ نامعتبر"
        }, SelectedIndex = 0 };
        nativePer = new ComboBox { MinWidth = 85, ItemsSource = new[] { "10", "20", "50", "100" }, SelectedIndex = 1 };
        nativeSort = new ComboBox { MinWidth = 140, ItemsSource = new[]
        {
            "جدیدترین ثبت", "شماره فاکتور", "تاریخ", "مشتری", "کارشناس", "مبلغ کل", "دریافتی", "وضعیت"
        }, SelectedIndex = 0 };
        nativeSortDirection = new ComboBox { MinWidth = 100,
            ItemsSource = new[] { "نزولی", "صعودی" }, SelectedIndex = 0 };
        filters.Children.Add(NativeField("شماره / مشتری", nativeSearch, 210));
        filters.Children.Add(NativeField("از تاریخ", nativeFrom, 124));
        filters.Children.Add(NativeField("تا تاریخ", nativeTo, 124));
        filters.Children.Add(NativeField("کارشناس", nativeSeller, 155));
        filters.Children.Add(NativeField("نوع فروش", nativeType, 155));
        filters.Children.Add(NativeField("وضعیت", nativeStatus, 152));
        filters.Children.Add(NativeField("تعداد ردیف", nativePer, 100));
        filters.Children.Add(NativeField("مرتب‌سازی کل نتایج", nativeSort, 155));
        filters.Children.Add(NativeField("جهت", nativeSortDirection, 105));
        filters.Children.Add(NativeButton("جستجو", () => { nativePage = 1; _ = LoadNativeInvoicePageAsync(); }, true));
        filters.Children.Add(NativeButton("حذف فیلتر", () =>
        {
            nativeSearch!.Text = "";
            nativeFrom!.SelectedDate = null; nativeTo!.SelectedDate = null;
            nativeSeller!.SelectedIndex = 0; nativeType!.SelectedIndex = 0;
            nativeStatus!.SelectedIndex = 0; nativePer!.SelectedIndex = 1;
            nativeSort!.SelectedIndex = 0; nativeSortDirection!.SelectedIndex = 0;
            nativePage = 1; _ = LoadNativeInvoicePageAsync();
        }));
        Header(NativePanel(filters));

        nativeNotice = new TextBlock
        {
            Text = "در حال اتصال به فهرست واقعی فاکتورها...",
            Foreground = Brushes.SaddleBrown,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(8, 8, 8, 12)
        };
        Header(nativeNotice);

        var foot = new DockPanel { Margin = new Thickness(5, 7, 5, 2) };
        nativePrevious = NativeButton("صفحه قبل", () =>
        {
            if (nativePage > 1) { nativePage--; _ = LoadNativeInvoicePageAsync(); }
        });
        nativeNext = NativeButton("صفحه بعد", () =>
        {
            if (nativePage < nativePages) { nativePage++; _ = LoadNativeInvoicePageAsync(); }
        });
        var pageBtns = new StackPanel { Orientation = Orientation.Horizontal };
        pageBtns.Children.Add(nativeNext);
        pageBtns.Children.Add(nativePrevious);
        DockPanel.SetDock(pageBtns, Dock.Left);
        foot.Children.Add(pageBtns);
        nativeCount = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        foot.Children.Add(nativeCount);
        DockPanel.SetDock(foot, Dock.Bottom);
        page.Children.Add(foot);

        nativeInvoiceGrid = new DataGrid
        {
            ItemsSource = nativeRows, IsReadOnly = true, AutoGenerateColumns = false,
            CanUserAddRows = false, CanUserDeleteRows = false, CanUserSortColumns = true,
            RowHeight = 42, HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single, Margin = new Thickness(3, 0, 3, 0)
        };
        void Column(string caption, string member, double width)
        {
            nativeInvoiceGrid.Columns.Add(new DataGridTextColumn
            {
                Header = caption, Binding = new Binding(member), Width = new DataGridLength(width)
            });
        }
        Column("شماره فاکتور", nameof(NativeInvoiceRow.Number), 140);
        Column("مشتری", nameof(NativeInvoiceRow.Customer), 200);
        Column("تاریخ", nameof(NativeInvoiceRow.Date), 116);
        Column("کارشناس", nameof(NativeInvoiceRow.Seller), 175);
        Column("مبلغ کل", nameof(NativeInvoiceRow.TotalText), 110);
        Column("دریافتی", nameof(NativeInvoiceRow.PaidText), 110);
        Column("مانده", nameof(NativeInvoiceRow.BalanceText), 110);
        Column("وضعیت پرداخت", nameof(NativeInvoiceRow.Status), 120);
        Column("وضعیت تحویل", nameof(NativeInvoiceRow.Flow), 110);
        var buttonTemplate = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(Button));
        factory.SetValue(Button.ContentProperty, "جزئیات");
        factory.SetValue(Button.MinWidthProperty, 78.0);
        factory.SetValue(Button.HeightProperty, 30.0);
        factory.SetValue(Button.MarginProperty, new Thickness(3));
        factory.SetValue(Button.StyleProperty, (Style)FindResource("Secondary"));
        factory.AddHandler(Button.ClickEvent, new RoutedEventHandler((sender, e) =>
        {
            if (sender is Button b && b.DataContext is NativeInvoiceRow row)
                _ = OpenNativeInvoiceDetailAsync(row.Id);
        }));
        buttonTemplate.VisualTree = factory;
        nativeInvoiceGrid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "عملیات", CellTemplate = buttonTemplate, Width = new DataGridLength(105)
        });
        nativeInvoiceGrid.MouseDoubleClick += (sender, e) =>
        {
            if (nativeInvoiceGrid.SelectedItem is NativeInvoiceRow row)
                _ = OpenNativeInvoiceDetailAsync(row.Id);
        };
        page.Children.Add(nativeInvoiceGrid);

        try
        {
            var meta = await ReadApi(liveGateway, token, "invoice_meta");
            if (!liveMode || section != "فاکتورهای الماس" || version != nativeGeneration) return;
            nativeCanCreate = meta.TryGetProperty("can_create", out var p) && p.ValueKind == JsonValueKind.True;
            nativeSellerChoices.Clear();
            nativeTypeChoices.Clear();
            nativeSellerChoices.Add(new NativePick(0, "همه کارشناسان"));
            nativeTypeChoices.Add(new NativePick(0, "همه انواع فروش"));
            foreach (var x in meta.GetProperty("staff").EnumerateArray())
                nativeSellerChoices.Add(new NativePick(Number(x, "id"), ReadText(x, "name")));
            foreach (var x in meta.GetProperty("sale_types").EnumerateArray())
                nativeTypeChoices.Add(new NativePick(Number(x, "id"), ReadText(x, "name")));
            nativeSeller.ItemsSource = nativeSellerChoices;
            nativeType.ItemsSource = nativeTypeChoices;
            nativeSeller.SelectedIndex = 0;
            nativeType.SelectedIndex = 0;
            nativeCreate.IsEnabled = nativeCanCreate;
            CreateButton.IsEnabled = nativeCanCreate;
            await LoadNativeInvoicePageAsync();
        }
        catch (Exception ex)
        {
            if (version == nativeGeneration && nativeNotice != null)
                nativeNotice.Text = "API فهرست بومی فاکتورها فعال نیست یا پاسخ نمی‌دهد. " +
                    "ابتدا ماژول almas_desktop_bridge نسخه 0.1.8 را نصب کنید. " +
                    "هیچ اطلاعات آزمایشی جایگزین اطلاعات واقعی نمی‌شود. (" + ex.GetType().Name + ")";
        }
    }

    private static string DateValue(DatePicker? picker) =>
        picker?.SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    private static string StatusKey(string? value) => value switch
    {
        "پرداخت‌شده" => "paid", "پرداخت‌نشده" => "unpaid", "پرداخت جزئی" => "partial",
        "رزرو شده" => "reserved", "تحویل مشتری" => "delivered", "مرجوعی" => "returned",
        "لغوشده" => "cancelled", "تاریخ نامعتبر" => "invalid", _ => ""
    };

    private async Task LoadNativeInvoicePageAsync()
    {
        if (!liveMode || nativeInvoiceGrid == null) return;
        var request = ++nativeGeneration;
        try
        {
            var per = nativePer?.SelectedItem?.ToString() ?? "20";
            var sort = new [] { "id", "number", "date", "customer", "seller", "total", "paid", "status" };
            var sortKey = sort[Math.Max(0, Math.Min(sort.Length - 1, nativeSort?.SelectedIndex ?? 0))];
            var sortDirection = nativeSortDirection?.SelectedIndex == 1 ? "asc" : "desc";
            var query = "invoices?page=" + nativePage +
                "&sort=" + sortKey + "&dir=" + sortDirection +
                "&per=" + Uri.EscapeDataString(per) +
                "&q=" + Uri.EscapeDataString((nativeSearch?.Text ?? "").Trim()) +
                "&from=" + Uri.EscapeDataString(DateValue(nativeFrom)) +
                "&to=" + Uri.EscapeDataString(DateValue(nativeTo)) +
                "&staff=" + (nativeSeller?.SelectedItem as NativePick)?.Id +
                "&sale_type=" + (nativeType?.SelectedItem as NativePick)?.Id +
                "&flag=" + Uri.EscapeDataString(StatusKey(nativeStatus?.SelectedItem?.ToString()));
            if (nativeNotice != null) nativeNotice.Text = "در حال دریافت صفحه " + nativePage + " از سرور...";
            var response = await ReadApi(liveGateway, token, query);
            if (!liveMode || request != nativeGeneration || section != "فاکتورهای الماس") return;
            nativeRows.Clear();
            foreach (var x in response.GetProperty("rows").EnumerateArray())
                nativeRows.Add(new NativeInvoiceRow
                {
                    Id = Number(x, "id"), Number = ReadText(x, "number"),
                    Customer = ReadText(x, "customer"), Seller = ReadText(x, "seller"),
                    Date = ReadText(x, "date"), Status = ReadText(x, "status"),
                    Total = ReadNumber(x, "total"), Paid = ReadNumber(x, "paid"),
                    CurrencyId = Number(x, "currency_id"),
                    Reserved = x.TryGetProperty("reserved", out var r) && r.ValueKind == JsonValueKind.True,
                    Delivered = x.TryGetProperty("delivered", out var d) && d.ValueKind == JsonValueKind.True,
                    Returned = x.TryGetProperty("returned", out var ret) && ret.ValueKind == JsonValueKind.True,
                    WarehouseExit = x.TryGetProperty("warehouse_exit", out var wh) && wh.ValueKind == JsonValueKind.True,
                    ReservationConflict = x.TryGetProperty("reservation_conflict", out var rc) && rc.ValueKind == JsonValueKind.True,
                    MissingReservation = x.TryGetProperty("missing_reservation", out var mr) && mr.ValueKind == JsonValueKind.True,
                    InvalidDate = x.TryGetProperty("invalid_date", out var iv) && iv.ValueKind == JsonValueKind.True
                });
            nativePage = Math.Max(1, Number(response, "page"));
            nativePages = Math.Max(1, Number(response, "pages"));
            var count = Number(response, "count");
            if (nativeCount != null)
                nativeCount.Text = "صفحه " + Fa(nativePage.ToString()) + " از " +
                    Fa(nativePages.ToString()) + " • " + Fa(count.ToString()) + " فاکتور";
            if (nativePrevious != null) nativePrevious.IsEnabled = nativePage > 1;
            if (nativeNext != null) nativeNext.IsEnabled = nativePage < nativePages;
            if (nativeNotice != null)
                nativeNotice.Text = "اطلاعات واقعی مرکز فروش • مبالغ بر اساس ارز ثبت‌شده فاکتور؛ " +
                    "هیچ عملیات مالی در این فهرست به‌صورت محلی ثبت نمی‌شود.";
        }
        catch (Exception ex)
        {
            if (request != nativeGeneration) return;
            nativeRows.Clear();
            if (nativeNotice != null) nativeNotice.Text =
                "دریافت فهرست واقعی ناموفق بود. خطا: " + ex.GetType().Name +
                ". اتصال API و نسخه پل را بررسی کنید.";
        }
    }

    private async Task OpenNativeInvoiceDetailAsync(int id)
    {
        if (!liveMode || id <= 0) return;
        try
        {
            var data = await ReadApi(liveGateway, token, "invoice?id=" + id);
            if (!liveMode) return;
            var invoice = data.GetProperty("invoice");
            var title = ReadText(invoice, "number");
            var window = NativeWindow("جزئیات فاکتور " + title, 1050, 730);
            var root = new DockPanel { Margin = new Thickness(15), LastChildFill = true };
            window.Content = root;

            var summary = new WrapPanel();
            foreach (var (caption, value) in new[]
            {
                ("شماره", title), ("مشتری", ReadText(invoice, "customer")),
                ("تاریخ", ReadText(invoice, "date")),
                ("کارشناس", ReadText(invoice, "seller")),
                ("نوع فروش", ReadText(invoice, "sale_type")),
                ("وضعیت", ReadText(invoice, "status")),
                ("کل", Money(ReadNumber(invoice, "total"))),
                ("دریافتی", Money(ReadNumber(invoice, "paid"))),
                ("مانده", Money(ReadNumber(invoice, "total") - ReadNumber(invoice, "paid"))),
                ("روش تحویل", ReadText(invoice, "delivery_method"))
            }) summary.Children.Add(NativeSummaryBox(caption, value));
            DockPanel.SetDock(summary, Dock.Top);
            root.Children.Add(summary);

            var disabled = new WrapPanel();
            disabled.Children.Add(NativeDisabledButton("ویرایش فاکتور", "نیازمند API رسمی و قواعد پرداخت، رزرو و تحویل"));
            disabled.Children.Add(NativeDisabledButton("ثبت دریافت", "نیازمند API رسمی پرداخت و صندوق"));
            disabled.Children.Add(NativeDisabledButton("مرجوعی", "نیازمند API رسمی مرجوعی و موجودی"));
            disabled.Children.Add(NativeDisabledButton("چاپ رسمی", "تا آماده شدن خروجی معتبر PDF از API"));
            DockPanel.SetDock(disabled, Dock.Bottom);
            root.Children.Add(disabled);

            var stack = new StackPanel { Margin = new Thickness(8) };
            stack.Children.Add(NativeTitle("اقلام فاکتور (داده‌های رسمی سرور)"));
            var lines = NativeGrid();
            foreach (var (caption, key, width) in new[]
            {
                ("شرح کالا","description",280.0),("کد","sku",100.0),("رنگ","color",96.0),
                ("اندازه","size",100.0),("تعداد","qty",75.0),("قیمت","rate",105.0),
                ("تخفیف ٪","discount",80.0),("خالص ردیف","net",115.0)
            }) lines.Columns.Add(new DataGridTextColumn { Header = caption, Binding = new Binding(key), Width = new DataGridLength(width) });
            var lineTable = new System.Data.DataTable();
            foreach (var name in new[] { "description","sku","color","size","qty","rate","discount","net" }) lineTable.Columns.Add(name);
            foreach (var x in data.GetProperty("lines").EnumerateArray())
                lineTable.Rows.Add(ReadText(x,"description"),ReadText(x,"sku"),
                    ReadText(x,"color"),ReadText(x,"size"),ReadText(x,"qty"),
                    ReadText(x,"rate"),ReadText(x,"discount"),ReadText(x,"net"));
            lines.ItemsSource = lineTable.DefaultView;
            lines.MinHeight = 140;
            lines.MaxHeight = 300;
            stack.Children.Add(lines);

            stack.Children.Add(NativeTitle("سوابق پرداخت واقعی"));
            var payments = NativeGrid();
            foreach (var (caption,key,width) in new[] { ("تاریخ","date",140.0),("روش","method",190.0),("مبلغ","amount",145.0),("شناسه تراکنش","transaction_id",195.0) })
                payments.Columns.Add(new DataGridTextColumn { Header = caption, Binding = new Binding(key), Width = new DataGridLength(width) });
            var payTable = new System.Data.DataTable();
            foreach (var key in new[] { "date","method","amount","transaction_id" }) payTable.Columns.Add(key);
            foreach (var p in data.GetProperty("payments").EnumerateArray())
                payTable.Rows.Add(ReadText(p,"date"),ReadText(p,"method"),
                    ReadText(p,"amount"),ReadText(p,"transaction_id"));
            payments.ItemsSource = payTable.DefaultView;
            payments.MinHeight = 110;
            stack.Children.Add(payments);
            root.Children.Add(new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "جزئیات فاکتور از سرور دریافت نشد (" + ex.GetType().Name + ").",
                "دریافت جزئیات");
        }
    }

    private void OpenNativeInvoiceCreate()
    {
        if (!liveMode || !nativeCanCreate)
        {
            MessageBox.Show(this, "مجوز ایجاد فاکتور برای این کلید دستگاه تأیید نشده است.");
            return;
        }
        var window = NativeWindow("صدور فاکتور — فرم بومی ویندوز", 1110, 780);
        var root = new DockPanel { Margin = new Thickness(15) };
        window.Content = root;
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        var form = new StackPanel { Margin = new Thickness(6) };
        scroll.Content = form;
        form.Children.Add(NativeTitle("صدور فاکتور در ویندوز — آماده‌سازی اقلام با داده‌های واقعی"));
        var warning = new TextBlock
        {
            Text = "توجه: به دلیل فعال نبودن API رسمی ثبت فاکتور، این فرم فعلاً برای بررسی انتخاب‌هاست. " +
                "هیچ فاکتور یا پرداختی روی سرور ایجاد نمی‌شود؛ قیمت و موجودی فروش نیز نباید حدس زده شوند.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.SaddleBrown, Margin = new Thickness(4, 8, 4, 15)
        };
        form.Children.Add(warning);

        var head = new WrapPanel();
        var saleType = new ComboBox { ItemsSource = nativeTypeChoices.Skip(1).ToArray(), MinWidth = 180, SelectedIndex = -1 };
        var seller = new ComboBox { ItemsSource = nativeSellerChoices.Skip(1).ToArray(), MinWidth = 190, SelectedIndex = -1 };
        var date = new DatePicker { SelectedDate = DateTime.Today };
        var due = new DatePicker { SelectedDate = DateTime.Today };
        var notes = new TextBox { MinWidth = 220, MaxLength = 400 };
        head.Children.Add(NativeField("نوع فروش", saleType, 190));
        head.Children.Add(NativeField("کارشناس", seller, 190));
        head.Children.Add(NativeField("تاریخ فاکتور", date, 150));
        head.Children.Add(NativeField("سررسید", due, 150));
        head.Children.Add(NativeField("یادداشت", notes, 220));
        form.Children.Add(NativePanel(head));

        var customerGroup = new WrapPanel();
        var customerSearch = new TextBox { MinWidth = 230, ToolTip = "حداقل دو حرف از نام مشتری یا شماره تلفن" };
        var customerChoices = new ComboBox { MinWidth = 290 };
        customerGroup.Children.Add(NativeField("جستجوی مشتری", customerSearch, 240));
        customerGroup.Children.Add(NativeButton("جستجوی مشتری", () =>
        {
            _ = FillNativeChoicesAsync("customers", customerSearch.Text, customerChoices);
        }));
        customerGroup.Children.Add(NativeField("مشتری انتخاب‌شده", customerChoices, 295));
        form.Children.Add(NativePanel(customerGroup));

        form.Children.Add(NativeTitle("انتخاب کالاها"));
        var pick = new WrapPanel();
        var productSearch = new TextBox { MinWidth = 230, ToolTip = "نام یا کد کالا" };
        var productChoices = new ComboBox { MinWidth = 460 };
        var collections = new ComboBox { MinWidth = 165 };
        var colors = new ComboBox { MinWidth = 155 };
        var sizes = new ComboBox { MinWidth = 150 };
        bool updatingFacets = false;
        async Task RefreshFacets()
        {
            if (updatingFacets || !liveMode) return;
            updatingFacets = true;
            try
            {
                int Id(ComboBox box) => (box.SelectedItem as NativePick)?.Id ?? 0;
                var response = await ReadApi(liveGateway, token, "product_filters?collection=" + Id(collections) +
                    "&color=" + Id(colors) + "&size=" + Id(sizes));
                void Fill(ComboBox box, string key, string title)
                {
                    var current = (box.SelectedItem as NativePick)?.Id ?? 0;
                    var options = new List<NativePick> { new(0, title) };
                    foreach (var item in response.GetProperty(key).EnumerateArray())
                        options.Add(new NativePick(Number(item, "id"), ReadText(item, "name")));
                    box.ItemsSource = options;
                    box.SelectedItem = options.FirstOrDefault(x => x.Id == current) ?? options[0];
                }
                Fill(collections, "collections", "همه کلکسیون‌ها");
                Fill(colors, "colors", "همه رنگ‌ها");
                Fill(sizes, "sizes", "همه اندازه‌ها");
            }
            catch (Exception ex)
            {
                MessageBox.Show(window, "فیلترهای کالا از سرور دریافت نشد (" + ex.GetType().Name +
                    "). نسخه رابط سرور و دسترسی را بررسی کنید.");
            }
            finally { updatingFacets = false; }
        }
        collections.SelectionChanged += (sender, e) => { if (!updatingFacets) _ = RefreshFacets(); };
        colors.SelectionChanged += (sender, e) => { if (!updatingFacets) _ = RefreshFacets(); };
        sizes.SelectionChanged += (sender, e) => { if (!updatingFacets) _ = RefreshFacets(); };
        _ = RefreshFacets();
        var qty = new TextBox { Text = "1", MinWidth = 95, FlowDirection = FlowDirection.LeftToRight };
        var discount = new TextBox { Text = "0", MinWidth = 95, FlowDirection = FlowDirection.LeftToRight };
        pick.Children.Add(NativeField("کد یا نام کالا", productSearch, 240));
        pick.Children.Add(NativeButton("جستجوی کالا", () =>
        {
            _ = FillNativeProductsAsync(productSearch.Text, productChoices, collections, colors, sizes);
        }));
        pick.Children.Add(NativeField("کلکسیون", collections, 180));
        pick.Children.Add(NativeField("رنگ", colors, 167));
        pick.Children.Add(NativeField("اندازه", sizes, 162));
        pick.Children.Add(NativeField("کالای انتخابی و موجودی قابل فروش", productChoices, 475));
        pick.Children.Add(NativeField("تعداد", qty, 105));
        pick.Children.Add(NativeField("تخفیف درخواست‌شده ٪", discount, 116));
        form.Children.Add(NativePanel(pick));

        var selected = new ObservableCollection<NativeDraftLine>();
        var selectedGrid = NativeGrid();
        selectedGrid.MinHeight = 140;
        selectedGrid.MaxHeight = 300;
        selectedGrid.ItemsSource = selected;
        foreach (var (caption,prop,width) in new[]
        {
            ("شناسه کالا","ItemId",110.0),("شرح کالا","Product",350.0),
            ("تعداد","QuantityText",105.0),("درصد تخفیف","DiscountText",125.0)
        }) selectedGrid.Columns.Add(new DataGridTextColumn { Header = caption, Binding = new Binding(prop), Width = new DataGridLength(width) });
        var action = new WrapPanel();
        action.Children.Add(NativeButton("افزودن قلم", () =>
        {
            if (productChoices.SelectedItem is not NativeProductPick choice)
            {
                MessageBox.Show(window, "ابتدا یک کالای معتبر انتخاب کنید."); return;
            }
            if (!decimal.TryParse(Normalize(qty.Text).Replace("٬","").Replace(",",""), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount)
                || amount <= 0 || amount > 999999)
            {
                MessageBox.Show(window, "تعداد معتبر وارد کنید."); return;
            }
            if (!decimal.TryParse(Normalize(discount.Text).Replace("٬","").Replace(",",""), NumberStyles.Any, CultureInfo.InvariantCulture, out var pct)
                || pct < 0 || pct > 100)
            {
                MessageBox.Show(window, "درصد تخفیف باید بین صفر تا صد باشد."); return;
            }
            if (choice.Stock is decimal available && amount > available)
            {
                MessageBox.Show(window, "موجودی قابل فروش کافی نیست. موجودی فعلی: " +
                    Money(available) + " | تعداد درخواستی: " + Money(amount));
                return;
            }
            if (selected.Any(x => x.ItemId == choice.Id))
            {
                MessageBox.Show(window, "این کالا قبلاً اضافه شده است."); return;
            }
            selected.Add(new NativeDraftLine { ItemId = choice.Id, Product = choice.Name, Quantity = amount, Discount = pct });
        }, true));
        action.Children.Add(NativeButton("حذف ردیف انتخابی", () =>
        {
            if (selectedGrid.SelectedItem is NativeDraftLine line) selected.Remove(line);
        }));
        action.Children.Add(NativeDisabledButton("ثبت نهایی فاکتور",
            "ثبت مالی تا تکمیل API معتبر مرکز فروش و محاسبه رسمی قیمت، تخفیف و رزرو غیرفعال است"));
        form.Children.Add(NativePanel(action));
        form.Children.Add(selectedGrid);
        form.Children.Add(new TextBlock
        {
            Text = "موجودی قابل فروش از دفتر جدید انبار و رزروهای فعال خوانده می‌شود. نرخ نوع فروش و تخفیف مجاز باید از Quote رسمی مرکز فروش دریافت و در ثبت نهایی دوباره تأیید شوند.",
            Foreground = Brushes.SaddleBrown, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 12, 8, 10)
        });
        window.ShowDialog();
    }

    private async Task FillNativeProductsAsync(
        string query, ComboBox target, ComboBox collections, ComboBox colors, ComboBox sizes)
    {
        if (!liveMode) return;
        int Id(ComboBox c) => (c.SelectedItem as NativePick)?.Id ?? 0;
        if (query.Trim().Length < 2 && Id(collections) == 0 && Id(colors) == 0 && Id(sizes) == 0)
        {
            MessageBox.Show(this, "حداقل دو حرف از کد یا نام کالا وارد کنید یا یک فیلتر انتخاب کنید.");
            return;
        }
        try
        {
            var trimmed = query.Trim();
            var path = "products?q=" +
                Uri.EscapeDataString(trimmed[..Math.Min(trimmed.Length, 75)]) +
                "&collection=" + Id(collections) + "&color=" + Id(colors) + "&size=" + Id(sizes);
            var response = await ReadApi(liveGateway, token, path);
            var options = new List<NativeProductPick>();
            foreach (var item in response.GetProperty("items").EnumerateArray())
            {
                decimal? stock = item.TryGetProperty("stock", out var value) &&
                    value.ValueKind == JsonValueKind.Number ? ReadNumber(item, "stock") : null;
                var code = ReadText(item, "code");
                var label = ReadText(item, "name") +
                    " | کد " + code +
                    " | " + ReadText(item, "group_name") +
                    " | " + ReadText(item, "color_name") +
                    " | " + ReadText(item, "size_name") +
                    " | قابل فروش: " + (stock.HasValue ? Money(stock.Value) : "معاف از کنترل انبار");
                options.Add(new NativeProductPick(Number(item, "id"), Clean(label), stock, code));
            }
            target.ItemsSource = options;
            target.SelectedIndex = options.Count == 1 ? 0 : -1;
            if (options.Count == 0) MessageBox.Show(this,
                "کالایی مطابق مشخصات در مدیریت کالا یافت نشد.");
        }
        catch (Exception ex)
        {
            // An unreliable stock check must fail closed.
            target.ItemsSource = Array.Empty<NativeProductPick>();
            MessageBox.Show(this,
                "جستجوی کالا یا موجودی قابل فروش در دسترس نیست (" +
                ex.GetType().Name + "). انتخاب محصول تا رفع مشکل انبار مسدود است.");
        }
    }

    private async Task FillNativeChoicesAsync(string type, string query, ComboBox target)
    {
        if (!liveMode || query.Trim().Length < 2)
        {
            MessageBox.Show(this, "حداقل دو حرف یا رقم برای جستجو وارد کنید."); return;
        }
        try
        {
            var result = await ReadApi(liveGateway, token,
                type + "?q=" + Uri.EscapeDataString(query.Trim()[..Math.Min(query.Trim().Length, 75)]));
            var choices = new List<NativePick>();
            foreach (var i in result.GetProperty("items").EnumerateArray())
                choices.Add(new NativePick(Number(i, "id"), Clean(
                    ReadText(i, "name") + (type == "customers" && ReadText(i, "extra") != "" ?
                        " | " + ReadText(i, "extra") : ""))));
            target.ItemsSource = choices;
            target.SelectedIndex = choices.Count == 1 ? 0 : -1;
            if (choices.Count == 0) MessageBox.Show(this, "موردی در سامانه یافت نشد.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "جستجو در سامانه ناموفق بود (" + ex.GetType().Name + ").");
        }
    }

    private async Task OpenNativeDetailByNumberAsync(string number)
    {
        if (string.IsNullOrWhiteSpace(number) || !liveMode) return;
        try
        {
            var response = await ReadApi(liveGateway, token,
                "invoices?q=" + Uri.EscapeDataString(number) + "&page=1&per=20");
            var match = response.GetProperty("rows").EnumerateArray()
                .FirstOrDefault(x => string.Equals(ReadText(x, "number"), number, StringComparison.OrdinalIgnoreCase));
            var id = match.ValueKind == JsonValueKind.Object ? Number(match, "id") : 0;
            if (id > 0) await OpenNativeInvoiceDetailAsync(id);
            else MessageBox.Show(this, "فاکتور در نتایج مجاز پیدا نشد.");
        }
        catch
        {
            MessageBox.Show(this, "خواندن جزئیات فاکتور از سامانه ناموفق بود.");
        }
    }

    private static DataGrid NativeGrid() => new()
    {
        IsReadOnly = true, AutoGenerateColumns = false,
        CanUserAddRows = false, CanUserDeleteRows = false,
        CanUserSortColumns = true, RowHeight = 38,
        HeadersVisibility = DataGridHeadersVisibility.Column
    };

    private Window NativeWindow(string title, int width, int height) => new()
    {
        Owner = this, Title = title, Width = width, Height = height,
        MinWidth = 770, MinHeight = 520,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        FlowDirection = FlowDirection.RightToLeft,
        FontFamily = FontFamily, FontSize = 12, Background = Background
    };

    private static TextBlock NativeTitle(string value) => new()
    {
        Text = value, FontSize = 17, FontWeight = FontWeights.SemiBold,
        Margin = new Thickness(6, 12, 6, 10)
    };

    private Border NativePanel(UIElement element) => new()
    {
        Style = (Style)FindResource("Card"),
        Child = element, Margin = new Thickness(2, 0, 2, 9), Padding = new Thickness(10)
    };

    private static StackPanel NativeField(string caption, Control control, double width)
    {
        var result = new StackPanel { Width = width, Margin = new Thickness(3, 3, 3, 7) };
        result.Children.Add(new TextBlock { Text = caption, Margin = new Thickness(2, 0, 2, 5) });
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        result.Children.Add(control);
        return result;
    }

    private Button NativeButton(string caption, Action onClick, bool primary = false)
    {
        var btn = new Button
        {
            Content = caption, Style = (Style)FindResource(primary ? "Primary" : "Secondary"),
            Margin = new Thickness(4), MinWidth = 110, Height = 36
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private Button NativeDisabledButton(string label, string reason) => new()
    {
        Content = label, Style = (Style)FindResource("Secondary"), Margin = new Thickness(4),
        MinWidth = 115, Height = 36, IsEnabled = false, ToolTip = reason
    };

    private static Border NativeSummaryBox(string label, string value) => new()
    {
        Child = new StackPanel
        {
            Children =
            {
                new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.Gray },
                new TextBlock { Text = value, FontSize = 14, FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap }
            }
        },
        Background = Brushes.FloralWhite,
        BorderBrush = Brushes.Bisque, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(9),
        Padding = new Thickness(12), Width = 180, Margin = new Thickness(4)
    };
}
