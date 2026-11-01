<?php defined('BASEPATH') or exit('No direct script access allowed'); init_head(); ?>
<div id="wrapper"><div class="content"><div class="row"><div class="col-md-8 col-md-offset-2">
<div class="panel_s"><div class="panel-body" dir="rtl">
<h3>کاربران مجاز اپلیکیشن صندوق ویندوز</h3>
<p>فقط شناسه کارکنانی را ثبت کنید که اجازه ورود به نسخه ویندوز صندوق را دارند. این فهرست مجوزهای مالی Perfex را افزایش نمی‌دهد؛ سطح دسترسی فاکتور، صندوق و انبار باید جداگانه تنظیم شود.</p>
<?php if ($saved): ?><div class="alert alert-success">شناسه‌ها ذخیره شدند.</div><?php endif; ?>
<?php if ($error): ?><div class="alert alert-danger"><?=html_escape($error)?></div><?php endif; ?>
<?=form_open(admin_url('almas_cashier_portal/settings'))?>
<label for="cashier_ids">شناسه کارکنان (staffid)، با ویرگول جدا شوند</label>
<input name="staff_ids" id="cashier_ids" class="form-control" dir="ltr" value="<?=html_escape($staff_ids)?>" placeholder="38,39" maxlength="2500">
<p class="text-muted" style="margin-top:12px">برای جلوگیری از ورود ناخواسته، فهرست اولیه خالی است. مدیر نیز تا زمانی که شناسه‌اش در فهرست نباشد از اپلیکیشن وارد نمی‌شود.</p>
<button type="submit" class="btn btn-primary">ذخیره کاربران مجاز</button>
<?=form_close()?>
</div></div></div></div></div></div>
<?php init_tail(); ?>
