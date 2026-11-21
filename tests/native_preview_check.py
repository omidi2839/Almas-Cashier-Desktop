from pathlib import Path
import xml.etree.ElementTree as ET
r=Path(__file__).resolve().parents[1]
x=r/'AlmasCashierDesktop/WpfDashboard.xaml'
ET.parse(x)
s=(r/'AlmasCashierDesktop/WpfDashboard.xaml.cs').read_text(encoding='utf-8')
assert 'UseWPF' in (r/'AlmasCashierDesktop/AlmasCashierDesktop.csproj').read_text()
assert 'new WpfDashboard()' in (r/'AlmasCashierDesktop/Program.cs').read_text()
assert 'DEMO-' in s and 'AllowAutoRedirect=false' in s and 'PostAsync' not in s
assert 'وظایف امروز' not in x.read_text(encoding='utf-8') and 'آخرین فعالیت‌ها' not in x.read_text(encoding='utf-8')
assert '0.3.0' in (r/'installer/AlmasCashier.iss').read_text()
native=(r/'AlmasCashierDesktop/WpfDashboard.NativeSales.cs').read_text(encoding='utf-8')
live=(r/'AlmasCashierDesktop/WpfDashboard.Live.cs').read_text(encoding='utf-8')
assert 'OpenNativeInvoiceListAsync()' in live and 'OpenNativeInvoiceListAsync()' in native
assert 'OpenNativeInvoiceDetailAsync' in native
assert 'OpenNativeInvoiceCreate' in native
assert 'WebView2' not in native and 'WebView2' not in live
assert 'invoices?page=' in native and 'invoice?id=' in native
assert 'ثبت نهایی فاکتور' in native and 'IsEnabled = false' in native
print('PASS: native invoice workspace, details, draft and safe API gating')
print('PASS: WPF, simplified layout, secure preview, version')
