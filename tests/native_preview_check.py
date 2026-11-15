from pathlib import Path
r=Path(__file__).resolve().parents[1]
s=(r/'AlmasCashierDesktop/AlmasDashboard.cs').read_text(encoding='utf-8')
p=(r/'AlmasCashierDesktop/Program.cs').read_text(encoding='utf-8')
assert 'new AlmasDashboard()' in p
assert 'new CashierWindow()' not in p
assert 'https://desktop.sejadalmas.iq' in s and 'AllowAutoRedirect=false' in s
assert 'HealthClient.GetAsync' in s and 'PostAsync' not in s and 'DefaultRequestHeaders.Authorization' not in s
assert 'DEMO-' in s and 'بدون اثر مالی' in s
assert '0.2.1' in (r/'installer/AlmasCashier.iss').read_text(encoding='utf-8')
print('PASS: native launch, preview-only financial data, secure health-only gateway, version')

assert 'ChooseFont()' in s and 'Tahoma' in s
assert 'SoftGrid' in s and 'SoftButton' in s and 'MetricIcon' in s
assert 'FaDigits' in s
