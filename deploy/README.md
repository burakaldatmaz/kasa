# Kasa — Production Kurulum (kasa.bkkbike.com)

## 1. İlk kurulum (sunucuda)

```bash
git clone <repo> kasa && cd kasa

# Parola hash'i üret (bcrypt, work factor 12) ve .env'e yaz:
docker compose run --rm --no-deps api hash-password '<gerçek-parola>'
cp .env.example .env        # PASSWORD_HASH= satırına üretilen hash'i yapıştır

docker compose up -d --build
curl -s http://127.0.0.1:8080/health   # {"status":"ok"} beklenir
```

- SQLite `/data/kasa.db` (named volume `kasa-data`), ilk açılışta migration'lar
  şemayı ve seed kategorileri otomatik kurar (`AUTO_MIGRATE=true`).
- `TZ=Asia/Bangkok`: "bugün" hesapları (eksik filo günü, varsayılan tarih)
  Bangkok gününe göre çalışır. Kontrol: `docker compose exec api dotnet Kasa.Api.dll now`

## 2. Nginx + sertifika

`deploy/nginx-kasa.bkkbike.com.conf` dosyasını sunucudaki nginx düzenine ekle
(`/etc/nginx/sites-available/` + symlink), sonra:

```bash
sudo certbot --nginx -d kasa.bkkbike.com   # sertifika + ssl satırları + yenileme timer'ı
sudo nginx -t && sudo systemctl reload nginx
```

Cookie `Secure` işaretli olduğundan uygulama yalnız HTTPS üzerinden çalışır;
80 portu 301 ile 443'e yönlenir, HSTS başlığı ekli.

## 3. Yedekler

`backup` servisi her gece 03:00'te (Bangkok) `sqlite3 ".backup"` ile tutarlı kopya alır:
`./backups/kasa-YYYY-MM-DD.db`, 30 günden eskiler silinir.

```bash
docker compose exec backup /usr/local/bin/kasa-backup   # elle tetikleme
sqlite3 backups/kasa-*.db 'PRAGMA integrity_check;'      # yedeği doğrulama
```

Geri dönüş: container'ı durdur, yedeği volume'a kopyala, başlat:

```bash
docker compose stop api
docker run --rm -v kasa_kasa-data:/data -v "$PWD/backups:/backups" alpine \
  cp /backups/kasa-YYYY-MM-DD.db /data/kasa.db
docker compose start api
```

## 4. Parola değiştirme

```bash
docker compose run --rm --no-deps api hash-password '<yeni-parola>'
# .env'deki PASSWORD_HASH'i güncelle, sonra:
docker compose up -d api
```

## 5. Güncelleme (yeni sürüm)

```bash
git pull && docker compose up -d --build
```
