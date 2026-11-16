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
assert '0.2.4' in (r/'installer/AlmasCashier.iss').read_text()
print('PASS: WPF, simplified layout, secure preview, version')
