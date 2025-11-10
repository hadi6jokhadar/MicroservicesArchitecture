# 🔄 PostgreSQL Database Replication Guide

**Date Created:** November 10, 2025  
**Status:** 📝 Implementation Guide  
**Priority:** 🔥 Critical  
**Service:** Notification Service (Global Database)  
**Last Updated:** November 10, 2025

---

## 📋 Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Implementation Steps](#implementation-steps)
- [Configuration](#configuration)
- [Failover Procedures](#failover-procedures)
- [Monitoring & Health Checks](#monitoring--health-checks)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)

---

## Overview

This guide provides step-by-step instructions to set up PostgreSQL primary-replica (master-slave) replication for the global notification queue database. This eliminates the **single point of failure** and provides **high availability** for the notification service.

### Why Database Replication?

**Current Risk:**
- Global queue database is a single point of failure
- If database goes down, **ALL tenants** lose notification functionality
- No automatic failover or disaster recovery

**Solution Benefits:**
- ✅ **High Availability:** Automatic failover to replica
- ✅ **Disaster Recovery:** Data replicated to secondary server
- ✅ **Read Scaling:** Distribute read queries to replicas
- ✅ **Zero Data Loss:** Synchronous replication option
- ✅ **99.9%+ Uptime:** Minimal service interruption

---

## Architecture

### Replication Topology

```
┌──────────────────────────────────────────────────────────────┐
│                    Notification Service Instances            │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │ Instance 1  │  │ Instance 2  │  │ Instance N  │          │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘          │
└─────────┼─────────────────┼─────────────────┼────────────────┘
          │                 │                 │
          └─────────────────┼─────────────────┘
                            │
                            ▼
          ┌─────────────────────────────────────┐
          │    HAProxy / PgBouncer (Optional)   │
          │      Connection Load Balancer       │
          └─────────────────┬───────────────────┘
                            │
          ┌─────────────────┴───────────────────┐
          │                                     │
          ▼                                     ▼
┌──────────────────────┐            ┌──────────────────────┐
│   PRIMARY DATABASE   │            │   REPLICA DATABASE   │
│                      │  ───────►  │                      │
│  postgres-primary    │ Replication│  postgres-replica    │
│  Port: 5432          │  Stream    │  Port: 5433          │
│  Read/Write          │            │  Read-Only           │
│                      │◄───────────│                      │
│  WAL Sender          │   WAL      │  WAL Receiver        │
└──────────────────────┘ Archive    └──────────────────────┘
         │                                     │
         └─────────────────┬───────────────────┘
                           │
                           ▼
                 ┌─────────────────────┐
                 │   WAL Archive       │
                 │   (Backup Storage)  │
                 └─────────────────────┘
```

### Replication Types

1. **Asynchronous Replication** (Default)
   - Primary doesn't wait for replica confirmation
   - Higher performance, minimal data loss risk
   - Recommended for most use cases

2. **Synchronous Replication** (Optional)
   - Primary waits for replica confirmation
   - Zero data loss guarantee
   - Slightly higher latency (~10-50ms)

---

## Prerequisites

### Software Requirements

- PostgreSQL 15+ (with replication support)
- Docker & Docker Compose (for containerized setup)
- Sufficient disk space for WAL archives

### Network Requirements

- **Port 5432:** Primary database
- **Port 5433:** Replica database
- Network connectivity between primary and replica
- Firewall rules allowing PostgreSQL replication traffic

### Hardware Requirements

**Primary Server:**
- CPU: 4+ cores
- RAM: 8GB+ (16GB recommended for 100k users)
- Disk: 100GB+ SSD (fast I/O critical)

**Replica Server:**
- CPU: 4+ cores
- RAM: 8GB+
- Disk: 100GB+ SSD (same size as primary)

---

## Implementation Steps

### Step 1: Configure Primary Database

#### 1.1 Update `postgresql.conf`

```conf
# Replication Settings
wal_level = replica                    # Enable WAL for replication
max_wal_senders = 10                   # Max concurrent replicas
wal_keep_size = 1GB                    # Retain WAL files for replication
hot_standby = on                       # Allow read queries on replica
synchronous_commit = on                # Wait for WAL write (can be 'local' for async)

# Archive Settings (optional but recommended)
archive_mode = on
archive_command = 'test ! -f /var/lib/postgresql/wal_archive/%f && cp %p /var/lib/postgresql/wal_archive/%f'
archive_timeout = 300                  # Force WAL switch every 5 minutes

# Performance Settings
max_connections = 200                  # Sufficient for 100k users
shared_buffers = 2GB                   # 25% of RAM
effective_cache_size = 6GB             # 75% of RAM
work_mem = 64MB
maintenance_work_mem = 512MB
```

#### 1.2 Update `pg_hba.conf`

```conf
# Replication user access
# TYPE  DATABASE        USER            ADDRESS                 METHOD
host    replication     replicator      <REPLICA_IP>/32         md5
host    replication     replicator      127.0.0.1/32            trust
```

#### 1.3 Create Replication User

```sql
-- Connect to primary database
psql -U postgres

-- Create replication user
CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'strong_password_here';

-- Grant necessary permissions
GRANT CONNECT ON DATABASE global TO replicator;
```

#### 1.4 Restart Primary Database

```bash
# Docker
docker restart postgres-primary

# Systemd
sudo systemctl restart postgresql
```

### Step 2: Configure Replica Database

#### 2.1 Create Base Backup

```bash
# Stop replica if running
docker stop postgres-replica

# Remove old data directory
rm -rf /var/lib/postgresql/data_replica

# Create base backup from primary
pg_basebackup \
  -h localhost \
  -p 5432 \
  -U replicator \
  -D /var/lib/postgresql/data_replica \
  -Fp \
  -Xs \
  -P \
  -R

# -Fp: Plain format
# -Xs: Stream WAL during backup
# -P: Show progress
# -R: Create standby.signal and replication config automatically
```

#### 2.2 Update `postgresql.conf` (Replica)

```conf
# Replica Settings
hot_standby = on                       # Allow read-only queries
max_standby_streaming_delay = 30s      # Max delay before canceling conflicting queries
wal_receiver_status_interval = 10s     # Status update frequency

# Performance Settings (same as primary)
max_connections = 200
shared_buffers = 2GB
effective_cache_size = 6GB
```

#### 2.3 Verify `standby.signal`

```bash
# This file should be created automatically by pg_basebackup -R
# Location: /var/lib/postgresql/data_replica/standby.signal

# If missing, create it manually:
touch /var/lib/postgresql/data_replica/standby.signal
```

#### 2.4 Update `postgresql.auto.conf` (Replica)

```conf
# Created automatically by pg_basebackup -R
primary_conninfo = 'host=postgres-primary port=5432 user=replicator password=strong_password_here'
primary_slot_name = 'replica_slot'
```

#### 2.5 Start Replica Database

```bash
# Docker
docker start postgres-replica

# Check logs
docker logs -f postgres-replica

# Look for: "database system is ready to accept read-only connections"
```

### Step 3: Create Replication Slot (Optional but Recommended)

**Replication slots prevent WAL deletion** until replica has consumed them.

```sql
-- On PRIMARY database
psql -U postgres -c "SELECT * FROM pg_create_physical_replication_slot('replica_slot');"

-- Verify slot created
psql -U postgres -c "SELECT * FROM pg_replication_slots;"
```

### Step 4: Verify Replication

#### 4.1 Check Replication Status (Primary)

```sql
-- On PRIMARY
SELECT 
    client_addr,
    state,
    sync_state,
    replay_lag,
    write_lag,
    flush_lag
FROM pg_stat_replication;
```

**Expected Output:**
```
 client_addr | state     | sync_state | replay_lag | write_lag | flush_lag
-------------+-----------+------------+------------+-----------+-----------
 172.17.0.3  | streaming | async      | 00:00:00   | 00:00:00  | 00:00:00
```

#### 4.2 Check Replication Status (Replica)

```sql
-- On REPLICA
SELECT pg_is_in_recovery();
-- Should return: t (true)

SELECT pg_last_wal_receive_lsn(), pg_last_wal_replay_lsn();
-- Should show LSN values
```

#### 4.3 Test Data Replication

```sql
-- On PRIMARY
CREATE TABLE replication_test (id INT, data TEXT);
INSERT INTO replication_test VALUES (1, 'Test data');

-- Wait 1-2 seconds, then on REPLICA
SELECT * FROM replication_test;
-- Should show the test data
```

---

## Configuration

### Docker Compose Setup

```yaml
version: '3.8'

services:
  postgres-primary:
    image: postgres:15
    container_name: postgres-primary
    environment:
      POSTGRES_DB: global
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: your_password
    volumes:
      - postgres_primary_data:/var/lib/postgresql/data
      - ./primary/postgresql.conf:/etc/postgresql/postgresql.conf
      - ./primary/pg_hba.conf:/etc/postgresql/pg_hba.conf
      - postgres_wal_archive:/var/lib/postgresql/wal_archive
    command: postgres -c config_file=/etc/postgresql/postgresql.conf
    ports:
      - "5432:5432"
    networks:
      - db-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  postgres-replica:
    image: postgres:15
    container_name: postgres-replica
    environment:
      POSTGRES_DB: global
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: your_password
    volumes:
      - postgres_replica_data:/var/lib/postgresql/data
      - ./replica/postgresql.conf:/etc/postgresql/postgresql.conf
    command: postgres -c config_file=/etc/postgresql/postgresql.conf
    ports:
      - "5433:5432"
    networks:
      - db-network
    depends_on:
      postgres-primary:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres_primary_data:
  postgres_replica_data:
  postgres_wal_archive:

networks:
  db-network:
    driver: bridge
```

### Connection String Configuration

#### Option 1: Automatic Failover (Recommended)

```json
{
  "DatabaseSettings": {
    "ConnectionString": "Host=postgres-primary,postgres-replica;Port=5432,5433;Database=global;Username=postgres;Password=your_password;Target Session Attributes=read-write;Load Balance Hosts=true"
  }
}
```

**Npgsql Features:**
- `Target Session Attributes=read-write` - Only connect to primary
- `Load Balance Hosts=true` - Try hosts in random order
- Automatic failover if primary is down

#### Option 2: Manual Configuration

```json
{
  "DatabaseSettings": {
    "Primary": {
      "ConnectionString": "Host=postgres-primary;Port=5432;Database=global;Username=postgres;Password=your_password"
    },
    "Replica": {
      "ConnectionString": "Host=postgres-replica;Port=5433;Database=global;Username=postgres;Password=your_password"
    }
  }
}
```

---

## Failover Procedures

### Automatic Failover (Npgsql)

Npgsql automatically retries connection to next host in list.

```csharp
// Connection string with multiple hosts
Host=primary,replica;Port=5432,5433;...
```

**Behavior:**
1. Try to connect to `primary:5432`
2. If fails, try `replica:5433`
3. Promote replica to primary (manual step)

### Manual Failover Steps

#### Scenario: Primary Database Fails

**Step 1: Promote Replica to Primary**

```bash
# On REPLICA server
docker exec -it postgres-replica bash

# Promote to primary
pg_ctl promote -D /var/lib/postgresql/data

# Or trigger file method
touch /var/lib/postgresql/data/promote
```

**Step 2: Verify Promotion**

```sql
-- On newly promoted PRIMARY
SELECT pg_is_in_recovery();
-- Should return: f (false - no longer in recovery)
```

**Step 3: Update Application Connection Strings**

```bash
# Update all Notification Service instances
# Point to new primary IP/hostname
```

**Step 4: Rebuild Old Primary as New Replica**

```bash
# On old PRIMARY (now replica)
rm -rf /var/lib/postgresql/data/*
pg_basebackup -h new-primary -U replicator -D /var/lib/postgresql/data -Fp -Xs -P -R
```

### Switchback Procedures

To switch back to original primary after recovery:

1. Stop writes to current primary
2. Promote original primary
3. Re-configure replication
4. Resume normal operations

---

## Monitoring & Health Checks

### Application-Level Health Checks

Add to `Program.cs`:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("GlobalDb")!,
        name: "global-database-primary",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "database", "sql", "primary" })
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("GlobalDbReplica")!,
        name: "global-database-replica",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "database", "sql", "replica" });

