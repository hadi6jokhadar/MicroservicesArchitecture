#!/bin/bash
set -e

# Create replication user
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- Create replication user
    CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD '$POSTGRES_REPLICATION_PASSWORD';

    -- Create replication slot
    SELECT * FROM pg_create_physical_replication_slot('replication_slot');

    -- Grant necessary permissions
    GRANT CONNECT ON DATABASE global TO replicator;
EOSQL

# Configure pg_hba.conf for replication
# Scoped to the "postgres-replica" service name (not 0.0.0.0/0) — Docker's embedded DNS
# resolves it to the replica container's address on the postgres-replication bridge
# network, and pg_hba.conf's hostname form is already the established pattern here (see
# the "Allow connections from replica" rule below, which predates this fix).
cat >> "$PGDATA/pg_hba.conf" <<EOF

# Replication connections — restricted to the replica container on this compose network
host    replication     replicator      postgres-replica        md5

# Allow connections from replica
host    all             all             postgres-replica        md5
EOF

# Reload PostgreSQL configuration
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    SELECT pg_reload_conf();
EOSQL

echo "PostgreSQL Primary configured for replication"
