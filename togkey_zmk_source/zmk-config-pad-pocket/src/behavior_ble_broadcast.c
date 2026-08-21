/*
 * Pad Pocket two-host BLE broadcast proof of concept.
 *
 * SPDX-License-Identifier: MIT
 */

#define DT_DRV_COMPAT togkey_behavior_ble_broadcast

#include <errno.h>
#include <zephyr/bluetooth/conn.h>
#include <zephyr/bluetooth/gatt.h>
#include <zephyr/bluetooth/uuid.h>
#include <zephyr/device.h>
#include <zephyr/logging/log.h>
#include <zephyr/sys/util.h>

#include <drivers/behavior.h>
#include <dt-bindings/zmk/hid_usage.h>
#include <dt-bindings/zmk/modifiers.h>
#include <zmk/behavior.h>
#include <zmk/ble.h>
#include <zmk/hid.h>

LOG_MODULE_REGISTER(pad_pocket_ble_broadcast, CONFIG_ZMK_LOG_LEVEL);

#define PC_PROFILE_FIRST 0
#define PC_PROFILE_LAST 1
#define ACTION_USB_C 0
#define ACTION_DISPLAYPORT 1

static const struct bt_gatt_attr *keyboard_report_attr;

static uint8_t find_keyboard_report_attr(const struct bt_gatt_attr *attr, void *user_data) {
    ARG_UNUSED(user_data);

    if (bt_uuid_cmp(attr->uuid, BT_UUID_HIDS_REPORT) == 0) {
        keyboard_report_attr = attr;
        return BT_GATT_ITER_STOP;
    }

    return BT_GATT_ITER_CONTINUE;
}

static int ensure_keyboard_report_attr(void) {
    if (keyboard_report_attr != NULL) {
        return 0;
    }

    /* ZMK declares the keyboard input report before its other HID reports. */
    bt_gatt_foreach_attr(0x0001, 0xffff, find_keyboard_report_attr, NULL);
    return keyboard_report_attr == NULL ? -ENOENT : 0;
}

static int send_to_profile(uint8_t profile, const struct zmk_hid_keyboard_report_body *report) {
    bt_addr_le_t *address = zmk_ble_profile_address(profile);
    if (bt_addr_le_cmp(address, BT_ADDR_LE_ANY) == 0 ||
        bt_addr_le_cmp(address, BT_ADDR_LE_NONE) == 0) {
        LOG_WRN("BLE profile %u is not paired", profile);
        return -ENODEV;
    }

    struct bt_conn *conn = bt_conn_lookup_addr_le(BT_ID_DEFAULT, address);
    if (conn == NULL) {
        LOG_WRN("BLE profile %u is not connected", profile);
        return -ENOTCONN;
    }

    struct bt_gatt_notify_params params = {
        .attr = keyboard_report_attr,
        .data = report,
        .len = sizeof(*report),
    };
    int err = bt_gatt_notify_cb(conn, &params);
    if (err == -EPERM) {
        bt_conn_set_security(conn, BT_SECURITY_L2);
    }
    bt_conn_unref(conn);

    return err;
}

static int broadcast_report(uint8_t action, bool pressed) {
    int err = ensure_keyboard_report_attr();
    if (err) {
        LOG_ERR("Could not find the keyboard GATT report attribute (%d)", err);
        return err;
    }

    struct zmk_hid_keyboard_report_body report = {0};
    if (pressed) {
        report.modifiers = MOD_LCTRL | MOD_LALT;
        report.keys[0] = action == ACTION_USB_C ? HID_USAGE_KEY_KEYBOARD_F23
                                                : HID_USAGE_KEY_KEYBOARD_F24;
    }

    int result = -ENOTCONN;
    bool delivered = false;
    for (uint8_t profile = PC_PROFILE_FIRST; profile <= PC_PROFILE_LAST; profile++) {
        int profile_err = send_to_profile(profile, &report);
        if (profile_err == 0) {
            delivered = true;
        } else {
            result = profile_err;
        }
    }

    return delivered ? 0 : result;
}

static int on_pressed(struct zmk_behavior_binding *binding,
                      struct zmk_behavior_binding_event event) {
    ARG_UNUSED(event);

    if (binding->param1 > ACTION_DISPLAYPORT) {
        return -EINVAL;
    }

    return broadcast_report(binding->param1, true);
}

static int on_released(struct zmk_behavior_binding *binding,
                       struct zmk_behavior_binding_event event) {
    ARG_UNUSED(event);

    if (binding->param1 > ACTION_DISPLAYPORT) {
        return -EINVAL;
    }

    return broadcast_report(binding->param1, false);
}

static const struct behavior_driver_api behavior_ble_broadcast_driver_api = {
    .binding_pressed = on_pressed,
    .binding_released = on_released,
};

BEHAVIOR_DT_INST_DEFINE(0, NULL, NULL, NULL, NULL, POST_KERNEL,
                        CONFIG_KERNEL_INIT_PRIORITY_DEFAULT,
                        &behavior_ble_broadcast_driver_api);
