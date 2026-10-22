#!/bin/sh
set -eu

if [ "${RUN_MIGRATIONS:-true}" = "true" ]; then
    max_attempts=6
    attempt=1

    while ! ./efbundle; do
        if [ "$attempt" -ge "$max_attempts" ]; then
            echo "Database migration failed after $max_attempts attempts." >&2
            exit 1
        fi

        wait_seconds=$((attempt * 5))
        echo "Database migration attempt $attempt failed; retrying in $wait_seconds seconds." >&2
        sleep "$wait_seconds"
        attempt=$((attempt + 1))
    done
fi

exec dotnet TeamPulse.Api.dll
