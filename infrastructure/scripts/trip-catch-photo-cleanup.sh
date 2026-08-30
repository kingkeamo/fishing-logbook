#!/usr/bin/env bash
#
# One-time migration: copy Catch and Trip photograph objects from the old
# user-scoped R2 keys to the new immutable-identity keys.
#
#   catches/{userId}/{catchId}/{photographId}   -> catch-photographs/{catchId}/{photographId}
#   trips/{userId}/{tripId}/{photographId}      -> trip-photographs/{tripId}/{photographId}
#
# This is a copy only. It never deletes or modifies the source object, so it
# is safe to re-run (already-copied destinations are skipped).
#
# Usage:
#   ./trip-catch-photo-cleanup.sh <bucket-name> <aws-profile> [--dry-run]
#   ./trip-catch-photo-cleanup.sh <bucket-name> <aws-profile> counts
#
# The "counts" mode only lists and counts objects under the old and new
# prefixes (no copying) - useful for checking progress from another
# terminal while a migration run is in flight.
#
# Requires the AWS CLI configured with an R2 API token under the given
# profile (access key id / secret access key), e.g. via `aws configure
# --profile <profile>`. Never hardcode credentials in this script.

set -euo pipefail

ENDPOINT_URL="https://2ac0a78d455e507417ca0a2813f43d0a.r2.cloudflarestorage.com"

usage() {
    echo "Usage: $0 <bucket-name> <aws-profile> [--dry-run|counts]" >&2
    exit 1
}

BUCKET="${1:-}"
PROFILE="${2:-}"
DRY_RUN=false
COUNTS_ONLY=false

if [[ -z "$BUCKET" || -z "$PROFILE" ]]; then
    usage
fi

case "${3:-}" in
    --dry-run)
        DRY_RUN=true
        ;;
    counts)
        COUNTS_ONLY=true
        ;;
esac

if ! command -v aws >/dev/null 2>&1; then
    echo "error: the AWS CLI is required (https://aws.amazon.com/cli/)" >&2
    exit 1
fi

AWS_ARGS=(--endpoint-url "$ENDPOINT_URL" --profile "$PROFILE")

copied=0
skipped=0
failed=0

count_prefix() {
    local prefix="$1"

    local keys
    if ! keys=$(aws s3api list-objects-v2 \
        "${AWS_ARGS[@]}" \
        --bucket "$BUCKET" \
        --prefix "$prefix" \
        --query 'Contents[].Key' \
        --output text); then
        echo "  error: failed to list objects under $prefix (see AWS CLI output above)" >&2
        return 1
    fi

    if [[ -z "$keys" || "$keys" == "None" ]]; then
        echo "  $prefix: 0 object(s)"
        return 0
    fi

    local total
    total=$(wc -w <<< "$keys")
    echo "  $prefix: $total object(s)"
}

migrate_prefix() {
    local old_prefix="$1"
    local new_prefix="$2"
    local expected_segments="$3" # segments after old_prefix, e.g. userId/catchId/photographId = 3

    echo "Scanning s3://$BUCKET/$old_prefix ..."

    local keys
    if ! keys=$(aws s3api list-objects-v2 \
        "${AWS_ARGS[@]}" \
        --bucket "$BUCKET" \
        --prefix "$old_prefix" \
        --query 'Contents[].Key' \
        --output text); then
        echo "  error: failed to list objects under $old_prefix (see AWS CLI output above)" >&2
        ((failed++)) || true
        return
    fi

    if [[ -z "$keys" || "$keys" == "None" ]]; then
        echo "  no objects found under $old_prefix"
        return
    fi

    local total
    total=$(wc -w <<< "$keys")
    echo "  found $total object(s) under $old_prefix"

    echo "  listing already-migrated objects under $new_prefix ..."
    local existing_keys
    if ! existing_keys=$(aws s3api list-objects-v2 \
        "${AWS_ARGS[@]}" \
        --bucket "$BUCKET" \
        --prefix "$new_prefix" \
        --query 'Contents[].Key' \
        --output text); then
        echo "  error: failed to list objects under $new_prefix (see AWS CLI output above)" >&2
        ((failed++)) || true
        return
    fi

    declare -A already_migrated=()
    if [[ -n "$existing_keys" && "$existing_keys" != "None" ]]; then
        local existing_key
        for existing_key in $existing_keys; do
            already_migrated["$existing_key"]=1
        done
    fi

    local key
    local index=0
    for key in $keys; do
        ((index++)) || true
        # old_prefix/{userId}/{aggregateId}/{photographId}
        local remainder="${key#"$old_prefix"}"
        IFS='/' read -r -a parts <<< "$remainder"

        if [[ "${#parts[@]}" -ne "$expected_segments" ]]; then
            echo "  [$index/$total] skip (unexpected shape): $key"
            ((skipped++)) || true
            continue
        fi

        local aggregate_id="${parts[1]}"
        local photograph_id="${parts[2]}"
        local new_key="${new_prefix}${aggregate_id}/${photograph_id}"

        if [[ -n "${already_migrated[$new_key]:-}" ]]; then
            echo "  [$index/$total] already migrated: $key -> $new_key"
            ((skipped++)) || true
            continue
        fi

        if [[ "$DRY_RUN" == true ]]; then
            echo "  [$index/$total] [dry-run] would copy: $key -> $new_key"
            ((copied++)) || true
            continue
        fi

        echo "  [$index/$total] copying: $key -> $new_key"
        if aws s3 cp "${AWS_ARGS[@]}" \
            "s3://$BUCKET/$key" \
            "s3://$BUCKET/$new_key"; then
            ((copied++)) || true
        else
            echo "  failed: $key" >&2
            ((failed++)) || true
        fi
    done
}

if [[ "$COUNTS_ONLY" == true ]]; then
    echo "Counting objects in s3://$BUCKET ..."
    count_prefix "catches/"
    count_prefix "trips/"
    count_prefix "catch-photographs/"
    count_prefix "trip-photographs/"
    exit 0
fi

migrate_prefix "catches/" "catch-photographs/" 3
migrate_prefix "trips/" "trip-photographs/" 3

echo
echo "Done. copied=$copied skipped=$skipped failed=$failed"
if [[ "$DRY_RUN" == true ]]; then
    echo "(dry run - no objects were actually copied)"
fi

if [[ "$failed" -gt 0 ]]; then
    exit 1
fi
