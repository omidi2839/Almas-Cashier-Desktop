using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AlmasCashierDesktop;

// This build is a native UI preview. No real credentials, invoices or financial
// operations are accepted. The legacy WebView remains in source but is not launched.
internal sealed class AlmasDashboard : Form
{
    internal static readonly Color Orange=ColorTranslator.FromHtml("#E87613"), Dark=ColorTranslator.FromHtml("#342A24"),
        Cream=ColorTranslator.FromHtml("#F7F1EA"), White=ColorTranslator.FromHtml("#FFFCF9"),
        Line=ColorTranslator.FromHtml("#E9DED2"), Muted=ColorTranslator.FromHtml("#80746B"),
        Green=ColorTranslator.FromHtml("#277D50");
    private readonly Panel _workspace=new(){Dock=DockStyle.Fill,Padding=new Padding(16),BackColor=Cream};
    private readonly FlowLayoutPanel _menu=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(12,10,8,10)};
    private readonly Label _status=new(){AutoSize=false,TextAlign=ContentAlignment.MiddleCenter,Dock=DockStyle.Fill};
    private readonly Label _profile=new(){AutoSize=false,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};
    private readonly Label _clock=new(){AutoSize=false,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight};
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=1000};
    private readonly Dictionary<string,Button> _nav=new();
    private readonly List<DemoInvoice> _invoices=new(){
        new("DEMO-1158","بازرگانی الرافدین",12500000,12500000,"۱۴:۲۵"),
        new("DEMO-1157","فروشگاه الزهراء",8700000,5000000,"۱۳:۳۰"),
        new("DEMO-1156","مؤسسه النور",6200000,0,"۱۲:۴۵"),
        new("DEMO-1155","فروشگاه بغداد",9800000,9800000,"۱۲:۱۰"),
        new("DEMO-1154","شرکت دجله",15000000,10000000,"۱۱:۰۵"),
        new("DEMO-1153","فروشگاه النخیل",4500000,4500000,"۰۹:۳۰")
    };
    private readonly List<string> _activity=new(){"۱۴:۲۵  فاکتور نمونه ثبت شد","۱۴:۱۰  دریافت نمونه ثبت شد","۱۳:۳۰  پیش‌نمایش رسید باز شد","۱۲:۵۸  صندوق آزمایشی باز شد"};
    private bool _arabic=false; // Language-ready layout; Persian is intentionally the only active translation.
    private string _role="صندوقدار";
    private string _section="داشبورد";
    private string _gateway="https://desktop.sejadalmas.iq";
    private bool _checking;
    private static readonly HttpClient HealthClient=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(7)};
    private static readonly (string Name,string Symbol,bool Accounting)[] MenuItems={
        ("داشبورد","⌂",false),("فاکتورها","▤",false),("ثبت سفارش","＋",false),("صندوق روزانه","▣",false),
        ("اسناد دریافتی","↓",true),("اسناد پرداختی","↑",true),("هزینه‌ها","◇",true),("تحویل صندوق","⇄",true),
        ("گزارش‌ها","▥",false),("تنظیمات","⚙",false)
    };
    private readonly HashSet<string> _tasksDone=new();
    private sealed record DemoInvoice(string Number,string Customer,decimal Total,decimal Paid,string Time);
    public AlmasDashboard(){
        Text="الماس — نسخه ویندوز | پیش‌نمایش 0.2.0";
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        MinimumSize=new Size(1100,690);Size=new Size(1500,900);StartPosition=FormStartPosition.CenterScreen;
        WindowState=FormWindowState.Maximized;Font=new Font("Segoe UI",10);ForeColor=Dark;BackColor=Cream;
        AutoScaleMode=AutoScaleMode.Dpi;
        ReadSettings();BuildShell();Navigate("داشبورد");
        _timer.Tick+=(_,_)=>UpdateClock();_timer.Start();UpdateClock();
        FormClosed+=(_,_)=>_timer.Dispose();
    }
    private void ReadSettings(){
        try{
            var f=Path.Combine(CashierConfig.Home,"native-preview.json");
            if(File.Exists(f)){var x=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(f));
                if(x!=null && x.TryGetValue("gateway",out var g)&&ValidGateway(g))_gateway=g;}
        }catch{/* A broken local preview preference never prevents startup. */}
    }
    private static bool ValidGateway(string? s)=>Uri.TryCreate(s,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.UserInfo==""&&u.Host.Length>0&&u.AbsolutePath=="/"&&u.Query==""&&u.Fragment=="";
    private void SaveGateway(string value){
        Directory.CreateDirectory(CashierConfig.Home);
        File.WriteAllText(Path.Combine(CashierConfig.Home,"native-preview.json"),JsonSerializer.Serialize(new Dictionary<string,string>{{"gateway",value}}));
        _gateway=value;SetConnection("آزمایش نشده",Muted);
    }
    private static Label L(string text,int size=10,bool bold=false,Color? color=null,ContentAlignment align=ContentAlignment.MiddleRight)
        =>new(){Text=text,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color??Dark,AutoSize=false,Dock=DockStyle.Fill,TextAlign=align,RightToLeft=RightToLeft.Yes};
    private static Button B(string text,Action action,bool primary=false){
        var b=new Button{Text=text,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,Font=new Font("Segoe UI",11,FontStyle.Bold),BackColor=primary?Orange:White,ForeColor=primary?Color.White:Dark,Margin=new Padding(5),RightToLeft=RightToLeft.Yes};
        b.FlatAppearance.BorderColor=primary?Orange:Line;b.Click+=(_,_)=>action();return b;
    }
    private static TableLayoutPanel Grid(int cols,int rows){
        return new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=cols,RowCount=rows,Margin=Padding.Empty,Padding=Padding.Empty,RightToLeft=RightToLeft.No,BackColor=Color.Transparent};
    }
    private static void PercentCols(TableLayoutPanel t,params float[] vals){foreach(var v in vals)t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,v));}
    private static void AbsRows(TableLayoutPanel t,params float[] vals){foreach(var v in vals)t.RowStyles.Add(new RowStyle(SizeType.Absolute,v));}
    private void BuildShell(){
        var root=Grid(2,2);root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,238));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));Controls.Add(root);
        var content=Grid(1,2);content.RowStyles.Add(new RowStyle(SizeType.Absolute,104));content.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.Controls.Add(content,0,0);content.Controls.Add(Header(),0,0);content.Controls.Add(_workspace,0,1);
        var side=Grid(1,3);side.BackColor=White;side.RowStyles.Add(new RowStyle(SizeType.Absolute,106));side.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,76));
        root.Controls.Add(side,1,0);
        var brand=new MotifPanel{Dock=DockStyle.Fill,BackColor=White,Margin=Padding.Empty};
        var bt=Grid(1,2);AbsRows(bt,61,33);bt.Controls.Add(L("◆  الماس",27,true,Orange,ContentAlignment.MiddleCenter),0,0);bt.Controls.Add(L("سیستم فروش و حسابداری",10,false,Muted,ContentAlignment.MiddleCenter),0,1);brand.Controls.Add(bt);side.Controls.Add(brand,0,0);
        side.Controls.Add(_menu,0,1);var foot=new MotifPanel{Dock=DockStyle.Fill,BackColor=White,Margin=Padding.Empty,Skyline=true};side.Controls.Add(foot,0,2);
        var bar=Grid(3,1);PercentCols(bar,32,28,40);bar.BackColor=White;bar.Controls.Add(L("نسخه آزمایشی 0.2.0 — بدون ثبت مالی",9,false,Muted,ContentAlignment.MiddleLeft),0,0);bar.Controls.Add(L("شعبه الماس • نجف، عراق",9,false,Muted,ContentAlignment.MiddleCenter),1,0);bar.Controls.Add(_clock,2,0);root.Controls.Add(bar,0,1);root.SetColumnSpan(bar,2);
        BuildMenu();
    }
    private Control Header(){
        var h=new MotifPanel{Dock=DockStyle.Fill,BackColor=White,Margin=Padding.Empty,Skyline=true};
        var t=Grid(3,1);t.Padding=new Padding(16,12,16,12);PercentCols(t,30,34,36);h.Controls.Add(t);
        var profile=Grid(1,2);PercentCols(profile,100);AbsRows(profile,35,37);
        _profile.Font=new Font("Segoe UI",12,FontStyle.Bold);profile.Controls.Add(_profile,0,0);profile.Controls.Add(L("شعبه نجف — داده‌های نمونه",10,false,Muted),0,1);t.Controls.Add(profile,0,0);
        var middle=Grid(2,2);PercentCols(middle,49,51);AbsRows(middle,36,36);
        middle.Controls.Add(B("آزمون اتصال",async()=>await CheckGateway()),0,0);middle.Controls.Add(_status,1,0);middle.Controls.Add(L("آفلاین: پیش‌نمایش محلی",10,true,Muted,ContentAlignment.MiddleCenter),0,1);middle.SetColumnSpan(middle.GetControlFromPosition(0,1)!,2);t.Controls.Add(middle,1,0);
        t.Controls.Add(L("نسخه ویندوز الماس",19,true),2,0);_profile.Text="کاربر آزمایشی | "+_role;SetConnection("متصل نیست",Muted);return h;
    }
    private void SetConnection(string text,Color c){_status.Text="● "+text;_status.ForeColor=c;_status.Font=new Font("Segoe UI",10,FontStyle.Bold);}
    private async Task CheckGateway(){
        if(_checking)return;_checking=true;SetConnection("در حال بررسی",Muted);
        try{
            using var response=await HealthClient.GetAsync(_gateway.TrimEnd('/')+"/almas-cashier-api/health",HttpCompletionOption.ResponseHeadersRead);
            // No bearer token or credentials are sent in this build.
            if(!response.IsSuccessStatusCode){SetConnection("API در دسترس نیست",Orange);return;}
            if(response.Content.Headers.ContentLength is long n && n>65536){SetConnection("پاسخ نامعتبر",Orange);return;}
            await using var stream=await response.Content.ReadAsStreamAsync();using var buf=new MemoryStream();
            var bytes=new byte[4096];int k;while((k=await stream.ReadAsync(bytes.AsMemory()))>0){if(buf.Length+k>65536){SetConnection("پاسخ نامعتبر",Orange);return;}buf.Write(bytes,0,k);}
            using var doc=JsonDocument.Parse(buf.ToArray());var x=doc.RootElement;
            bool ok=x.TryGetProperty("success",out var success)&&success.ValueKind==JsonValueKind.True&&x.TryGetProperty("service",out var s)&&s.GetString()=="almas_cashier_desktop";
            SetConnection(ok?"API پاسخ‌گو است":"API تأیید نشد",ok?Green:Orange);
        }catch{SetConnection("آفلاین / بدون ارتباط",Orange);}finally{_checking=false;}
    }
    private void UpdateClock(){_clock.Text=DateTime.Now.ToString("yyyy/MM/dd    HH:mm:ss",CultureInfo.InvariantCulture)+"     ◷";}
    private void BuildMenu(){
        _menu.Controls.Clear();_nav.Clear();
        foreach(var x in MenuItems){
            if(x.Accounting&&_role!="حسابدار")continue;
            var name=x.Name;var b=B(name+"    "+x.Symbol,()=>Navigate(name));b.Dock=DockStyle.None;b.Width=208;b.Height=46;b.TextAlign=ContentAlignment.MiddleRight;b.Margin=new Padding(0,0,0,5);_menu.Controls.Add(b);_nav[name]=b;
        }
        _profile.Text="کاربر آزمایشی | "+_role;
    }
    private void Navigate(string name){
        _section=name;foreach(var pair in _nav){bool active=pair.Key==name;pair.Value.BackColor=active?Orange:White;pair.Value.ForeColor=active?Color.White:Dark;}
        foreach(Control child in _workspace.Controls.Cast<Control>().ToArray())child.Dispose();_workspace.Controls.Clear();
        _workspace.Controls.Add(name=="داشبورد"?Dashboard():name=="تنظیمات"?Settings():name=="فاکتورها"?InvoicesPage():SectionPage(name));
    }
    private static string Money(decimal n)=>n.ToString("N0",CultureInfo.InvariantCulture);
    private static TableLayoutPanel Card(string heading,int rows=2){
        var grid=Grid(1,rows);grid.BackColor=White;grid.Padding=new Padding(12);grid.Margin=new Padding(5);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute,38));
        grid.Controls.Add(L(heading,13,true),0,0);return grid;
    }
    private Control Metric(string label,string value,string note,Color accent){
        var t=Grid(1,3);t.BackColor=White;t.Margin=new Padding(5);t.Padding=new Padding(14,9,14,9);AbsRows(t,27,41,23);
        t.Controls.Add(L(label,11,true,accent),0,0);t.Controls.Add(L(value,21,true),0,1);t.Controls.Add(L(note,9,false,Muted),0,2);return t;
    }
    private Control Dashboard(){
        var t=Grid(2,1);PercentCols(t,28,72);
        var aside=Grid(1,2);aside.RowStyles.Add(new RowStyle(SizeType.Percent,45));aside.RowStyles.Add(new RowStyle(SizeType.Percent,55));t.Controls.Add(aside,0,0);
        var tasks=Card("وظایف امروز   ☷");tasks.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var list=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(4),RightToLeft=RightToLeft.Yes};
        foreach(string task in new[]{"بررسی فاکتورهای باز","کنترل دریافت‌های امروز","آماده‌سازی گزارش روزانه","بررسی وضعیت صندوق"}){
            var cb=new CheckBox{Text=task,AutoSize=true,Margin=new Padding(6,12,6,5),Checked=_tasksDone.Contains(task)};
            cb.CheckedChanged+=(_,_)=>{if(cb.Checked)_tasksDone.Add(task);else _tasksDone.Remove(task);};list.Controls.Add(cb);}
        tasks.Controls.Add(list,0,1);aside.Controls.Add(tasks,0,0);
        var activity=Card("آخرین فعالیت‌ها   ◷");activity.RowStyles.Add(new RowStyle(SizeType.Percent,100));var events=new ListBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=White,ForeColor=Dark,Font=new Font("Segoe UI",10),RightToLeft=RightToLeft.Yes};events.Items.AddRange(_activity.Take(12).Cast<object>().ToArray());activity.Controls.Add(events,0,1);aside.Controls.Add(activity,0,1);
        var main=Grid(1,3);main.RowStyles.Add(new RowStyle(SizeType.Absolute,126));main.RowStyles.Add(new RowStyle(SizeType.Absolute,68));main.RowStyles.Add(new RowStyle(SizeType.Percent,100));t.Controls.Add(main,1,0);
        var metrics=Grid(4,1);PercentCols(metrics,25,25,25,25);
        metrics.Controls.Add(Metric("فاکتورهای باز",_invoices.Count(x=>x.Total>x.Paid).ToString(),"فاکتور نمونه",Orange),0,0);
        metrics.Controls.Add(Metric("مانده صندوق","—","بدون اتصال مالی",Muted),1,0);
        metrics.Controls.Add(Metric("دریافتی امروز",Money(_invoices.Sum(x=>x.Paid)),"دینار — نمونه",Green),2,0);
        metrics.Controls.Add(Metric("فروش امروز",Money(_invoices.Sum(x=>x.Total)),"دینار — نمونه",Orange),3,0);main.Controls.Add(metrics,0,0);
        var actions=Grid(3,1);PercentCols(actions,33,33,34);actions.Controls.Add(B("چاپ رسید نمونه",ShowReceipt),0,0);actions.Controls.Add(B("ثبت دریافت نمونه",DemoPayment),1,0);actions.Controls.Add(B("ایجاد فاکتور نمونه",DemoCreate,true),2,0);main.Controls.Add(actions,0,1);
        var table=Card("آخرین فاکتورها — داده‌های نمونه   ▤",3);table.RowStyles.Add(new RowStyle(SizeType.Absolute,42));table.RowStyles.Add(new RowStyle(SizeType.Percent,100));table.Controls.Add(B("نمایش همه فاکتورها",()=>Navigate("فاکتورها")),0,1);table.Controls.Add(InvoiceTable(_invoices),0,2);main.Controls.Add(table,0,2);
        return t;
    }
    private DataGridView InvoiceTable(IEnumerable<DemoInvoice> rows){
        var g=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=White,BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,RightToLeft=RightToLeft.Yes,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,EnableHeadersVisualStyles=false,ColumnHeadersHeight=35,RowTemplate={Height=41}};
        g.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=ColorTranslator.FromHtml("#F2E5D8"),ForeColor=Dark,Font=new Font("Segoe UI",10,FontStyle.Bold),Alignment=DataGridViewContentAlignment.MiddleRight};
        g.DefaultCellStyle=new DataGridViewCellStyle{BackColor=White,ForeColor=Dark,SelectionBackColor=ColorTranslator.FromHtml("#F8DFC6"),SelectionForeColor=Dark,Alignment=DataGridViewContentAlignment.MiddleRight};g.GridColor=Line;
        foreach(var name in new[]{"شماره","مشتری","مبلغ","پرداخت","وضعیت","زمان"})g.Columns.Add(name,name);
        g.Columns[1].FillWeight=155;g.Columns[2].FillWeight=115;g.Columns[3].FillWeight=115;g.Columns[5].FillWeight=65;
        foreach(var x in rows){var i=g.Rows.Add(x.Number,x.Customer,Money(x.Total),Money(x.Paid),x.Paid>=x.Total?"تسویه شده":x.Paid>0?"پرداخت جزئی":"باز",x.Time);g.Rows[i].Cells[4].Style.ForeColor=x.Paid>=x.Total?Green:Orange;}
        return g;
    }
    private Control InvoicesPage(){
        var c=Card("فهرست فاکتورهای نمونه",3);c.RowStyles.Add(new RowStyle(SizeType.Absolute,50));c.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var tools=Grid(3,1);PercentCols(tools,24,24,52);tools.Controls.Add(B("ثبت دریافت نمونه",DemoPayment),0,0);tools.Controls.Add(B("ایجاد فاکتور نمونه",DemoCreate,true),1,0);
        var q=new TextBox{Dock=DockStyle.Fill,PlaceholderText="جستجوی شماره یا مشتری نمونه",RightToLeft=RightToLeft.Yes,Margin=new Padding(8,10,8,8)};tools.Controls.Add(q,2,0);c.Controls.Add(tools,0,1);
        var host=new Panel{Dock=DockStyle.Fill};c.Controls.Add(host,0,2);
        void Refresh(){foreach(Control old in host.Controls.Cast<Control>().ToArray())old.Dispose();host.Controls.Add(InvoiceTable(_invoices.Where(x=>x.Number.Contains(q.Text,StringComparison.OrdinalIgnoreCase)||x.Customer.Contains(q.Text,StringComparison.OrdinalIgnoreCase))));}
        q.TextChanged+=(_,_)=>Refresh();Refresh();return c;
    }
    private Control SectionPage(string name){
        var c=Card(name+" — محیط آزمایشی",3);c.RowStyles.Add(new RowStyle(SizeType.Absolute,95));c.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        c.Controls.Add(L("این بخش برای بازبینی چیدمان آماده است. ورود واقعی، ثبت قطعی، تأیید صندوق و همگام‌سازی در این پیش‌نمایش فعال نیست.",13,false,Muted),0,1);
        if(name=="ثبت سفارش"||name=="اسناد دریافتی")c.Controls.Add(B("باز کردن فرم نمونه",name=="ثبت سفارش"?DemoCreate:DemoPayment),0,2);else c.Controls.Add(L("پیش‌نمایش رابط — بدون اثر مالی",16,true,Orange,ContentAlignment.MiddleCenter),0,2);return c;
    }
    private Control Settings(){
        var c=Card("تنظیمات نسخه ویندوز",6);AbsRowsSecondary(c,40,55,55,50);
        c.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        c.Controls.Add(L("نشانی درگاه امن (HTTPS) — فقط آزمون سلامت",11,true),0,1);
        var address=new TextBox{Text=_gateway,Dock=DockStyle.Fill,RightToLeft=RightToLeft.No,Margin=new Padding(6,10,6,12)};c.Controls.Add(address,0,2);
        var row=Grid(2,1);PercentCols(row,50,50);row.Controls.Add(B("آزمون اتصال",async()=>await CheckGateway()),0,0);row.Controls.Add(B("ذخیره نشانی",()=>{if(!ValidGateway(address.Text)){MessageBox.Show("نشانی HTTPS معتبر بدون مسیر وارد کنید.");return;}try{SaveGateway(address.Text.Trim());MessageBox.Show("نشانی ذخیره شد.");}catch{MessageBox.Show("ذخیره تنظیمات ناموفق بود.");}},true),1,0);c.Controls.Add(row,0,3);
        var roles=Grid(2,1);PercentCols(roles,50,50);
        roles.Controls.Add(B("نمای آزمایشی حسابدار",()=>ChangeRole("حسابدار"),_role=="حسابدار"),0,0);roles.Controls.Add(B("نمای آزمایشی صندوقدار",()=>ChangeRole("صندوقدار"),_role=="صندوقدار"),1,0);c.Controls.Add(roles,0,4);
        c.Controls.Add(L("زبان فعلی: فارسی (راست‌چین). عربی در مرحله بعد تکمیل می‌شود.\nتغییر نقش فقط برای مشاهده طرح است و مجوز واقعی ایجاد نمی‌کند.\nهیچ رمز عبور، مبلغ واقعی یا سند مالی در این نسخه به سرور ارسال نمی‌شود.",12,false,Muted,ContentAlignment.TopRight),0,5);return c;
    }
    private static void AbsRowsSecondary(TableLayoutPanel t,params float[] sizes){foreach(var n in sizes)t.RowStyles.Add(new RowStyle(SizeType.Absolute,n));}
    private void ChangeRole(string role){_role=role;BuildMenu();Navigate("داشبورد");}
    private void DemoCreate(){
        using var f=new Form{Text="فاکتور / سفارش نمونه",StartPosition=FormStartPosition.CenterParent,Size=new Size(480,310),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,BackColor=Cream,Font=Font};
        var t=Grid(1,6);t.Padding=new Padding(18);AbsRows(t,35,38,33,40,44,48);f.Controls.Add(t);
        t.Controls.Add(L("تمرین رابط — از اطلاعات واقعی استفاده نکنید",11,true,Orange),0,0);var customer=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,RightToLeft=RightToLeft.Yes};customer.Items.AddRange(new object[]{"مشتری نمونه الف","مشتری نمونه ب","مشتری نمونه ج"});customer.SelectedIndex=0;t.Controls.Add(customer,0,1);t.Controls.Add(L("مبلغ نمونه (دینار)"),0,2);
        var amount=new NumericUpDown{Dock=DockStyle.Fill,Maximum=999999999,Minimum=1,Value=100000,ThousandsSeparator=true};t.Controls.Add(amount,0,3);t.Controls.Add(L("فقط در حافظه این اجرا باقی می‌ماند.",10,false,Muted),0,4);
        t.Controls.Add(B("ثبت نمونه",()=>{_invoices.Insert(0,new("DEMO-"+DateTime.Now.ToString("HHmmssfff"),customer.Text,amount.Value,0,DateTime.Now.ToString("HH:mm")));_activity.Insert(0,"فاکتور نمونه ایجاد شد");f.DialogResult=DialogResult.OK;},true),0,5);if(f.ShowDialog(this)==DialogResult.OK)Navigate(_section);
    }
    private void DemoPayment(){
        var open=_invoices.Where(x=>x.Paid<x.Total).ToArray();if(open.Length==0){MessageBox.Show("فاکتور باز نمونه وجود ندارد.");return;}
        using var f=new Form{Text="ثبت دریافت نمونه",StartPosition=FormStartPosition.CenterParent,Size=new Size(480,270),FormBorderStyle=FormBorderStyle.FixedDialog,BackColor=Cream,Font=Font};
        var t=Grid(1,5);t.Padding=new Padding(18);AbsRows(t,35,38,32,38,48);f.Controls.Add(t);t.Controls.Add(L("پرداخت آزمایشی — بدون اثر مالی",12,true,Orange),0,0);
        var select=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};select.Items.AddRange(open.Select(x=>(object)(x.Number+" — "+x.Customer)).ToArray());select.SelectedIndex=0;t.Controls.Add(select,0,1);
        t.Controls.Add(L("مبلغ دریافت نمونه"),0,2);var money=new NumericUpDown{Dock=DockStyle.Fill,Minimum=1,Maximum=open[0].Total-open[0].Paid,Value=open[0].Total-open[0].Paid,ThousandsSeparator=true};t.Controls.Add(money,0,3);select.SelectedIndexChanged+=(_,_)=>{var x=open[select.SelectedIndex];money.Maximum=x.Total-x.Paid;money.Value=money.Maximum;};t.Controls.Add(B("ثبت نمونه",()=>{var x=open[select.SelectedIndex];var i=_invoices.IndexOf(x);_invoices[i]=x with{Paid=x.Paid+money.Value};_activity.Insert(0,"دریافت نمونه ثبت شد");f.DialogResult=DialogResult.OK;},true),0,4);if(f.ShowDialog(this)==DialogResult.OK)Navigate(_section);
    }
    private void ShowReceipt(){var x=_invoices.First();MessageBox.Show("رسید نمونه — فاقد اعتبار مالی\n\n"+x.Number+"\n"+x.Customer+"\nمبلغ دریافت نمونه: "+Money(x.Paid)+" دینار\n\nچاپ عملیاتی در این نسخه فعال نیست.","پیش‌نمایش رسید");}
    // Restrained line-art derived from Iraqi geometric ornament and palm silhouettes.
    private sealed class MotifPanel:Panel{
        public bool Skyline{get;set;}
        public MotifPanel(){DoubleBuffered=true;}
        protected override void OnPaint(PaintEventArgs e){
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            using var p=new Pen(Color.FromArgb(35,225,145,66),1);
            for(int x=0;x<Math.Min(Width,200);x+=34)for(int y=0;y<Height;y+=34){g.DrawPolygon(p,new[]{new Point(x+17,y),new Point(x+34,y+17),new Point(x+17,y+34),new Point(x,y+17)});}
            if(!Skyline)return;using var b=new SolidBrush(Color.FromArgb(48,183,128,76));int ground=Height-1,centre=Width/2;
            for(int x=centre-125;x<centre+125;x+=17){int h=10+Math.Abs(x*7%22);g.FillRectangle(b,x,ground-h,12,h);}
            for(int i=0;i<5;i++){int w=44-i*7;g.FillRectangle(b,centre-w/2,ground-12-i*9,w,10);}
            for(int x=centre-106;x<=centre+106;x+=212){g.FillRectangle(b,x,ground-37,4,36);for(int k=-2;k<=2;k++)g.DrawLine(p,x+2,ground-36,x+2+k*7,ground-44+Math.Abs(k)*3);}
        }
    }
}
