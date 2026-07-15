CREATE TABLE IF NOT EXISTS shelves (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    warehouse TEXT NOT NULL,
    aisle TEXT NOT NULL,
    shelf_code TEXT NOT NULL,
    position TEXT NOT NULL,
    device_id TEXT NULL,
    camera_device TEXT NULL,
    enabled INTEGER NOT NULL,
    version INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS products (
    id TEXT PRIMARY KEY,
    sku TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    expiration_date TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS devices (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    serial_number TEXT NOT NULL UNIQUE,
    kind TEXT NOT NULL,
    status TEXT NOT NULL,
    last_seen TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS evaluation_rules (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    metric TEXT NOT NULL,
    operator TEXT NOT NULL,
    threshold REAL NOT NULL,
    result_status TEXT NOT NULL,
    led_color TEXT NOT NULL,
    priority INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NULL
);

CREATE TABLE IF NOT EXISTS shelf_resource_bindings (
    id TEXT PRIMARY KEY,
    shelf_id TEXT NOT NULL,
    kind TEXT NOT NULL,
    resource_id TEXT NOT NULL,
    FOREIGN KEY (shelf_id) REFERENCES shelves(id) ON DELETE CASCADE,
    UNIQUE (shelf_id, kind, resource_id)
);

CREATE TABLE IF NOT EXISTS shelf_observations (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    shelf_id TEXT NOT NULL,
    captured_at TEXT NOT NULL,
    inventory_percent INTEGER NOT NULL,
    days_until_expiration INTEGER NOT NULL,
    expired_product_detected INTEGER NOT NULL,
    sensor_online INTEGER NOT NULL,
    status TEXT NOT NULL,
    led_color TEXT NOT NULL,
    confidence REAL NOT NULL,
    reason TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS alerts (
    id TEXT PRIMARY KEY,
    shelf_id TEXT NOT NULL,
    severity TEXT NOT NULL,
    status TEXT NOT NULL,
    message TEXT NOT NULL,
    occurrences INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    last_occurred_at TEXT NOT NULL,
    acknowledged_at TEXT NULL,
    resolved_at TEXT NULL
);

CREATE INDEX IF NOT EXISTS ix_shelves_location ON shelves (warehouse, aisle, shelf_code, position);
CREATE INDEX IF NOT EXISTS ix_shelf_bindings_shelf ON shelf_resource_bindings (shelf_id, kind);
CREATE UNIQUE INDEX IF NOT EXISTS ux_shelf_single_controller ON shelf_resource_bindings (shelf_id) WHERE kind = 'Controller';
CREATE UNIQUE INDEX IF NOT EXISTS ux_shelf_single_camera ON shelf_resource_bindings (shelf_id) WHERE kind = 'Camera';
CREATE UNIQUE INDEX IF NOT EXISTS ux_shelf_single_led ON shelf_resource_bindings (shelf_id) WHERE kind = 'LedOutput';
CREATE INDEX IF NOT EXISTS ix_shelf_observations_shelf_captured ON shelf_observations (shelf_id, captured_at DESC);
CREATE UNIQUE INDEX IF NOT EXISTS ux_alerts_open_shelf ON alerts (shelf_id) WHERE status <> 'Resolved';
CREATE INDEX IF NOT EXISTS ix_alerts_status_last_occurred ON alerts (status, last_occurred_at DESC);
