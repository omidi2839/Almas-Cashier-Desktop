using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlmasCashierDesktop;

public partial class WpfDashboard
{
    private bool liveMode;
    private string token="", liveGateway="";
    private int generation;
    private bool busy;
    private JsonElement liveData;
    private readonly List<Invoice> liveInvoices=new();
    private readonly List<(string Group,string Label,string Id)> liveMenu=new();
    private static readonly HttpClient Reader=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(25)};
    private static string ReadText(JsonElement el,string key)=>el.TryGetProperty(key,out var v)?(v.ValueKind==JsonValueKind.String?v.GetString()??"":v.ToString()):"";
    private static decimal ReadNumber(JsonElement el,string key)=>decimal.TryParse(ReadText(el,key),NumberStyles.Any,CultureInfo.InvariantCulture,out var n)?n:0;
    private async Task<JsonElement> ReadApi(string host,string secret,string route)
    {
        if(!ValidGateway(host))throw new InvalidOperationException("نشانی HTTPS نامعتبر است.");
        using var req=new HttpRequestMessage(HttpMethod.Get,host.TrimEnd('/')+"/almas_desktop_bridge/api/"+route);
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);
        using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(25));
        using var res=await Reader.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct.Token);
        if(!res.IsSuccessStatusCode)throw new InvalidOperationException(res.StatusCode==System.Net.HttpStatusCode.Unauthorized||res.StatusCode==System.Net.HttpStatusCode.Forbidden?"کلید دستگاه یا دسترسی کاربر معتبر نیست.":"ارتباط با درگاه برقرار نشد. کد "+(int)res.StatusCode);
        await using var stream=await res.Content.ReadAsStreamAsync(ct.Token);
        using var buffer=new MemoryStream();var bytes=new byte[8192];int count;
        while((count=await stream.ReadAsync(bytes.AsMemory(),ct.Token))>0){if(buffer.Length+count>4*1024*1024)throw new InvalidOperationException("حجم پاسخ بیش از حد مجاز است.");buffer.Write(bytes,0,count);}
        using var doc=JsonDocument.Parse(buffer.ToArray());var root=doc.RootElement;
        if(ReadText(root,"service")!="almas_desktop_bridge"||!root.TryGetProperty("success",out var ok)||ok.ValueKind!=JsonValueKind.True)throw new InvalidOperationException("پاسخ درگاه معتبر نیست.");
        return root.Clone();
    }
    private void SetLiveAppearance(bool active)
    {
        ModeLabel.Text=active?"نجف • اطلاعات سامانه • فقط مشاهده":"شعبه نجف  •  محیط آزمایشی";
        TableNote.Text=active?"اطلاعات دریافت‌شده از سرور":"داده‌های نمونه • بدون اثر مالی";
        FooterMode.Text=active?"0.3.1 | WPF بومی • کاتالوگ واقعی":"0.3.1 | پیش‌نمایش محلی WPF";
        ConnectButton.Content=active?"به‌روزرسانی":"اتصال به سامانه";
        CreateButton.IsEnabled=!active;
        PayButton.IsEnabled=ReceiptButton.IsEnabled=!active;
        CreateButton.Content=active?"صدور فاکتور بومی":"＋ ایجاد فاکتور";
        CreateButton.ToolTip=active?"پس از دریافت مجوزهای سرور و ورود به فرم بومی فعال می‌شود":null;
        CreateButton.ToolTip=PayButton.ToolTip=ReceiptButton.ToolTip=active?"ثبت مالی در این نسخه فعال نیست":null;
    }
    private void LiveSettings()
    {
        OtherContent.Children.Add(new TextBlock{Text=liveMode?"اتصال به اطلاعات واقعی برقرار است":"اتصال امن به سامانه الماس",FontSize=18,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,15)});
        OtherContent.Children.Add(FieldLabel("نشانی درگاه HTTPS"));
        var host=new TextBox{Text=liveMode?liveGateway:gateway,FlowDirection=FlowDirection.LeftToRight,MaxWidth=520,MinWidth=330,HorizontalAlignment=HorizontalAlignment.Right,IsReadOnly=liveMode};
        OtherContent.Children.Add(host);
        OtherContent.Children.Add(FieldLabel("کلید اختصاصی دستگاه (از مدیریت اتصال ویندوز)"));
        var key=new PasswordBox{MinHeight=36,MaxWidth=520,MinWidth=330,HorizontalAlignment=HorizontalAlignment.Right,FlowDirection=FlowDirection.LeftToRight,IsEnabled=!liveMode};
        OtherContent.Children.Add(key);
        var button=ActionButton("اتصال و دریافت اطلاعات",()=>{},true);button.IsEnabled=!liveMode;
        button.Click+=async(_,_)=>{
            if(busy)return;var h=host.Text.Trim();var k=key.Password.Trim();
            if(!ValidGateway(h)||k.Length!=64||!k.All(Uri.IsHexDigit)){MessageBox.Show(this,"نشانی HTTPS و کلید ۶۴ رقمی معتبر را وارد کنید.");return;}
            busy=true;button.IsEnabled=false;int g=++generation;SetConnection("در حال اتصال",false);
            try{
                var data=await ReadApi(h,k,"snapshot");
                if(g!=generation)return;
                ApplySnapshot(data);liveMode=true;token=k;liveGateway=h;gateway=h;key.Clear();
                try{Directory.CreateDirectory(CashierConfig.Home);File.WriteAllText(SettingsFile,JsonSerializer.Serialize(new Dictionary<string,string>{{"gateway",gateway}}));}catch{}
                SetLiveAppearance(true);BuildMenu();SetConnection("متصل",true);OpenLive("داشبورد");
            }catch(InvalidOperationException ex){SetConnection("اتصال ناموفق",false);MessageBox.Show(this,ex.Message);}
            catch{SetConnection("اتصال ناموفق",false);MessageBox.Show(this,"درگاه پاسخ نداد. نشانی، گواهی SSL و نصب ماژول را بررسی کنید.");}
            finally{busy=false;button.IsEnabled=!liveMode;}
        };
        OtherContent.Children.Add(button);
        if(liveMode)OtherContent.Children.Add(ActionButton("قطع اتصال و پاک‌کردن داده‌ها",DisconnectLive));
        else{
            OtherContent.Children.Add(new TextBlock{Text="نمای آزمایشی (جدا از اطلاعات واقعی)",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,22,0,6)});
            OtherContent.Children.Add(ActionButton("نمای صندوقدار",()=>ChangeRole("صندوقدار")));
            OtherContent.Children.Add(ActionButton("نمای حسابدار",()=>ChangeRole("حسابدار")));
        }
        OtherContent.Children.Add(new TextBlock{Text="کلید فقط در حافظه همین اجرا نگهداری می‌شود. اطلاعات واقعی روی رایانه ذخیره نمی‌شود. این نسخه فقط برای مشاهده است.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,16,0,0)});
    }
    private void ApplySnapshot(JsonElement data)
    {
        var rows=new List<Invoice>();var entries=new List<(string Group,string Label,string Id)>();
        foreach(var m in data.GetProperty("menu").EnumerateArray())entries.Add((ReadText(m,"group"),ReadText(m,"label"),ReadText(m,"id")));
        foreach(var i in data.GetProperty("invoices").EnumerateArray())rows.Add(new Invoice(ReadText(i,"number"),ReadText(i,"customer"),ReadNumber(i,"total"),ReadNumber(i,"paid"),ReadText(i,"date"),ReadText(i,"status"),ReadText(i,"currency")));
        if(entries.Any(x=>x.Label==""||x.Id=="")||entries.GroupBy(x=>x.Label).Any(x=>x.Count()>1))throw new InvalidOperationException("ساختار منوی سرور نامعتبر است.");
        liveMenu.Clear();liveMenu.AddRange(entries);liveInvoices.Clear();liveInvoices.AddRange(rows);liveData=data;
    }
    private void DisconnectLive()
    {
        ++generation;liveMode=false;token=liveGateway="";liveInvoices.Clear();liveMenu.Clear();liveData=default;
        InvoicesGrid.ItemsSource=null;Metrics.ItemsSource=null;OtherContent.Children.Clear();
        SetLiveAppearance(false);SetConnection("متصل نیست",false);BuildMenu();Navigate("تنظیمات");
    }
    private void BuildLiveMenu()
    {
        RoleLabel.Text=ReadText(liveData,"staff");AddNavLive("داشبورد","");
        string previous="";
        foreach(var m in liveMenu){
            if(m.Group!=previous){Navigation.Children.Add(new TextBlock{Text=m.Group,FontWeight=FontWeights.SemiBold,Foreground=Brushes.SaddleBrown,Margin=new Thickness(0,10,8,5)});previous=m.Group;}
            AddNavLive(m.Label,"");
        }
        AddNavLive("تنظیمات","");
    }
    private void AddNavLive(string name,string ignored)
    {
        var b=new Button{Style=(Style)FindResource("Nav"),Content=new TextBlock{Text=name,Margin=new Thickness(4,0,4,0),VerticalAlignment=VerticalAlignment.Center}};
        b.Click+=(_,_)=>Navigate(name);nav[name]=b;Navigation.Children.Add(b);
    }
    private void RefreshLive()
    {
        if(liveData.ValueKind!=JsonValueKind.Object)return;
        var m=liveData.GetProperty("metrics");
        string V(string k)=>ReadText(m,k)==""?"—":ReadText(m,k);
        Metrics.ItemsSource=new[]{
            new Metric("فروش امروز",V("sales"),"فاکتورهای عادی • IQD","\uE7BF","#FBEBD9","#C76C20"),
            new Metric("دریافتی امروز",V("receipts"),"تاریخ پرداخت • IQD","\uE8C7","#E7F4E5","#39834B"),
            new Metric("صندوق کل",V("cash"),"IQD • در صورت مجوز","\uE8D6","#F3EEDF","#887237"),
            new Metric("فاکتورهای باز",V("open"),"فاکتورهای عادی","\uE8A5","#FBEBD9","#C76C20")
        };
        var q=Normalize(SearchBox.Text);var rows=liveInvoices.Where(i=>Normalize(i.Number).Contains(q,StringComparison.OrdinalIgnoreCase)||Normalize(i.Customer).Contains(q,StringComparison.OrdinalIgnoreCase)).ToList();
        InvoicesGrid.ItemsSource=rows;TableCount.Text=Fa(rows.Count.ToString())+" ردیف از آخرین ۱۰۰ فاکتور";
        TableNote.Text="اطلاعات واقعی • "+ReadText(liveData,"as_of");
    }
    private async void OpenLive(string name)
    {
        if(!liveMode)return;
        InvoiceWorkspace.Visibility=Visibility.Collapsed;
        section=name;PageTitle.Text=name;foreach(var pair in nav)pair.Value.Tag=pair.Key==name?"active":null;
        int g=++generation;
        var item=liveMenu.FirstOrDefault(x=>x.Label==name);
        if(item.Id=="invoices"){await OpenNativeInvoiceListAsync();return;}
        bool dash=name=="داشبورد";
        DashboardView.Visibility=dash?Visibility.Visible:Visibility.Collapsed;OtherView.Visibility=dash?Visibility.Collapsed:Visibility.Visible;
        OtherContent.Children.Clear();
        if(dash){TableTitle.Text=name=="داشبورد"?"آخرین فاکتورها":"فاکتورهای الماس";RefreshLive();return;}
        if(string.IsNullOrEmpty(item.Id))return;
        OtherContent.Children.Add(new TextBlock{Text="در حال دریافت اطلاعات..."});
        try{
            var data=await ReadApi(liveGateway,token,"section?id="+Uri.EscapeDataString(item.Id));
            if(!liveMode||g!=generation||section!=name)return;RenderSection(data);SetConnection("متصل",true);
        }catch{
            if(!liveMode||g!=generation)return;OtherContent.Children.Clear();OtherContent.Children.Add(new TextBlock{Text="اطلاعات این بخش دریافت نشد. اتصال و مجوزها را بررسی کنید."});SetConnection("خطا در دریافت",false);
        }
    }
    private void RenderSection(JsonElement data)
    {
        OtherContent.Children.Clear();
        OtherContent.Children.Add(new TextBlock{Text=ReadText(data,"note"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)});
        var table=new DataTable();var names=data.GetProperty("columns").EnumerateArray().Select(x=>x.GetString()??"").ToArray();
        foreach(var name in names)table.Columns.Add(name,typeof(string));
        foreach(var r in data.GetProperty("rows").EnumerateArray()){
            var vals=r.EnumerateArray().Select(x=>(object)(x.ValueKind==JsonValueKind.String?x.GetString()??"":x.ToString())).ToArray();if(vals.Length==names.Length)table.Rows.Add(vals);
        }
        OtherContent.Children.Add(new DataGrid{ItemsSource=table.DefaultView,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,AutoGenerateColumns=true,CanUserSortColumns=true,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeight=42,MaxHeight=510});
    }
    private async Task ReloadLive()
    {
        if(!liveMode||busy)return;busy=true;var current=section;int g=++generation;SetConnection("در حال دریافت",false);
        try{
            var data=await ReadApi(liveGateway,token,"snapshot");
            if(!liveMode||generation!=g||section!=current)return;
            ApplySnapshot(data);BuildMenu();SetConnection("متصل",true);OpenLive(nav.ContainsKey(current)&&current!="تنظیمات"?current:"داشبورد");
        }catch{
            if(!liveMode||generation!=g)return;
            // Never display old financial information as a current response.
            liveInvoices.Clear();liveData=default;Metrics.ItemsSource=null;InvoicesGrid.ItemsSource=null;OtherContent.Children.Clear();SetConnection("دریافت ناموفق؛ اتصال مجدد",false);
            DisconnectLive();SetConnection("اتصال قطع شد",false);
        }finally{busy=false;}
    }
}
