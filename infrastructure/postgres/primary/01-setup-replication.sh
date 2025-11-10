#!/bin/bash
set -e

# Create replication user
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- Create replication user
    CREATE USER replicator WITH REPLICATION ENCRYPTED PASSWORD 'replicator_password';
    
    -- Create replication slot
    SELECT * FROM pg_create_physical_replication_slot('replication_slot');
    
    -- Grant necessary permissions
    GRANT CONNECT ON DATABASE global TO replicator;
EOSQL

# Configure pg_hba.conf for replication
cat >> "$PGDATA/pg_hba.conf" <<EOF

# Replication connections
host    replication     replicator      0.0.0.0/0               md5
host    replication     replicator      ::/0                    md5

# Allow connections from replica
host    all             all             postgres-replica        md5
EOF

# Reload PostgreSQL configuration
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    SELECT pg_reload_conf();
EOSQL

echo "PostgreSQL Primary configured for replication"
