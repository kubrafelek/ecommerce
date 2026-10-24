# Case Study

Yoğun trafik altında çalışan bir e-ticaret senaryosu için sipariş servisi. Sipariş veritabanına kaydedildiğinde kullanıcıya mail gönderilir. Fatura hazırlama işlemi ise kuyruk üzerinden arka planda yapılır ve fatura hazır olunca kullanıcıya fatura maili iletilir.

## İçindekiler
1. [Mimari ve Tasarım Kararları](#1-mimari-ve-tasarım-kararları)
2. [Teknoloji Seti](#2-teknoloji-seti)
3. [Kurulum (Adım Adım)](#3-kurulum-adım-adım)
4. [Çalıştırma ve Test](#4-çalıştırma-ve-test)
5. [Proje Yapısı](#5-proje-yapısı)
6. [API](#6-api)
7. [Production İçin Sonraki Adımlar](#7-production-için-sonraki-adımlar)
8. [İlerleme Durumu / Değişiklik Günlüğü](#8-ilerleme-durumu--değişiklik-günlüğü)

---

## 1. Mimari ve Tasarım Kararları

```
                 ┌──────────────────────── tek DB transaction ────────────────────────┐
Swagger ──POST──▶│ OrdersController → OrderService → Orders + OrderItems + OutboxMessage │──▶ 202 Accepted
                 └────────────────────────────────────────────────────────────────────┘
                                                 │ (Outbox delivery servisi)
                                                 ▼
                                    RabbitMQ  exchange: OrderCreated
                                       │                         │
                     queue: order-confirmation-email   queue: invoice
                                       │                         │
                     OrderConfirmationEmailConsumer   InvoiceConsumer (max 16 paralel)
                          "Siparişiniz alındı"         fatura hazırla (yavaş) → DB: Invoiced
                                       │                         │ → "Faturanız hazır"
                                       └──────────▶ SMTP (Mailpit) ◀┘
```

| Problem | Karar | Neden |
|---|---|---|
| Fatura hazırlama uzun sürüyor, 8k TPS var | Fatura işi request'ten ayrıldı, **RabbitMQ** kuyruğunda asenkron işleniyor. API `202 Accepted` döner. | Request thread'leri uzun iş için bloklanmaz; API hızlı kalır, fatura işi ayrı ölçeklenir. |
| Sipariş DB'ye yazılıp mesaj kaybolabilir (dual-write) | **MassTransit EF Core Transactional Outbox** | Sipariş ve event aynı transaction'da yazılır; RabbitMQ geçici kapalı olsa bile mesaj kaybolmaz. |
| Yavaş fatura, onay mailini geciktirmemeli | Aynı `OrderCreated` event'i **iki ayrı kuyruğa** gider | Onay maili, fatura kuyruğu birikse de anında gider. |
| Mesaj tekrar gelebilir (at-least-once) | `InvoiceConsumer` idempotent: `Status == Invoiced` ise atlar | Çift fatura kesilmez. |
| Geçici SMTP/DB hatası | Retry (1s, 5s, 15s) → sonra `*_error` kuyruğu | Hatalı mesaj kaybolmaz, incelenebilir. |
| Fatura servisi/DB'nin boğulması | `ConcurrentMessageLimit = 16` | Backpressure: kuyruk birikir ama sistem çökmez. |
| Fiyat değişirse eski sipariş etkilenmesin | `OrderItem` ürün adı + birim fiyatı **snapshot** olarak saklar | Fatura sipariş anındaki fiyatla kesilir. |
| GUID PK'lerin index fragmentation'ı | `NewId.NextSequentialGuid()` | SQL Server clustered index'e sıralı insert. |


## 2. Teknoloji Seti

| Katman | Teknoloji |
|---|---|
| Framework | .NET 10 Web API (gereksinim: min .NET 7) |
| Veritabanı | MS SQL Server 2022 (Docker) |
| ORM | Entity Framework Core 10 — **Code First** + Migrations |
| Mesajlaşma | RabbitMQ + MassTransit 8 (v8 Apache 2.0; v9+ ticari lisanslı) |
| Mail | MailKit → Mailpit (lokal sahte SMTP sunucusu, web arayüzlü) |
| Dokümantasyon | Swagger (Swashbuckle) |

## 3. Kurulum (Adım Adım)

### 3.1 Gerekli araçlar

| Araç | Kontrol komutu | Kurulum (macOS) |
|---|---|---|
| .NET SDK 8+ (projede 10 kullanıldı) | `dotnet --list-sdks` | `brew install --cask dotnet-sdk` veya https://dotnet.microsoft.com/download |
| Docker (Docker Desktop / OrbStack) | `docker info` | `brew install --cask orbstack` |

### 3.2 Altyapıyı ayağa kaldır (SQL Server, RabbitMQ, Mailpit)

```bash
docker compose up -d
docker ps   # 3 container "Up" olmalı
```

| Servis | Adres | Bilgi |
|---|---|---|
| SQL Server | `localhost,1433` | `sa` / `Str0ng!Passw0rd` |
| RabbitMQ panel | http://localhost:15672 | `guest` / `guest` |
| Mailpit (gelen mailler) | http://localhost:8025 | — |

### 3.3 EF Core aracı (sadece migration eklemek için gerekli)

```bash
dotnet tool restore   # dotnet-tools.json içindeki dotnet-ef'i kurar
```

Migration'lar `Development` ortamında uygulama açılırken **otomatik** uygulanır. Manuel uygulamak istersen:

```bash
dotnet ef database update -p src/ECommerce.Api
```

Model değiştiğinde yeni migration:

```bash
dotnet ef migrations add <Isim> -p src/ECommerce.Api -o Data/Migrations
```

## 4. Çalıştırma ve Test

```bash
dotnet run --project src/ECommerce.Api
```

- Swagger: http://localhost:5160/swagger
- Gelen mailler: http://localhost:8025

**Test senaryosu**

1. `GET /api/products` → seed edilmiş ürünleri gör (Laptop, Kulaklık, Mouse).
2. `POST /api/orders`:
   ```json
   {
     "customerEmail": "test@example.com",
     "items": [
       { "productId": 1, "quantity": 1 },
       { "productId": 2, "quantity": 2 }
     ]
   }
   ```
   → `202 Accepted`, `status: "Created"`.
3. Mailpit'te birkaç saniye içinde **"Siparişiniz alındı"** maili:
   ```
   Siparişiniz oluşturuldu.
   Sipariş Detayları;

   • Ürün adı: Laptop
   • Adet: 1
   • Birim Fiyat: 35.000,00 TL
   ...
   ```
4. ~10 sn sonra (fatura simülasyonu) **"Faturanız hazır - INV-..."** maili.
5. `GET /api/orders/{id}` → `status: "Invoiced"`, `invoiceNumber` dolu.

Fatura gecikmesi `appsettings.json > Invoice:SimulatedDelaySeconds` ile ayarlanır (veya `Invoice__SimulatedDelaySeconds=3 dotnet run ...`).

**Hata senaryoları:** geçersiz e-posta / `quantity: 0` / olmayan `productId` → `400 ValidationProblem`.

## 5. Proje Yapısı

```
ecommerce/
├── docker-compose.yml            # SQL Server, RabbitMQ, Mailpit
├── dotnet-tools.json             # dotnet-ef (local tool)
├── ECommerce.slnx
└── src/ECommerce.Api/
    ├── Program.cs                # DI, EF Core, MassTransit + Outbox, Swagger
    ├── appsettings.json          # Connection string, RabbitMQ, SMTP, fatura gecikmesi
    ├── Controllers/
    │   ├── OrdersController.cs   # POST /api/orders, GET /api/orders/{id}
    │   └── ProductsController.cs # GET /api/products
    ├── Domain/Entities.cs        # Product, Order, OrderItem, OrderStatus
    ├── Data/
    │   ├── AppDbContext.cs       # Fluent config, seed data, outbox tabloları
    │   └── Migrations/           # Code First migration'lar
    ├── Contracts/OrderCreated.cs # Kuyruk mesaj kontratı
    ├── Consumers/
    │   ├── OrderConfirmationEmailConsumer.cs  # "Siparişiniz alındı" maili
    │   └── InvoiceConsumer.cs                 # Fatura hazırlama + fatura maili
    ├── Models/OrderDtos.cs       # Request/response DTO'ları + validation
    └── Services/
        ├── OrderService.cs       # Sipariş oluşturma iş mantığı
        ├── EmailSender.cs        # MailKit SMTP gönderici
        └── EmailTemplates.cs     # Mail içerikleri
```

## 6. API

| Metot | Endpoint | Açıklama | Yanıt |
|---|---|---|---|
| GET | `/api/products` | Ürün listesi | 200 |
| POST | `/api/orders` | Sipariş oluştur | 202 / 400 |
| GET | `/api/orders/{id}` | Sipariş durumu (`Created` / `Invoiced`) | 200 / 404 |