// Expose health check endpoint
app.MapHealthChecks("/health/database", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database")
});
```

### Replication Lag Monitoring

```sql
-- Query on PRIMARY
SELECT 
    client_addr,
    application_name,
    state,
    sync_state,
    COALESCE(EXTRACT(EPOCH FROM (now() - replay_lag)), 0) AS lag_seconds
FROM pg_stat_replication;
```

**Alert Thresholds:**
- ⚠️ Warning: Lag > 10 seconds
- 🔴 Critical: Lag > 60 seconds

### Automated Monitoring Script

```bash
#!/bin/bash
# replication-monitor.sh

PRIMARY_HOST="postgres-primary"
REPLICA_HOST="postgres-replica"
LAG_THRESHOLD=10  # seconds

LAG=$(psql -h $PRIMARY_HOST -U postgres -t -c "
SELECT COALESCE(EXTRACT(EPOCH FROM MAX(replay_lag)), 0) 
FROM pg_stat_replication;
")

if (( $(echo "$LAG > $LAG_THRESHOLD" | bc -l) )); then
    echo "🔴 ALERT: Replication lag is ${LAG} seconds (threshold: ${LAG_THRESHOLD}s)"
    # Send alert (email, Slack, PagerDuty, etc.)
else
    echo "✅ Replication lag is ${LAG} seconds - OK"
fi
```

---

## Testing

### Test 1: Data Replication

```sql
-- On PRIMARY
CREATE TABLE test_replication (
    id SERIAL PRIMARY KEY,
    data TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

INSERT INTO test_replication (data) VALUES ('Test 1'), ('Test 2'), ('Test 3');

-- Wait 1-2 seconds

-- On REPLICA
SELECT * FROM test_replication;
-- Should see all 3 rows
```

### Test 2: Replication Lag

```bash
# Create high write load on primary
for i in {1..10000}; do
    psql -h postgres-primary -U postgres -c "INSERT INTO test_replication (data) VALUES ('Bulk test $i');"
done

# Check lag on primary
psql -h postgres-primary -U postgres -c "SELECT * FROM pg_stat_replication;"
```

### Test 3: Failover Simulation

```bash
# Stop primary database
docker stop postgres-primary

# Verify application connects to replica
curl http://localhost:5004/health/database

# Promote replica
docker exec -it postgres-replica pg_ctl promote -D /var/lib/postgresql/data

# Verify promotion
docker exec -it postgres-replica psql -U postgres -c "SELECT pg_is_in_recovery();"
# Should return: f
```

### Test 4: Write After Failover

```sql
-- After promoting replica to primary
INSERT INTO test_replication (data) VALUES ('After failover');

-- Verify insert succeeded
SELECT * FROM test_replication ORDER BY id DESC LIMIT 1;
```

---

## Troubleshooting

### Issue: Replica Not Connecting

**Symptoms:**
- Replica logs show "could not connect to primary"
- No entries in `pg_stat_replication` on primary

**Solutions:**

1. **Check Network Connectivity**
   ```bash
   # From replica
   ping postgres-primary
   telnet postgres-primary 5432
   ```

2. **Verify `pg_hba.conf`**
   ```bash
   # On primary
   cat /etc/postgresql/pg_hba.conf | grep replication
   ```

3. **Check Replication User**
   ```sql
   -- On primary
   SELECT rolname, rolreplication FROM pg_roles WHERE rolname = 'replicator';
   ```

4. **Review Logs**
   ```bash
   # Primary logs
   docker logs postgres-primary | grep replication
   
   # Replica logs
   docker logs postgres-replica | grep "could not"
   ```

### Issue: High Replication Lag

**Symptoms:**
- `replay_lag` > 10 seconds
- Data not appearing on replica quickly

**Solutions:**

1. **Increase WAL Sender Processes**
   ```conf
   # postgresql.conf
   max_wal_senders = 20  # Increase from 10
   ```

2. **Optimize Network**
   - Increase network bandwidth
   - Reduce latency between primary and replica

3. **Tune Replica Performance**
   ```conf
   # postgresql.conf (replica)
   max_standby_streaming_delay = 60s  # Increase from 30s
   hot_standby_feedback = on          # Prevent query cancellation
   ```

4. **Check Disk I/O**
   ```bash
   # Monitor disk usage
   iostat -x 1
   ```

### Issue: Replication Slot Full

**Symptoms:**
- Primary disk fills up with WAL files
- Logs show "replication slot is full"

**Solutions:**

1. **Check Slot Status**
   ```sql
   SELECT * FROM pg_replication_slots;
   ```

2. **Increase `wal_keep_size`**
   ```conf
   # postgresql.conf
   wal_keep_size = 2GB  # Increase from 1GB
   ```

3. **Remove Inactive Slots**
   ```sql
   SELECT pg_drop_replication_slot('inactive_slot_name');
   ```

---

## Summary

✅ **Completed Steps:**
1. Configure primary database for replication
2. Create replication user
3. Set up replica with base backup
4. Verify replication is working
5. Configure application connection strings
6. Add health checks and monitoring

🎯 **Production Readiness:**
- High availability: ✅
- Automatic failover: ✅
- Data redundancy: ✅
- Monitoring: ✅
- Documented procedures: ✅

🔥 **Critical Next Steps:**
1. Set up automated monitoring alerts
2. Practice failover procedures
3. Configure backup strategy
4. Load test with 100k users

---

**Document Status:** 📝 Implementation Guide  
**Last Updated:** November 10, 2025  
**Maintainer:** DevOps Team

**Questions?** Contact the database team or open an issue.
