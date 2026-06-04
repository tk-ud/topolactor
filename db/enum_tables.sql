-- =============================================================================
-- enum_tables.sql
-- Canonical enum item and enum group dictionary store.
-- SSOT: docs/design/enum-dictionary-ssot.yaml
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS enum;

CREATE TABLE IF NOT EXISTS enum.items (
    enum_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    index_num  INTEGER     NOT NULL,
    name       TEXT        NOT NULL,
    CONSTRAINT uq_enum_items_index UNIQUE (index_num)
);

CREATE TABLE IF NOT EXISTS enum.groups (
    group_id    UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    index_num   INTEGER     NOT NULL,
    group_name  TEXT        NOT NULL,
    CONSTRAINT uq_enum_groups_index UNIQUE (index_num)
);

CREATE TABLE IF NOT EXISTS enum.group_items (
    group_id        UUID    NOT NULL REFERENCES enum.groups(group_id) ON DELETE CASCADE,
    position        INTEGER NOT NULL,
    enum_index_num  INTEGER NOT NULL REFERENCES enum.items(index_num),
    PRIMARY KEY (group_id, position),
    CONSTRAINT uq_enum_group_items_member UNIQUE (group_id, enum_index_num)
);

CREATE INDEX IF NOT EXISTS idx_enum_group_items_group
    ON enum.group_items (group_id, position);
