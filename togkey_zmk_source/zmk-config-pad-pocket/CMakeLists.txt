if(CONFIG_PAD_POCKET_BLE_BROADCAST)
  zephyr_library()
  # External Zephyr libraries do not inherit ZMK's application include path.
  zephyr_library_include_directories(${APPLICATION_SOURCE_DIR}/include)
  zephyr_library_sources(src/behavior_ble_broadcast.c)
endif()
