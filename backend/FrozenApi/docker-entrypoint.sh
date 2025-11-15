#!/bin/bash
set -e

echo "========================================="
echo "FrozenApi Docker Entrypoint"
echo "========================================="

# Wait for PostgreSQL to be ready if using PostgreSQL
if [ "$USE_POSTGRES" = "true" ]; then
    echo "Waiting for PostgreSQL to be ready..."
    
    # Extract host from connection string
    POSTGRES_HOST=$(echo $ConnectionStrings__PostgreSQL | grep -oP 'Host=\K[^;]+' || echo "postgres")
    POSTGRES_PORT=$(echo $ConnectionStrings__PostgreSQL | grep -oP 'Port=\K[^;]+' || echo "5432")
    
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
