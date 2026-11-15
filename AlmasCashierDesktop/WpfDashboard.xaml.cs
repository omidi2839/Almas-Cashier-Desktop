using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace AlmasCashierDesktop;

public partial class WpfDashboard : Window
{
    private readonly List<Invoice> invoices = new()
    {
        new("DEMO-1158","بازرگانی الرافدین",12500000,12500000,"14:25"),
        new("DEMO-1157","فروشگاه الزهراء",8700000,5000000,"13:30"),
        new("DEMO-1156","مؤسسه النور",6200000,0,"12:45"),
        new("DEMO-1155","فروشگاه بغداد",9800000,9800000,"12:10"),
        new("DEMO-1154","شرکت دجله",15000000,10000000,"11:05"),
        new("DEMO-1153","فروشگاه النخیل",4500000,4500000,"09:30")
    };
    private readonly (string Name,string Icon,bool Accounting)[] menu =
    {
        ("داشبورد","\uE80F",false),("فاکتورها","\uE8A5",false),("ثبت سفارش","\uE7BF",false),("صندوق روزانه","\uE8C7",false),
        ("اسناد دریافتی","\uE896",true),("اسناد پرداختی","\uE898",true),("هزینه‌ها","\uE8EC",true),("تحویل صندوق","\uE8AB",true),
        ("گزارش‌ها","\uE9D9",false),("تنظیمات","\uE713",false)
    };
    private readonly Dictionary<string,Button> nav = new();
    private readonly DispatcherTimer clock = new(){Interval=TimeSpan.FromSeconds(1)};
    private string role="صندوقدار", section="داشبورد", gateway="https://desktop.sejadalmas.iq";
    private bool checking;
    private static readonly HttpClient HealthClient=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(7)};
    private static string SettingsFile=>Path.Combine(CashierConfig.Home,"native-preview.json");
    public WpfDashboard()
    {
        InitializeComponent(); ReadSettings(); BuildMenu(); Navigate("داشبورد");
        clock.Tick+=(_,_)=>UpdateClock(); clock.Start(); UpdateClock(); Closed+=(_,_)=>clock.Stop();
    }
    private static string Fa(string s)=>string.Concat(s.Select(c=>c>='0'&&c<='9'?(char)('۰'+c-'0'):c==','?'٬':c));
    private static string Money(decimal v)=>Fa(v.ToString("N0",CultureInfo.InvariantCulture));
    private static string Normalize(string s)=>string.Concat(s.Trim().Select(c=>c>='۰'&&c<='۹'?(char)('0'+c-'۰'):c>='٠'&&c<='٩'?(char)('0'+c-'٠'):c=='ي'?'ی':c=='ك'?'ک':c));
    public sealed record Invoice(string Number,string Customer,decimal Total,decimal Paid,string Time,string ServerStatus="",string Currency="IQD")
    {
        public decimal Remaining=>Math.Max(0,Total-Paid);
        public string TotalText=>FormatCurrency(Total);
        public string PaidText=>FormatCurrency(Paid);
        public string RemainingText=>FormatCurrency(Remaining);
        private string FormatCurrency(decimal v)=>Currency=="IQD"?Money(v):Fa(v.ToString("N2",CultureInfo.InvariantCulture))+" "+Currency;
        public string Status=>ServerStatus!=""?ServerStatus:Paid>=Total?"تسویه شده":Paid>0?"پرداخت جزئی":"باز";
        public string StatusBackground=>Paid>=Total?"#E5F3E6":Paid>0?"#FFF1D9":"#E7EFF8";
        public string StatusColor=>Paid>=Total?"#277746":Paid>0?"#97601D":"#32699C";
    }
    public sealed record Metric(string Label,string Value,string Note,string Glyph,string Tint,string Color);
    private void UpdateClock()=>ClockLabel.Text=Fa(DateTime.Now.ToString("yyyy/MM/dd   HH:mm",CultureInfo.InvariantCulture));
    private void BuildMenu()
    {
        Navigation.Children.Clear();nav.Clear();
        if(liveMode){BuildLiveMenu();return;}
        foreach(var item in menu)
        {
            if(item.Accounting&&role!="حسابدار")continue;
            var name=item.Name;
            var panel=new Grid();panel.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(27)});panel.ColumnDefinitions.Add(new ColumnDefinition());
            var icon=new TextBlock{Text=item.Icon,FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=15,VerticalAlignment=VerticalAlignment.Center};
            var label=new TextBlock{Text=name,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(label,1);panel.Children.Add(icon);panel.Children.Add(label);
            var b=new Button{Style=(Style)FindResource("Nav"),Content=panel};b.Click+=(_,_)=>Navigate(name);nav[name]=b;Navigation.Children.Add(b);
        }
        RoleLabel.Text=role+" آزمایشی";
    }
    private void Navigate(string name)
    {
        if(liveMode&&name!="تنظیمات"){OpenLive(name);return;}
        section=name;PageTitle.Text=name;
        foreach(var pair in nav)pair.Value.Tag=pair.Key==name?"active":null;
        bool show=name=="داشبورد"||name=="فاکتورها";
        DashboardView.Visibility=show?Visibility.Visible:Visibility.Collapsed;
        OtherView.Visibility=show?Visibility.Collapsed:Visibility.Visible;
        if(show){TableTitle.Text=name=="داشبورد"?"آخرین فاکتورها":"فهرست فاکتورها";Refresh();return;}
        OtherContent.Children.Clear();
        if(name=="تنظیمات"){Settings();return;}
        OtherContent.Children.Add(new TextBlock{Text=name,FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
        OtherContent.Children.Add(new TextBlock{Text="این بخش در نسخه طراحی آزمایشی است. ثبت واقعی و همگام‌سازی مالی هنوز فعال نیست.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
        if(name=="ثبت سفارش")OtherContent.Children.Add(ActionButton("فرم سفارش نمونه",()=>CreateDemo("سفارش نمونه"),true));
        if(name=="اسناد دریافتی")OtherContent.Children.Add(ActionButton("فرم دریافت نمونه",DemoPayment,true));
    }
    private void Refresh()
    {
        if(liveMode){RefreshLive();return;}
        Metrics.ItemsSource=new[]{
            new Metric("فروش امروز",Money(invoices.Sum(i=>i.Total)),"دینار • نمونه","\uE7BF","#FBEBD9","#C76C20"),
            new Metric("دریافتی امروز",Money(invoices.Sum(i=>i.Paid)),"دینار • نمونه","\uE8C7","#E7F4E5","#39834B"),
            new Metric("مانده صندوق","—","بدون اتصال مالی","\uE8D6","#F3EEDF","#887237"),
            new Metric("فاکتورهای باز",Fa(invoices.Count(i=>i.Remaining>0).ToString()),"فاکتور نمونه","\uE8A5","#FBEBD9","#C76C20")
        };
        var q=Normalize(SearchBox.Text);
        var found=invoices.Where(i=>Normalize(i.Number).Contains(q,StringComparison.OrdinalIgnoreCase)||Normalize(i.Customer).Contains(q,StringComparison.OrdinalIgnoreCase)).ToList();
        InvoicesGrid.ItemsSource=found;TableCount.Text=Fa(found.Count.ToString())+" فاکتور نمونه";
    }
    private void Search_Changed(object sender,TextChangedEventArgs e)
    {
        if(SearchHint==null||InvoicesGrid==null)return;
        SearchHint.Visibility=string.IsNullOrEmpty(SearchBox.Text)?Visibility.Visible:Visibility.Collapsed;Refresh();
    }
    private Button ActionButton(string text,Action doIt,bool primary=false)
    {
        var b=new Button{Content=text,Style=(Style)FindResource(primary?"Primary":"Secondary"),HorizontalAlignment=HorizontalAlignment.Right,MinWidth=135,Margin=new Thickness(0,6,0,6)};
        b.Click+=(_,_)=>doIt();return b;
    }
    private void ReadSettings()
    {
        try{if(File.Exists(SettingsFile)){var o=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(SettingsFile));if(o!=null&&o.TryGetValue("gateway",out var g)&&ValidGateway(g))gateway=g;}}catch{}
    }
    private static bool ValidGateway(string? s)=>Uri.TryCreate(s,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.UserInfo==""&&u.Host.Length>0&&u.AbsolutePath=="/"&&u.Query==""&&u.Fragment=="";
    private void Settings()
    {
        LiveSettings();return;
#pragma warning disable CS0162
        OtherContent.Children.Add(new TextBlock{Text="نشانی درگاه امن (HTTPS)",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
        var input=new TextBox{Text=gateway,FlowDirection=FlowDirection.LeftToRight,MaxWidth=600,HorizontalAlignment=HorizontalAlignment.Right};
        OtherContent.Children.Add(input);
        OtherContent.Children.Add(ActionButton("ذخیره نشانی",()=>{
            if(!ValidGateway(input.Text.Trim())){MessageBox.Show(this,"نشانی معتبر HTTPS بدون مسیر وارد کنید.");return;}
            try{Directory.CreateDirectory(CashierConfig.Home);File.WriteAllText(SettingsFile,JsonSerializer.Serialize(new Dictionary<string,string>{{"gateway",input.Text.Trim()}}));gateway=input.Text.Trim();SetConnection("آزمایش نشده",false);}
            catch{MessageBox.Show(this,"ذخیره تنظیمات ناموفق بود.");}
        },true));
        OtherContent.Children.Add(new TextBlock{Text="نمای آزمایشی نقش‌ها",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,22,0,6)});
        OtherContent.Children.Add(ActionButton("نمای صندوقدار",()=>ChangeRole("صندوقدار")));
        OtherContent.Children.Add(ActionButton("نمای حسابدار",()=>ChangeRole("حسابدار")));
        OtherContent.Children.Add(new TextBlock{Text="زبان فعلی فارسی است. تغییر نقش صرفاً نمای آزمایشی را تغییر می‌دهد و مجوز عملیاتی ایجاد نمی‌کند.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,18,0,0)});
    }
    #pragma warning restore CS0162
    private void ChangeRole(string r){role=r;BuildMenu();Navigate("داشبورد");}
    private void SetConnection(string text,bool ok){ConnectionLabel.Text="●  "+text;ConnectionLabel.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(ok?"#277746":"#91612F"));}
    private async void CheckConnection_Click(object sender,RoutedEventArgs e){if(liveMode)await ReloadLive();else Navigate("تنظیمات");}
    private async Task CheckGateway()
    {
        if(checking)return;checking=true;SetConnection("در حال بررسی",false);
        try{
            using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(7));
            using var res=await HealthClient.GetAsync(gateway.TrimEnd('/')+"/almas-cashier-api/health",HttpCompletionOption.ResponseHeadersRead,ct.Token);
            if(!res.IsSuccessStatusCode){SetConnection("API در دسترس نیست",false);return;}
            await using var stream=await res.Content.ReadAsStreamAsync(ct.Token);using var buffer=new MemoryStream();
            var bytes=new byte[4096];int n;while((n=await stream.ReadAsync(bytes.AsMemory(),ct.Token))>0){if(buffer.Length+n>65536){SetConnection("پاسخ نامعتبر",false);return;}buffer.Write(bytes,0,n);}
            using var doc=JsonDocument.Parse(buffer.ToArray());var root=doc.RootElement;
            bool ok=root.TryGetProperty("success",out var a)&&a.ValueKind==JsonValueKind.True&&root.TryGetProperty("service",out var s)&&s.GetString()=="almas_cashier_desktop";
            SetConnection(ok?"API پاسخ‌گو است":"API تأیید نشد",ok);
        }catch{SetConnection("آفلاین / بدون ارتباط",false);}finally{checking=false;}
    }
    private Window Dialog(string title,StackPanel content)
    {
        var w=new Window{Owner=this,Title=title,Width=440,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,FlowDirection=FlowDirection.RightToLeft,FontFamily=FontFamily,FontSize=12,Background=Background,Content=content};
        content.Margin=new Thickness(22);return w;
    }
    private static TextBlock FieldLabel(string s)=>new(){Text=s,Margin=new Thickness(0,12,0,6)};
    private static bool ParseAmount(string s,out decimal v)=>decimal.TryParse(Normalize(s).Replace("٬","").Replace(",",""),NumberStyles.Number,CultureInfo.InvariantCulture,out v)&&v>0&&v<=999999999;
    private void Create_Click(object sender,RoutedEventArgs e)=>CreateDemo("فاکتور نمونه");
    private void CreateDemo(string title)
    {
        if(liveMode)return;
        var p=new StackPanel();var w=Dialog(title,p);p.Children.Add(new TextBlock{Text="داده آزمایشی — بدون اثر مالی",Foreground=Brushes.SaddleBrown});
        p.Children.Add(FieldLabel("مشتری"));var customer=new ComboBox{ItemsSource=new[]{"مشتری نمونه الف","مشتری نمونه ب","مشتری نمونه ج"},SelectedIndex=0};p.Children.Add(customer);
        p.Children.Add(FieldLabel("مبلغ (دینار)"));var amount=new TextBox{Text="100000",FlowDirection=FlowDirection.LeftToRight};p.Children.Add(amount);
        p.Children.Add(ActionButton("ثبت نمونه",()=>{
            if(!ParseAmount(amount.Text,out var v)||v!=decimal.Truncate(v)){MessageBox.Show(w,"مبلغ صحیح دیناری وارد کنید.");return;}
            // Orders are a visual form only; do not mix them into invoice totals.
            if(title=="فاکتور نمونه")invoices.Insert(0,new Invoice("DEMO-"+DateTime.Now.ToString("HHmmssfff"),customer.SelectedItem?.ToString()??"نمونه",v,0,DateTime.Now.ToString("HH:mm")));
            w.DialogResult=true;
        },true));
        if(w.ShowDialog()==true&&section!="ثبت سفارش")Refresh();
    }
    private void Payment_Click(object sender,RoutedEventArgs e)=>DemoPayment();
    private void DemoPayment()
    {
        if(liveMode)return;
        var options=invoices.Where(x=>x.Remaining>0).ToList();if(options.Count==0){MessageBox.Show(this,"فاکتور باز نمونه وجود ندارد.");return;}
        var p=new StackPanel();var w=Dialog("ثبت دریافت نمونه",p);p.Children.Add(new TextBlock{Text="پرداخت آزمایشی — بدون اثر مالی",Foreground=Brushes.SaddleBrown});
        p.Children.Add(FieldLabel("فاکتور"));var select=new ComboBox{ItemsSource=options,DisplayMemberPath="Number",SelectedIndex=0};p.Children.Add(select);
        p.Children.Add(FieldLabel("مبلغ دریافت (دینار)"));var amount=new TextBox{FlowDirection=FlowDirection.LeftToRight,Text=options[0].Remaining.ToString("0",CultureInfo.InvariantCulture)};p.Children.Add(amount);
        select.SelectionChanged+=(_,_)=>{if(select.SelectedItem is Invoice i)amount.Text=i.Remaining.ToString("0",CultureInfo.InvariantCulture);};
        p.Children.Add(ActionButton("ثبت نمونه",()=>{if(select.SelectedItem is not Invoice i||!ParseAmount(amount.Text,out var v)||v!=decimal.Truncate(v)||v>i.Remaining){MessageBox.Show(w,"مبلغ از مانده بیشتر است یا معتبر نیست.");return;}invoices[invoices.IndexOf(i)]=i with{Paid=i.Paid+v};w.DialogResult=true;},true));
        if(w.ShowDialog()==true&&DashboardView.Visibility==Visibility.Visible)Refresh();
    }
    private void Receipt_Click(object sender,RoutedEventArgs e)
    {
        if(liveMode)return;
        var i=InvoicesGrid.SelectedItem as Invoice??invoices.FirstOrDefault();if(i==null)return;
        MessageBox.Show(this,i.Number+"\n"+i.Customer+"\nدریافت نمونه: "+i.PaidText+" دینار\nچاپ رسمی در نسخه آزمایشی فعال نیست.","پیش‌نمایش رسید");
    }
    private void Details_Click(object sender,RoutedEventArgs e)
    {
        if(sender is Button b&&b.DataContext is Invoice i)MessageBox.Show(this,i.Number+"\n"+i.Customer+"\nمبلغ: "+i.TotalText+"\nدریافت: "+i.PaidText+"\nمانده: "+i.RemainingText,liveMode?"جزئیات فاکتور":"جزئیات نمونه");
    }
    internal static void SmokeTest(string? output)
    {
        var window=new WpfDashboard{WindowState=WindowState.Normal};
        try{
            foreach(var size in new[]{new Size(1024,670),new Size(1440,850),new Size(1920,1080)}){
                window.Width=size.Width;window.Height=size.Height;
                window.Measure(size);window.Arrange(new Rect(size));window.UpdateLayout();
                if(window.InvoicesGrid.Columns.Count!=8||window.Metrics.Items.Count!=4)throw new InvalidOperationException("dashboard bindings");
            }
            window.SearchBox.Text="۱۱۵۸";if(window.InvoicesGrid.Items.Count!=1)throw new InvalidOperationException("search");
            window.SearchBox.Text="";window.ChangeRole("حسابدار");if(!window.nav.ContainsKey("هزینه‌ها"))throw new InvalidOperationException("accountant nav");
            window.Navigate("تنظیمات");window.Navigate("داشبورد");
            if(output!=null){window.Width=1440;window.Height=850;window.Measure(new Size(1440,850));window.Arrange(new Rect(0,0,1440,850));window.UpdateLayout();var bmp=new RenderTargetBitmap(1440,850,96,96,PixelFormats.Pbgra32);bmp.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var stream=File.Create(output);encoder.Save(stream);}
        } finally {window.clock.Stop();}
    }
}
