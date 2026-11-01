<?php
defined('BASEPATH') or exit('No direct script access allowed');

class Almas_cashier_portal extends AdminController
{
    public function __construct()
    {
        parent::__construct();
    }

    public function status()
    {
        header('Content-Type: application/json; charset=utf-8');
        header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
        if (!is_staff_logged_in()) {
            http_response_code(401);
            echo json_encode(['authenticated' => false, 'allowed' => false]);
            return;
        }
        $staffId = (int)get_staff_user_id();
        $configured = (string)get_option('almas_cashier_desktop_staff_ids');
        $ids = $this->parse_ids($configured);
        $allowed = in_array($staffId, $ids, true);
        if ($allowed) {
            $staff = $this->db->select('active')->where('staffid', $staffId)->get(db_prefix() . 'staff')->row();
            $allowed = $staff && (int)$staff->active === 1;
        }
        http_response_code($allowed ? 200 : 403);
        echo json_encode(['authenticated' => true, 'allowed' => (bool)$allowed], JSON_UNESCAPED_UNICODE);
    }

    public function settings()
    {
        if (!is_admin()) access_denied('Almas Cashier Desktop settings');
        $saved = false;
        $error = '';
        if (strtoupper((string)$this->input->method()) === 'POST') {
            $raw = (string)$this->input->post('staff_ids', false);
            if (strlen($raw) > 2500 || !preg_match('/^[0-9,;\s]*$/', $raw)) {
                $error = 'شناسه‌ها را فقط با عدد و جداکننده ویرگول وارد کنید.';
            } else {
                $ids = $this->parse_ids($raw);
                if (count($ids) > 100) $error = 'حداکثر ۱۰۰ شناسه مجاز است.';
                else {
                    $activeCount = 0;
                    if ($ids) $activeCount = (int)$this->db->where_in('staffid', $ids)->where('active', 1)->count_all_results(db_prefix() . 'staff');
                    if ($activeCount !== count($ids)) $error = 'حداقل یک شناسه در جدول کارکنان فعال نیست.';
                    else { update_option('almas_cashier_desktop_staff_ids', implode(',', $ids)); $saved = true; }
                }
            }
        }
        $data = [
            'title' => 'کاربران مجاز صندوق ویندوز الماس',
            'staff_ids' => (string)get_option('almas_cashier_desktop_staff_ids'),
            'saved' => $saved,
            'error' => $error,
        ];
        $this->load->view('almas_cashier_portal/settings', $data);
    }

    private function parse_ids($value)
    {
        $parts = preg_split('/[,;\s]+/', trim((string)$value), -1, PREG_SPLIT_NO_EMPTY);
        $out = [];
        foreach ($parts ?: [] as $part) {
            if (ctype_digit((string)$part) && (int)$part > 0) $out[] = (int)$part;
        }
        return array_values(array_unique($out));
    }
}
