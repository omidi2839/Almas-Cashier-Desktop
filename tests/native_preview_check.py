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
assert '0.2.5' in (r/'installer/AlmasCashier.iss').read_text()
ops=(r/'AlmasCashierDesktop/WpfDashboard.Operations.cs').read_text(encoding='utf-8')
assert 'OperationsView' in x.read_text(encoding='utf-8')
assert 'CoreWebView2Environment' in ops and 'Uri.UriSchemeHttps' in ops
assert 'Authorization=' not in ops and 'Bearer ' not in ops.replace('Device bearer tokens', '')
assert 'admin/almas_invoice_core' in ops and 'admin/almas_warehouse/stock' in ops
assert 'admin/sales_accounting' in ops
print('PASS: WPF, simplified layout, secure preview, version')
