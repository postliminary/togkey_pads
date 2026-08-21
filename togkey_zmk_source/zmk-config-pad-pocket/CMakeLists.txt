if(CONFIG_PAD_POCKET_BLE_BROADCAST)
  zephyr_library()
  zephyr_library_sources(src/behavior_ble_broadcast.c)
endif()
