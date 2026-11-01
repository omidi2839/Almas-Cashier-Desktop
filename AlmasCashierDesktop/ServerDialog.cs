using System;
using System.Drawing;
using System.Windows.Forms;

namespace AlmasCashierDesktop;

internal sealed class ServerDialog : Form
{
    private readonly TextBox _address;
    public string ServerUrl => _address.Text.Trim().TrimEnd('/');

    public ServerDialog(string current)
    {
        Text = "تنظیم اتصال مرکز فروش الماس";
        Width = 560; Height = 270; MinimumSize = MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(248, 250, 253);
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Controls.Add(new Label { Text = "آدرس سامانه (بدون مسیر صفحه):", Left = 24, Top = 24, Width = 500, Height = 28 });
        _address = new TextBox { Left = 24, Top = 57, Width = 500, Text = current, RightToLeft = RightToLeft.No, TextAlign = HorizontalAlignment.Left };
        Controls.Add(_address);
        Controls.Add(new Label { Text = "برای ورود با رمز عبور، HTTPS ضروری است. در HTTP اطلاعات ورود رمزگذاری نمی‌شوند.", Left = 24, Top = 106, Width = 500, Height = 54, ForeColor = Color.FromArgb(150, 55, 34) });
        var cancel = new Button { Text = "انصراف", DialogResult = DialogResult.Cancel, Left = 24, Top = 170, Width = 105, Height = 35 };
        var save = new Button { Text = "ذخیره", Left = 140, Top = 170, Width = 105, Height = 35, BackColor = Color.FromArgb(224, 110, 16), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        save.Click += (_, _) =>
        {
            if (!CashierConfig.IsValidServer(ServerUrl))
            {
                MessageBox.Show(this, "آدرس را به شکل https://sejadalmas.iq وارد کنید.", "آدرس نامعتبر", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (ServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                MessageBox.Show(this, "اتصال HTTP ناامن است و رمز ورود را در مسیر انتقال محافظت نمی‌کند. آیا با وجود این خطر ادامه می‌دهید؟", "هشدار امنیتی", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            DialogResult = DialogResult.OK;
        };
        Controls.Add(cancel); Controls.Add(save);
        AcceptButton = save; CancelButton = cancel;
    }
}
