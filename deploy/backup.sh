#!/bin/sh
# Kasa gece yedeği. sqlite3 ".backup" API'si kullanılır: yazma anında bile tutarlı
# kopya üretir (cp ile kopyalama yarım transaction yakalayabilir).
set -eu

STAMP="$(date +%Y-%m-%d)"
TARGET="/backups/kasa-$STAMP.db"

sqlite3 /data/kasa.db ".backup '$TARGET'"

# 30 günden eski yedekleri temizle
find /backups -name 'kasa-*.db' -type f -mtime +30 -delete

echo "$(date '+%Y-%m-%d %H:%M:%S') yedek alındı: $TARGET"
