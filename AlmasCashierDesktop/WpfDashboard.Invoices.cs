using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Documents;
using Microsoft.Win32;

namespace AlmasCashierDesktop;

public partial class WpfDashboard
{
    public sealed record PreviewLine(string Name,string Code,string Color,string Size,decimal Quantity,decimal Rate,decimal Discount)
    {
        public decimal Gross=>Quantity*Rate;
        public decimal DiscountAmount=>Gross*Discount/100m;
        public decimal Net=>Gross-DiscountAmount;
    }
    public sealed record PreviewPayment(decimal Amount,string Method,string Date);
    private sealed record DemoProduct(string Name,string Code,string Color,string Size,decimal Rate);
    private readonly DemoProduct[] demoProducts={
        new("کلکسیون بهاره","1354","کرم","۲ × ۳",450000),
        new("الگانس صفویه","2217","سیلور","۳ × ۴",690000),
        new("فلورا","1108","سرمه‌ای","۱.۵ × ۲.۲۵",260000),
        new("تابلو فرش چاپی","5076","—","۷۰ × ۵۰",90000),
        new("اردهال ۱۲۰۰","3112","دودی","۲ × ۳",560000),
        new("بهاره G","1354","کرم GOLD","۱ × ۲",180000)
    };
    private readonly string[] demoSellers={"کارشناس نمونه الف","کارشناس نمونه ب","کارشناس نمونه ج"};
    private readonly string[] demoTypes={"حضوری","آنلاین","ارگانی"};
    private readonly string[] demoOrganizations={"سازمان نمونه الف","سازمان نمونه ب"};
    private readonly List<Invoice> demoOrders=new();
    private int demoSequence=2000;
    private DataGrid? previewGrid;
    private ItemsControl? previewMetrics;
    private TextBox? filterSearch;
    private ComboBox? filterSeller,filterType,filterStatus,filterPer;
    private DatePicker? filterFrom,filterTo;
    private TextBlock? previewCount;
    private Button? prevPage,nextPageButton;
    private int previewPage=1,previewPages=1;
    private readonly HashSet<string> selectedPreview=new();
    private void InitializePreviewInvoices()
    {
        var day=DateTime.Today;
        for(int n=0;n<invoices.Count;n++){
            var i=invoices[n];
            invoices[n]=i with{Time=day.AddDays(-n).ToString("yyyy-MM-dd"),Seller=demoSellers[n%3],SaleType=demoTypes[n%3],Organization=n%3==2?demoOrganizations[0]:"",Reserved=n==2||n==4,Delivered=n==0||n==3,
                Lines=new[]{new PreviewLine("کالای نمونه "+(n+1),demoProducts[n].Code,demoProducts[n].Color,demoProducts[n].Size,1,i.Total,0)},
                Payments=i.Paid>0?new[]{new PreviewPayment(i.Paid,"نقدی",day.AddDays(-n).ToString("yyyy-MM-dd"))}:Array.Empty<PreviewPayment>()};
        }
        for(int n=0;n<15;n++){
            var p=demoProducts[n%6];var total=p.Rate*(n%3+1);var paid=n%4==0?total:n%4==1?total/2:0;
            invoices.Add(new Invoice("DEMO-"+(1149-n),"مشتری آزمایشی "+Fa((n+1).ToString()),total,paid,day.AddDays(-6-n).ToString("yyyy-MM-dd")){
                Seller=demoSellers[n%3],SaleType=demoTypes[n%3],Organization=n%3==2?demoOrganizations[1]:"",Delivered=n%4==0,Returned=n==8,Cancelled=n==14,Reserved=n%4!=0&&n!=14,
                Lines=new[]{new PreviewLine(p.Name,p.Code,p.Color,p.Size,n%3+1,p.Rate,0)},Payments=paid>0?new[]{new PreviewPayment(paid,"نقدی",day.AddDays(-6-n).ToString("yyyy-MM-dd"))}:Array.Empty<PreviewPayment>()});
        }
    }
    private TextBlock Label(string text,double size=12)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,5)};
    private Border Panel(UIElement child)=>new(){Style=(Style)FindResource("Card"),Padding=new Thickness(10),Child=child,Margin=new Thickness(0,0,0,8)};
    private Button Cmd(string text,Action action,bool primary=false)
    {
        var b=ActionButton(text,action,primary);b.Margin=new Thickness(0,3,6,3);b.MinWidth=0;return b;
    }
    private Button Pending(string text)
    {
        var b=Cmd(text,()=>{});b.IsEnabled=false;b.ToolTip="نیازمند اتصال HTTPS و پیاده‌سازی عملیات سرور؛ در نمونه غیرفعال";return b;
    }
    private StackPanel Field(string label,UIElement control,double width=155)
    {
        var p=new StackPanel{Width=width,Margin=new Thickness(0,0,8,6)};p.Children.Add(Label(label));p.Children.Add(control);return p;
    }
    private static ComboBox Choice(IEnumerable<string> options,string? selected=null,bool editable=false)
    {
        var a=options.ToArray();var c=new ComboBox{ItemsSource=a,IsEditable=editable,IsTextSearchEnabled=true,MinHeight=34};
        if(selected!=null&&a.Contains(selected))c.SelectedItem=selected;else if(editable)c.Text=selected??"";else if(a.Length>0)c.SelectedIndex=0;return c;
    }
    private static string Value(ComboBox? c)=>c?.IsEditable==true?c.Text.Trim():c?.SelectedItem?.ToString()??"";
    private static DataGridTextColumn Column(string header,string path,double width=110,string? sort=null)=>new(){Header=header,Binding=new Binding(path),SortMemberPath=sort??path,Width=new DataGridLength(width),MinWidth=65,IsReadOnly=true};
    private DataGrid GridTable()=>new(){AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false,CanUserDeleteRows=false,CanUserSortColumns=true,SelectionMode=DataGridSelectionMode.Extended,SelectionUnit=DataGridSelectionUnit.FullRow,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeight=43,GridLinesVisibility=DataGridGridLinesVisibility.None,EnableRowVirtualization=true,BorderThickness=new Thickness(0),Background=Brushes.White};
    private void OpenPreviewInvoiceList()
    {
        if(liveMode)return;
        var root=new DockPanel{LastChildFill=true};
        void Top(UIElement c){DockPanel.SetDock(c,Dock.Top);root.Children.Add(c);}
        var notice=Label("فاکتورهای الماس • محیط آزمایشی مستقل — هیچ سندی به سرور ارسال یا ذخیره نمی‌شود.",13);notice.Foreground=Brushes.SaddleBrown;Top(notice);
        previewMetrics=new ItemsControl();
        var uf=new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.UniformGrid));uf.SetValue(System.Windows.Controls.Primitives.UniformGrid.ColumnsProperty,5);previewMetrics.ItemsPanel=new ItemsPanelTemplate(uf);
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromRgb(255,253,250)));border.SetValue(Border.PaddingProperty,new Thickness(10));border.SetValue(Border.MarginProperty,new Thickness(0,0,7,8));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(10));border.SetValue(Border.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(232,222,213)));border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        var inner=new FrameworkElementFactory(typeof(StackPanel));
        foreach(var (path,size,bold) in new[]{("Label",11.0,false),("Value",19.0,true),("Note",10.0,false)}){
            var tb=new FrameworkElementFactory(typeof(TextBlock));tb.SetBinding(TextBlock.TextProperty,new Binding(path));tb.SetValue(TextBlock.FontSizeProperty,size);tb.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);tb.SetValue(TextBlock.MarginProperty,new Thickness(0,2,0,2));if(bold)tb.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);inner.AppendChild(tb);
        }
        border.AppendChild(inner);previewMetrics.ItemTemplate=new DataTemplate{VisualTree=border};Top(previewMetrics);
        var toolbar=new WrapPanel();
        toolbar.Children.Add(Cmd("＋ ایجاد فاکتور",()=>EditPreviewInvoice(null),true));
        toolbar.Children.Add(Pending("ورود گروهی"));toolbar.Children.Add(Pending("پرداخت گروهی"));
        toolbar.Children.Add(Cmd("چاپ فهرست نمونه",()=>PrintPreviewList(false)));toolbar.Children.Add(Cmd("خروجی CSV (Excel)",()=>ExportPreview(false)));
        Top(Panel(toolbar));
        var filtersPanel=new WrapPanel();
        filterSearch=new TextBox{ToolTip="شماره فاکتور یا مشتری"};
        filterFrom=new DatePicker();filterTo=new DatePicker();
        filterSeller=Choice(new[]{"همه کارشناسان"}.Concat(demoSellers));filterType=Choice(new[]{"همه انواع فروش"}.Concat(demoTypes));
        filterStatus=Choice(new[]{"همه وضعیت‌ها","پرداخت‌شده","پرداخت‌نشده","پرداخت جزئی","رزرو شده","تحویل مشتری","مرجوعی","لغوشده"});
        filterPer=Choice(new[]{"10","20","50","100"},"20");
        filtersPanel.Children.Add(Field("شماره فاکتور / نام مشتری",filterSearch,205));filtersPanel.Children.Add(Field("تاریخ از",filterFrom,125));filtersPanel.Children.Add(Field("تاریخ تا",filterTo,125));
        filtersPanel.Children.Add(Field("کارشناس فروش",filterSeller,165));filtersPanel.Children.Add(Field("نوع فروش",filterType,130));filtersPanel.Children.Add(Field("وضعیت",filterStatus,140));filtersPanel.Children.Add(Field("تعداد نمایش",filterPer,85));
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,6)};
        buttons.Children.Add(Cmd("جستجو",()=>{previewPage=1;RefreshPreviewList();},true));buttons.Children.Add(Cmd("پاک کردن",ClearPreviewFilters));filtersPanel.Children.Add(buttons);
        filterSearch.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){previewPage=1;RefreshPreviewList();}};
        Top(Panel(filtersPanel));
        var holder=new DockPanel{LastChildFill=true};var foot=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};
        previewCount=Label("",12);foot.Children.Add(previewCount);prevPage=Cmd("قبلی",()=>{previewPage--;RefreshPreviewList();});nextPageButton=Cmd("بعدی",()=>{previewPage++;RefreshPreviewList();});foot.Children.Add(prevPage);foot.Children.Add(nextPageButton);
        var selectionBar=new WrapPanel();
        selectionBar.Children.Add(Cmd("انتخاب همه صفحه",()=>{if(previewGrid!=null)previewGrid.SelectAll();}));
        selectionBar.Children.Add(Cmd("لغو انتخاب",()=>{selectedPreview.Clear();previewGrid?.UnselectAll();}));
        selectionBar.Children.Add(Cmd("CSV انتخاب‌شده‌ها",()=>ExportPreview(true)));
        selectionBar.Children.Add(Cmd("چاپ انتخاب‌شده‌ها",()=>PrintPreviewList(true)));
        selectionBar.Children.Add(Cmd("جزئیات",()=>WithSelected(ShowPreviewDetails)));
        selectionBar.Children.Add(Cmd("ثبت پرداخت",()=>WithSelected(PayPreview)));
        selectionBar.Children.Add(Cmd("ویرایش",()=>WithSelected(i=>EditPreviewInvoice(i))));
        selectionBar.Children.Add(Cmd("حذف",()=>WithSelected(DeletePreview)));
        DockPanel.SetDock(selectionBar,Dock.Top);holder.Children.Add(selectionBar);DockPanel.SetDock(foot,Dock.Bottom);holder.Children.Add(foot);
        previewGrid=GridTable();previewGrid.Columns.Add(Column("شماره فاکتور","Number",125));previewGrid.Columns.Add(Column("تاریخ","Time",100));
        previewGrid.Columns.Add(Column("مشتری","Customer",155));previewGrid.Columns.Add(Column("کارشناس فروش","Seller",135));previewGrid.Columns.Add(Column("نوع فروش","SaleType",92));
        previewGrid.Columns.Add(Column("مبلغ کل","TotalText",120,"Total"));previewGrid.Columns.Add(Column("پرداخت","PaidText",110,"Paid"));previewGrid.Columns.Add(Column("وضعیت","CombinedStatus",230));
        var detailFactory=new FrameworkElementFactory(typeof(Button));detailFactory.SetValue(ContentControl.ContentProperty,"جزئیات");detailFactory.SetValue(FrameworkElement.HeightProperty,28.0);detailFactory.SetValue(FrameworkElement.StyleProperty,(Style)FindResource("Secondary"));
        detailFactory.AddHandler(Button.ClickEvent,new RoutedEventHandler((o,e)=>{if(o is Button b&&b.DataContext is Invoice i)ShowPreviewDetails(i);}));
        previewGrid.Columns.Add(new DataGridTemplateColumn{Header="عملیات",Width=new DataGridLength(82),CellTemplate=new DataTemplate{VisualTree=detailFactory},CanUserSort=false});
        previewGrid.MouseDoubleClick+=(_,_)=>{if(previewGrid.SelectedItem is Invoice i)ShowPreviewDetails(i);};
        holder.Children.Add(previewGrid);root.Children.Add(Panel(holder));
        InvoiceWorkspace.Content=root;previewPage=1;RefreshPreviewList();
    }
    private void ClearPreviewFilters()
    {
        if(filterSearch==null)return;filterSearch.Clear();filterFrom!.SelectedDate=filterTo!.SelectedDate=null;
        filterSeller!.SelectedIndex=filterType!.SelectedIndex=filterStatus!.SelectedIndex=0;previewPage=1;selectedPreview.Clear();RefreshPreviewList();
    }
    private static bool MatchStatus(Invoice i,string status)=>status switch{
        "پرداخت‌شده"=>!i.Cancelled&&i.Paid>=i.Total,"پرداخت‌نشده"=>!i.Cancelled&&i.Paid==0,"پرداخت جزئی"=>!i.Cancelled&&i.Paid>0&&i.Paid<i.Total,
        "رزرو شده"=>i.Reserved&&!i.Delivered&&!i.Cancelled&&!i.Returned,"تحویل مشتری"=>i.Delivered,"مرجوعی"=>i.Returned,"لغوشده"=>i.Cancelled,_=>true};
    private List<Invoice> FilteredPreview()
    {
        var q=Normalize(filterSearch?.Text??"");var seller=Value(filterSeller);var type=Value(filterType);var status=Value(filterStatus);
        return invoices.Where(i=>(q==""||Normalize(i.Number).Contains(q,StringComparison.OrdinalIgnoreCase)||Normalize(i.Customer).Contains(q,StringComparison.OrdinalIgnoreCase))
          &&(seller==""||seller.StartsWith("همه")||i.Seller==seller)&&(type==""||type.StartsWith("همه")||i.SaleType==type)&&MatchStatus(i,status)
          &&(filterFrom?.SelectedDate==null||(DateTime.TryParse(i.Time,out var d1)&&d1.Date>=filterFrom.SelectedDate.Value.Date))
          &&(filterTo?.SelectedDate==null||(DateTime.TryParse(i.Time,out var d2)&&d2.Date<=filterTo.SelectedDate.Value.Date))).ToList();
    }
    private void RefreshPreviewList()
    {
        if(previewGrid==null||liveMode)return;
        var all=FilteredPreview();var valid=all.Where(x=>!x.Cancelled).ToList();var ret=valid.Where(x=>x.Returned).ToList();
        Metric M(string label,IEnumerable<Invoice> list)=>new(label,Money(list.Sum(x=>x.Total)),Fa(list.Count().ToString())+" فاکتور • دینار نمونه","","","");
        previewMetrics!.ItemsSource=new[]{
            M("جمع مبالغ فاکتورها",valid),M("مرجوعی‌ها",ret),
            new Metric("دریافتی‌ها",Money(valid.Sum(x=>x.Paid)),Fa(valid.Sum(x=>x.Payments.Count).ToString())+" پرداخت • نمونه","","",""),
            M("فروش آنلاین",valid.Where(x=>x.SaleType=="آنلاین")),M("فروش حضوری",valid.Where(x=>x.SaleType=="حضوری"))};
        int per=int.TryParse(Value(filterPer),out var p)?p:20;
        previewPages=Math.Max(1,(all.Count+per-1)/per);previewPage=Math.Clamp(previewPage,1,previewPages);
        var visible=all.Skip((previewPage-1)*per).Take(per).ToList();previewGrid.ItemsSource=visible;
        previewCount!.Text="نمایش "+Fa(visible.Count.ToString())+" از "+Fa(all.Count.ToString())+" • صفحه "+Fa(previewPage.ToString())+" از "+Fa(previewPages.ToString())+"   ";
        prevPage!.IsEnabled=previewPage>1;nextPageButton!.IsEnabled=previewPage<previewPages;
    }
    private void WithSelected(Action<Invoice> action)
    {
        if(liveMode)return;
        if(previewGrid?.SelectedItem is Invoice i){var found=invoices.FirstOrDefault(x=>x.Number==i.Number);if(found!=null)action(found);}
        else MessageBox.Show(this,"ابتدا یک فاکتور را انتخاب کنید.");
    }
    private List<Invoice> ExportRows(bool selected)=>selected?previewGrid?.SelectedItems.Cast<Invoice>().ToList()??new List<Invoice>():FilteredPreview();
    private void ExportPreview(bool selection)
    {
        if(liveMode)return;var rows=ExportRows(selection);if(rows.Count==0){MessageBox.Show(this,"فاکتوری برای خروجی انتخاب نشده است.");return;}
        var d=new SaveFileDialog{FileName="Almas-DEMO-invoices.csv",Filter="CSV (Excel)|*.csv",DefaultExt=".csv"};if(d.ShowDialog(this)!=true)return;
        string E(string v){if(v.Length>0&&"=+-@\t\r".Contains(v[0]))v="'"+v;return "\""+v.Replace("\"","\"\"")+"\"";}
        try{
            var lines=new List<string>{"\"نمونه - فاقد اعتبار مالی\",\"تاریخ\",\"مشتری\",\"کارشناس\",\"نوع فروش\",\"مبلغ کل IQD\",\"پرداخت IQD\",\"وضعیت\""};
            lines.AddRange(rows.Select(i=>string.Join(",",new[]{i.Number,i.Time,i.Customer,i.Seller,i.SaleType,i.Total.ToString(CultureInfo.InvariantCulture),i.Paid.ToString(CultureInfo.InvariantCulture),i.CombinedStatus}.Select(E))));
            File.WriteAllLines(d.FileName,lines,new UTF8Encoding(true));
        }catch(IOException ex){MessageBox.Show(this,"ذخیره فایل ناموفق: "+ex.Message);}
    }
    private void PrintPreviewList(bool selection)
    {
        if(liveMode)return;var rows=ExportRows(selection);if(rows.Count==0){MessageBox.Show(this,"فاکتوری انتخاب نشده است.");return;}
        ShowPrintPreview("فهرست فاکتورها — آزمایشی",string.Join("\n",rows.Select(i=>i.Number+" | "+i.Customer+" | "+i.TotalText+" د.ع | "+i.CombinedStatus)));
    }
    private void ShowPrintPreview(string title,string text)
    {
        var p=new StackPanel();var w=new Window{Owner=this,Title=title,Width=740,Height=570,WindowStartupLocation=WindowStartupLocation.CenterOwner,FlowDirection=FlowDirection.RightToLeft,FontFamily=FontFamily,Background=Background};
        var doc=new FlowDocument{FontFamily=FontFamily,FontSize=14,FlowDirection=FlowDirection.RightToLeft,PagePadding=new Thickness(25)};
        doc.Blocks.Add(new Paragraph(new Run("الماس — سند آزمایشی / فاقد اعتبار مالی")){FontSize=20,FontWeight=FontWeights.Bold});
        doc.Blocks.Add(new Paragraph(new Run(text)));
        var dock=new DockPanel{Margin=new Thickness(12)};var print=Cmd("چاپ نمونه / PDF با چاپگر ویندوز",()=>{
            var pd=new PrintDialog();if(pd.ShowDialog()==true){doc.PageWidth=pd.PrintableAreaWidth;doc.PageHeight=pd.PrintableAreaHeight;pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,title);}
        },true);DockPanel.SetDock(print,Dock.Bottom);dock.Children.Add(print);dock.Children.Add(new FlowDocumentScrollViewer{Document=doc,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});w.Content=dock;w.ShowDialog();
    }
    private void ReplacePreview(Invoice old,Invoice next)
    {
        var ix=invoices.FindIndex(x=>x.Number==old.Number);
        if(ix>=0)invoices[ix]=next;
        RefreshPreviewList();if(section=="داشبورد")Refresh();
    }
    private void RecordPreviewPayment(Invoice i,decimal amount,string method)
    {
        if(liveMode||!i.CanPay||amount<=0||amount>i.Remaining)return;
        ReplacePreview(i,i with{Paid=i.Paid+amount,Payments=i.Payments.Append(new PreviewPayment(amount,method,DateTime.Now.ToString("yyyy-MM-dd HH:mm"))).ToArray()});
    }
    private void PayPreview(Invoice i)
    {
        if(liveMode)return;if(!i.CanPay){MessageBox.Show(this,"فاکتور قابل پرداخت نیست.");return;}
        var p=new StackPanel();var w=Dialog("ثبت پرداخت نمونه",p);p.Children.Add(Label(i.Number+" • مانده "+i.RemainingText+" دینار"));
        var amount=new TextBox{Text=i.Remaining.ToString(CultureInfo.InvariantCulture),FlowDirection=FlowDirection.LeftToRight};
        var method=Choice(new[]{"نقدی","بانکی","کارت","اقساطی (شرکتی)"});p.Children.Add(FieldLabel("مبلغ دریافت (دینار)"));p.Children.Add(amount);p.Children.Add(FieldLabel("روش پرداخت"));p.Children.Add(method);
        var note=Label("پرداخت اقساطی شرکتی نیازمند تعرفه و قواعد سرور است و در این نمونه ثبت نمی‌شود.");note.Foreground=Brushes.SaddleBrown;p.Children.Add(note);
        p.Children.Add(Cmd("ثبت پرداخت نمونه",()=>{
            if(Value(method)=="اقساطی (شرکتی)"){MessageBox.Show(w,"تعرفه اقساط پس از اتصال امن دریافت می‌شود.");return;}
            if(!ParseAmount(amount.Text,out var v)||v!=decimal.Truncate(v)||v>i.Remaining){MessageBox.Show(w,"مبلغ دیناری معتبر و حداکثر مانده وارد کنید.");return;}
            RecordPreviewPayment(i,v,Value(method));w.DialogResult=true;
        },true));w.ShowDialog();
    }
    private void RefundPreview(Invoice i)
    {
        if(liveMode||i.Paid<=0||i.Paid>=i.Total||i.Delivered||i.Returned||i.Cancelled)return;
        if(MessageBox.Show(this,"استرداد کامل دریافت‌های نمونه و آزاد شدن امکان ویرایش؟","استرداد آزمایشی",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        ReplacePreview(i,i with{Paid=0,Payments=i.Payments.Append(new PreviewPayment(-i.Paid,"استرداد نمونه",DateTime.Now.ToString("yyyy-MM-dd HH:mm"))).ToArray()});
    }
    private void DeletePreview(Invoice i)
    {
        if(liveMode)return;if(!i.CanEdit){MessageBox.Show(this,"فقط فاکتور بدون پرداخت و بدون تحویل/مرجوعی قابل حذف است.");return;}
        if(MessageBox.Show(this,"حذف این فاکتور نمونه و رزرو نمایشی آن؟","حذف نمونه",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        invoices.RemoveAll(x=>x.Number==i.Number);RefreshPreviewList();if(section=="داشبورد")Refresh();
    }
    private void ShowPreviewDetails(Invoice i)
    {
        if(liveMode)return;
        var w=new Window{Owner=this,Title="جزئیات "+i.Number+" — نمونه",Width=900,Height=610,MinWidth=700,WindowStartupLocation=WindowStartupLocation.CenterOwner,FlowDirection=FlowDirection.RightToLeft,FontFamily=FontFamily,Background=Background};
        var p=new StackPanel{Margin=new Thickness(18)};
        p.Children.Add(Label("اطلاعات فاکتور • بدون اثر مالی",17));p.Children.Add(Label(i.Number+" | "+i.Customer+" | "+i.Time+" | "+i.Seller+" | "+i.SaleType,14));
        p.Children.Add(Label(i.CombinedStatus+" | مبلغ کل "+i.TotalText+" | دریافتی "+i.PaidText+" | مانده "+i.RemainingText+" دینار",14));
        var g=LineGrid(i.Lines);g.MaxHeight=250;p.Children.Add(g);p.Children.Add(Label("سوابق پرداخت",15));
        var paid=GridTable();paid.Columns.Add(Column("تاریخ","Date",175));paid.Columns.Add(Column("روش","Method",150));paid.Columns.Add(Column("مبلغ","Amount",145));paid.ItemsSource=i.Payments;paid.MaxHeight=155;p.Children.Add(paid);
        var actions=new WrapPanel();
        void Add(string name,Action run,bool allowed=true){var b=Cmd(name,()=>{w.Close();run();});b.IsEnabled=allowed;actions.Children.Add(b);}
        Add("ویرایش",()=>EditPreviewInvoice(i),i.CanEdit);Add("ثبت پرداخت",()=>PayPreview(i),i.CanPay);
        Add("استرداد دریافت جزئی",()=>RefundPreview(i),i.Paid>0&&i.Paid<i.Total&&!i.Delivered&&!i.Returned&&!i.Cancelled);
        Add("حذف",()=>DeletePreview(i),i.CanEdit);Add("چاپ نمونه",()=>ShowPrintPreview(i.Number,i.Customer+"\n"+string.Join("\n",i.Lines.Select(l=>l.Name+" | "+l.Code+" | "+l.Quantity+" × "+Money(l.Rate)+" | "+Money(l.Net)))+"\nجمع کل: "+i.TotalText+" دینار\nپرداخت: "+i.PaidText));
        p.Children.Add(actions);w.Content=new ScrollViewer{Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
    }
    private DataGrid LineGrid(IEnumerable<PreviewLine> rows)
    {
        var g=GridTable();
        foreach(var (label,path,width) in new[]{("کالا","Name",160.0),("کد","Code",86.0),("رنگ","Color",90.0),("اندازه","Size",99.0),("تعداد","Quantity",75.0),("قیمت","Rate",110.0),("تخفیف ٪","Discount",80.0),("جمع","Net",120.0)})g.Columns.Add(Column(label,path,width));
        g.ItemsSource=rows.ToList();return g;
    }
    private static decimal AutoAdjustment(decimal subtotal)=>Math.Round(subtotal/1000m,0,MidpointRounding.AwayFromZero)*1000m-subtotal;
    private static bool ValidLine(decimal qty,decimal rate,decimal discount)=>qty>0&&qty<=10000&&rate>0&&rate<=999999999&&discount>=0&&discount<=100;
    private static bool ReadNonNegative(TextBox edit,out decimal number)=>decimal.TryParse(Normalize(edit.Text).Replace("٬","").Replace(",",""),NumberStyles.Number,CultureInfo.InvariantCulture,out number)&&number>=0;
    private void EditPreviewInvoice(Invoice? edit,bool order=false)
    {
        if(liveMode)return;
        if(edit!=null&&!edit.CanEdit){MessageBox.Show(this,"ویرایش فقط برای فاکتور بدون پرداخت، بدون تحویل و بدون مرجوعی مجاز است. دریافت جزئی را ابتدا مسترد کنید.");return;}
        var lines=(edit?.Lines??Array.Empty<PreviewLine>()).ToList();
        var w=new Window{Owner=this,Title=(order?"ثبت سفارش":edit==null?"ایجاد فاکتور":"ویرایش فاکتور")+" — نمونه",Width=1120,Height=760,MinWidth=820,MinHeight=580,WindowStartupLocation=WindowStartupLocation.CenterOwner,FlowDirection=FlowDirection.RightToLeft,FontFamily=FontFamily,FontSize=12,Background=Background};
        var root=new DockPanel{Margin=new Thickness(14)};var body=new StackPanel();
        body.Children.Add(Label("اطلاعات اصلی • ثبت فقط در حافظه همین اجرا؛ قیمت و سقف تخفیف نمونه‌اند.",14));
        var top=new WrapPanel();var type=Choice(demoTypes,edit?.SaleType??"حضوری");
        var org=Choice(demoOrganizations,edit?.Organization,true);var orgField=Field("سازمان یا ارگان *",org,210);
        var cust=Choice(invoices.Select(i=>i.Customer).Distinct().Concat(new[]{"مشتری نمونه جدید"}),edit?.Customer??"",true);
        var seller=Choice(demoSellers,edit?.Seller);var marketer=Choice(new[]{"بدون بازاریاب"}.Concat(demoSellers),edit?.Marketer);
        var date=new DatePicker{SelectedDate=edit!=null&&DateTime.TryParse(edit.Time,out var dt)?dt:DateTime.Today};
        var due=new DatePicker{SelectedDate=edit!=null&&DateTime.TryParse(edit.Due,out var dd)?dd:null};
        var delivery=Choice(new[]{"تحویل حضوری","ارسال به مشتری","ارسال از انبار"},edit?.DeliveryMethod);
        var number=new TextBox{Text=edit?.Number??(order?"DEMO-ORDER-":"DEMO-")+(demoSequence+1),IsReadOnly=true,FlowDirection=FlowDirection.LeftToRight};
        var note=new TextBox{Text=edit?.Note??""};var currency=Choice(order?new[]{"IQD","USD"}:new[]{"IQD"},edit?.Currency);
        top.Children.Add(Field("نوع فروش *",type,135));top.Children.Add(orgField);top.Children.Add(Field("بازاریاب",marketer,180));top.Children.Add(Field("مشتری * (جستجو / نام نمونه)",cust,220));
        top.Children.Add(Field("ارز",currency,85));top.Children.Add(Field("مأمور فروش *",seller,195));top.Children.Add(Field("شماره سند",number,150));
        top.Children.Add(Field("روش تحویل کالا",delivery,175));top.Children.Add(Field("تاریخ",date,140));top.Children.Add(Field("سررسید",due,140));top.Children.Add(Field("یادداشت مدیر",note,220));
        void OrgVisibility()=>orgField.Visibility=Value(type)=="ارگانی"?Visibility.Visible:Visibility.Collapsed;
        type.SelectionChanged+=(_,_)=>OrgVisibility();OrgVisibility();body.Children.Add(Panel(top));
        body.Children.Add(Label("فیلتر سریع انتخاب کالا",15));var search=new TextBox{ToolTip="کد یا نام کالا"};
        var collection=Choice(new[]{"همه"}.Concat(demoProducts.Select(p=>p.Name).Distinct()));
        var color=Choice(new[]{"همه"}.Concat(demoProducts.Select(p=>p.Color).Distinct()));var size=Choice(new[]{"همه"}.Concat(demoProducts.Select(p=>p.Size).Distinct()));
        var product=new ComboBox{DisplayMemberPath="Name",MinHeight=36};var qty=new TextBox{Text="1"};var price=new TextBox{Text="0"};var discount=new TextBox{Text="0"};
        void Products(){
            var q=Normalize(search.Text);var results=demoProducts.Where(p=>(Normalize(p.Code).Contains(q,StringComparison.OrdinalIgnoreCase)||Normalize(p.Name).Contains(q,StringComparison.OrdinalIgnoreCase))&&(Value(collection)=="همه"||p.Name==Value(collection))&&(Value(color)=="همه"||p.Color==Value(color))&&(Value(size)=="همه"||p.Size==Value(size))).ToList();
            product.ItemsSource=results;product.SelectedIndex=results.Count>0?0:-1;
        }
        product.SelectionChanged+=(_,_)=>{if(product.SelectedItem is DemoProduct p)price.Text=p.Rate.ToString(CultureInfo.InvariantCulture);};
        search.TextChanged+=(_,_)=>Products();collection.SelectionChanged+=(_,_)=>Products();color.SelectionChanged+=(_,_)=>Products();size.SelectionChanged+=(_,_)=>Products();Products();
        var f=new WrapPanel();f.Children.Add(Field("کد یا نام کالا",search,170));f.Children.Add(Field("کلکسیون",collection,170));f.Children.Add(Field("رنگ",color,115));f.Children.Add(Field("اندازه",size,125));f.Children.Add(Field("نتیجه انتخاب کالا",product,210));
        f.Children.Add(Field("تعداد / مقدار",qty,85));f.Children.Add(Field(order?"قیمت نمونه واحد":"قیمت واحد نمونه",price,120));f.Children.Add(Field("تخفیف ٪",discount,80));body.Children.Add(Panel(f));
        var g=LineGrid(lines);g.Height=220;
        var summary=Label("",15);var adjust=new TextBox{Text=(edit?.Adjustment??0).ToString(CultureInfo.InvariantCulture),Width=125};
        void Update(){g.ItemsSource=lines.ToList();decimal gross=lines.Sum(x=>x.Gross),disc=lines.Sum(x=>x.DiscountAmount),net=gross-disc,auto=currency.SelectedItem?.ToString()=="IQD"?AutoAdjustment(net):0;decimal.TryParse(Normalize(adjust.Text),NumberStyles.Number,CultureInfo.InvariantCulture,out var manual);summary.Text="جمع جزئی: "+Money(gross)+"   |   تخفیفات: "+Money(disc)+"   |   پس از تخفیف: "+Money(net)+"   |   تعدیل خودکار: "+Money(auto)+"\nجمع کل نمونه: "+(currency.SelectedItem?.ToString()=="USD"?(net+auto+manual).ToString("N2",CultureInfo.InvariantCulture):Money(net+auto+manual))+" "+Value(currency);}
        var actions=new WrapPanel();
        actions.Children.Add(Cmd("＋ افزودن کالا",()=>{
            if(product.SelectedItem is not DemoProduct p||!ReadNonNegative(qty,out var q)||!ReadNonNegative(price,out var r)||!ReadNonNegative(discount,out var d)||!ValidLine(q,r,d)||(!order&&q!=decimal.Truncate(q))){MessageBox.Show(w,"کالا، تعداد و نرخ معتبر و درصد تخفیف ۰ تا ۱۰۰ وارد کنید.");return;}
            lines.Add(new PreviewLine(p.Name,p.Code,p.Color,p.Size,q,r,d));Update();
        },true));
        actions.Children.Add(Cmd("حذف ردیف انتخابی",()=>{if(g.SelectedItem is PreviewLine l){lines.Remove(l);Update();}}));
        body.Children.Add(actions);body.Children.Add(g);var adjustmentPanel=new WrapPanel();adjustmentPanel.Children.Add(Field("تعدیل دستی نمونه",adjust,155));adjustmentPanel.Children.Add(summary);body.Children.Add(Panel(adjustmentPanel));
        adjust.TextChanged+=(_,_)=>Update();currency.SelectionChanged+=(_,_)=>Update();Update();
        var bottom=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Left};bottom.Children.Add(Cmd("انصراف",()=>w.Close()));
        bottom.Children.Add(Cmd("ثبت نمونه",()=>{
            if(Value(cust)==""||Value(seller)==""||date.SelectedDate==null||(Value(type)=="ارگانی"&&Value(org)=="")||lines.Count==0){MessageBox.Show(w,"مشتری، کارشناس، تاریخ، سازمان (در فروش ارگانی) و حداقل یک کالا لازم است.");return;}
            if(!decimal.TryParse(Normalize(adjust.Text),NumberStyles.Number,CultureInfo.InvariantCulture,out var manual)||Math.Abs(manual)>5000){MessageBox.Show(w,"تعدیل دستی نمونه باید بین منفی و مثبت ۵۰۰۰ باشد.");return;}
            var net=lines.Sum(l=>l.Net);var total=net+(Value(currency)=="IQD"?AutoAdjustment(net):0)+manual;if(total<=0){MessageBox.Show(w,"جمع کل باید مثبت باشد.");return;}
            var invoice=new Invoice(edit?.Number??(order?"DEMO-ORDER-":"DEMO-")+(++demoSequence),Value(cust),total,edit?.Paid??0,date.SelectedDate.Value.ToString("yyyy-MM-dd"),"",Value(currency)){
                Seller=Value(seller),SaleType=Value(type),Organization=Value(type)=="ارگانی"?Value(org):"",DeliveryMethod=Value(delivery),Marketer=Value(marketer),Note=note.Text,Due=due.SelectedDate?.ToString("yyyy-MM-dd")??"",Adjustment=manual,Reserved=!order,Lines=lines.ToArray(),Payments=edit?.Payments??Array.Empty<PreviewPayment>()};
            if(order){demoOrders.Insert(0,invoice);}else if(edit!=null){ReplacePreview(edit,invoice);}else invoices.Insert(0,invoice);
            w.DialogResult=true;
        },true));DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});w.Content=root;
        if(w.ShowDialog()==true){if(order){Navigate("ثبت سفارش");}else{RefreshPreviewList();if(section=="داشبورد")Refresh();}}
    }
    private void RenderPreviewSection(string name)
    {
        // These are local navigation and sample tables, not server transactions.
        if(name=="ثبت سفارش"){
            OtherContent.Children.Add(Cmd("＋ ثبت سفارش نمونه",()=>EditPreviewInvoice(null,true),true));
            var g=GridTable();g.Columns.Add(Column("شماره","Number",150));g.Columns.Add(Column("مشتری","Customer",160));g.Columns.Add(Column("تاریخ","Time"));g.Columns.Add(Column("مبلغ","TotalText"));g.Columns.Add(Column("ارز","Currency",70));g.ItemsSource=demoOrders.ToList();g.MaxHeight=420;OtherContent.Children.Add(g);return;
        }
        if(name=="خروج و ارسال کالا"||name=="مرجوعی کالا"){
            var g=GridTable();g.Columns.Add(Column("فاکتور","Number",130));g.Columns.Add(Column("مشتری","Customer",170));g.Columns.Add(Column("روش تحویل","DeliveryMethod",150));g.Columns.Add(Column("وضعیت","CombinedStatus",230));
            g.ItemsSource=invoices.Where(x=>!x.Cancelled&&(name=="مرجوعی کالا"?x.Delivered||x.Returned:x.Reserved||x.Delivered)).ToList();g.MaxHeight=450;OtherContent.Children.Add(g);return;
        }
        if(name=="دریافت و پرداخت"||name=="مدیریت صندوق"||name=="نمای کلی"||name=="دریافتی از فروشندگان"){
            var tabs=new TabControl{Margin=new Thickness(0,8,0,8)};
            var receipts=GridTable();receipts.Columns.Add(Column("شماره","Number",140));receipts.Columns.Add(Column("مشتری","Customer",180));receipts.Columns.Add(Column("دریافتی نمونه","PaidText",160,"Paid"));receipts.Columns.Add(Column("کارشناس","Seller",170));receipts.ItemsSource=invoices.Where(x=>!x.Cancelled&&x.Paid>0).ToList();receipts.MaxHeight=420;tabs.Items.Add(new TabItem{Header="دریافتی فاکتورها (نمونه)",Content=receipts});
            if(name=="دریافت و پرداخت")foreach(var title in new[]{"اسناد پرداختی","هزینه‌ها","سرفصل‌ها"})tabs.Items.Add(new TabItem{Header=title,Content=Label("عملیات این تب پس از اتصال و بررسی مجوز سرور تکمیل می‌شود.")});
            OtherContent.Children.Add(tabs);return;
        }
        OtherContent.Children.Add(Label("چینش منو مطابق نسخه وب؛ داده‌ها و عملیات این بخش پس از اتصال امن تکمیل می‌شود.",13));
    }
    private void PreviewInvoiceSmokeTest()
    {
        Navigate("فاکتورهای الماس");
        if(InvoiceWorkspace.Visibility!=Visibility.Visible||previewMetrics?.Items.Count!=5||previewGrid?.Columns.Count!=9)throw new InvalidOperationException("invoice workspace");
        if(previewGrid.Items.Count!=20)throw new InvalidOperationException("invoice page size");
        previewPage=2;RefreshPreviewList();if(previewGrid.Items.Count!=1)throw new InvalidOperationException("invoice second page");
        filterSearch!.Text="۱۱۵۸";previewPage=1;RefreshPreviewList();if(previewGrid.Items.Count!=1)throw new InvalidOperationException("invoice search");
        ClearPreviewFilters();filterType!.SelectedItem="آنلاین";RefreshPreviewList();if(previewGrid.Items.Cast<Invoice>().Any(i=>i.SaleType!="آنلاین"))throw new InvalidOperationException("sale type filter");
        ClearPreviewFilters();filterStatus!.SelectedItem="پرداخت‌نشده";RefreshPreviewList();if(previewGrid.Items.Cast<Invoice>().Any(i=>i.Paid!=0||i.Cancelled))throw new InvalidOperationException("status filter");
        var i=invoices.FirstOrDefault(x=>x.CanEdit)??throw new InvalidOperationException("missing editable sample");
        RecordPreviewPayment(i,1000,"نقدی");var paid=invoices.First(x=>x.Number==i.Number);if(paid.Paid!=1000||paid.CanEdit||paid.Payments.Count!=i.Payments.Count+1)throw new InvalidOperationException("payment edit gate");
        ReplacePreview(paid,i);
        if(!ValidLine(2,1000,20)||ValidLine(0,1000,20)||AutoAdjustment(10500)!=500||new PreviewLine("x","1","c","s",2,1000,20).Net!=1600)throw new InvalidOperationException("line calculation");
        ClearPreviewFilters();
        foreach(var n in new[]{"ثبت سفارش","مدیریت صندوق","دریافت و پرداخت","مرجوعی کالا","خروج و ارسال کالا"})Navigate(n);
        Navigate("داشبورد");
    }
}
