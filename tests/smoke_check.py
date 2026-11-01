"""Structural smoke checks independent of unavailable .NET/Windows runtime."""
from pathlib import Path
root = Path(__file__).resolve().parents[1]
cs = (root/'AlmasCashierDesktop/CashierWindow.cs').read_text()
portal = (root/'backend/almas_cashier_portal/controllers/Almas_cashier_portal.php').read_text()
assert cs.count('(\"فاکتورها\",') == 1
for expected in ('/admin/almas_invoice_core','sales_commercial_rules/sales_requests','sales_commercial_rules/cashier','sales_delivery_flow/returns'):
    assert expected.lstrip('/') in cs
assert 'aic-kpis' in cs and 'bulk_import' in cs and 'bulk_payments' in cs
assert 'VerifyCashierAsync' in cs and 'status' in cs
assert 'is_staff_logged_in' in portal and 'get_staff_user_id' in portal
assert 'almas_cashier_desktop_staff_ids' in portal
assert 'Password' not in (root/'AlmasCashierDesktop/CashierConfig.cs').read_text()
assert len(list((root/'backend/almas_cashier_portal').rglob('*.php'))) == 3
print('PASS: routes, desktop-only invoice CSS, server allowlist, files')
