#!/bin/bash
set -e

# Docker/Render: default FileSystemWatcher exhausts inotify (128) during deploy preboot → exit 139
export DOTNET_USE_POLLING_FILE_WATCHER=1

echo "========================================="
echo "FrozenApi Docker Entrypoint"
echo "========================================="

# Detect PostgreSQL from Render/common env vars (not only USE_POSTGRES)
USE_PG=false
if [ "$USE_POSTGRES" = "true" ] || [ -n "$DATABASE_URL" ] || [ -n "$ConnectionStrings__DefaultConnection" ] || [ -n "$ConnectionStrings__PostgreSQL" ]; then
    USE_PG=true
fi

# Wait for PostgreSQL to be ready if using PostgreSQL
if [ "$USE_PG" = "true" ]; then
    echo "Using PostgreSQL database"
    echo "Waiting for PostgreSQL to be ready..."
    
    # Extract host from connection string
    PG_CONN="${ConnectionStrings__DefaultConnection:-$ConnectionStrings__PostgreSQL}"
    if [ -n "$PG_CONN" ]; then
        POSTGRES_HOST=$(echo "$PG_CONN" | grep -oP 'Host=\K[^;]+' || echo "postgres")
        POSTGRES_PORT=$(echo "$PG_CONN" | grep -oP 'Port=\K[^;]+' || echo "5432")
    elif [ -n "$DATABASE_URL" ]; then
        POSTGRES_HOST=$(echo "$DATABASE_URL" | sed -E 's|.*@([^:/]+).*|\1|')
        POSTGRES_PORT=$(echo "$DATABASE_URL" | sed -E 's|.*:([0-9]+)/.*|\1|')
        if [ -z "$POSTGRES_PORT" ] || [ "$POSTGRES_PORT" = "$DATABASE_URL" ]; then
            POSTGRES_PORT=5432
        fi
    else
        POSTGRES_HOST="postgres"
        POSTGRES_PORT="5432"
    fi
    
    echo "PostgreSQL host: $POSTGRES_HOST"
    echo "PostgreSQL port: $POSTGRES_PORT"
    
    # Wait for PostgreSQL (max 60 seconds)
    timeout=60
    elapsed=0
    
    until timeout 1 bash -c "cat < /dev/null > /dev/tcp/$POSTGRES_HOST/$POSTGRES_PORT" 2>/dev/null; do
        echo "Waiting for PostgreSQL... ($elapsed/$timeout seconds)"
        sleep 2
        elapsed=$((elapsed + 2))
        
        if [ $elapsed -ge $timeout ]; then
            echo "ERROR: PostgreSQL not ready after $timeout seconds"
            exit 1
        fi
    done
    
    echo "PostgreSQL is ready!"
    
    # Run migrations if APPLY_MIGRATIONS is true
    if [ "$APPLY_MIGRATIONS" = "true" ]; then
        echo "Applying database migrations..."
        dotnet ef database update --no-build || {
            echo "WARNING: Migration failed, but continuing..."
        }
    fi
else
    echo "Using SQLite database"
fi

echo "Starting FrozenApi..."
echo "========================================="

# Start the application
exec dotnet FrozenApi.dll
