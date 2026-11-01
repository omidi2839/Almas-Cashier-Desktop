<?php
/*
Module Name: Almas Cashier Desktop Access
Description: Explicit cashier allowlist for Almas Cashier Windows desktop session checks.
Version: 0.1.0
Requires at least: 3.0.*
*/
defined('BASEPATH') or exit('No direct script access allowed');

define('ALMAS_CASHIER_PORTAL', 'almas_cashier_portal');
register_activation_hook(ALMAS_CASHIER_PORTAL, function () {
    if (function_exists('add_option')) add_option('almas_cashier_desktop_staff_ids', '');
});
