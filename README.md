# Case Study

An order service for a high-traffic e-commerce scenario. When an order is saved to the database, a confirmation email is sent to the user. Invoice generation runs in the background via a message queue, and once the invoice is ready, an invoice email is sent to the user.

## Table of Contents
1. [Architecture and Design Decisions](#1-architecture-and-design-decisions)
2. [Tech Stack](#2-tech-stack)
3. [Setup (Step by Step)](#3-setup-step-by-step)
4. [Running and Testing](#4-running-and-testing)
5. [Project Structure](#5-project-structure)
6. [API](#6-api)
7. [Screenshots](#7-screenshots)

---

## 1. Architecture and Design Decisions

```
                 ┌────────────────────── single DB transaction ───────────────────────┐
Swagger ──POST──▶│ OrdersController → OrderService → Orders + OrderItems + OutboxMessage │──▶ 202 Accepted
                 └────────────────────────────────────────────────────────────────────┘
                                                 │ (Outbox delivery service)
                                                 ▼
                                    RabbitMQ  exchange: OrderCreated
                                       │                         │
                     queue: order-confirmation-email   queue: invoice
                                       │                         │
                     OrderConfirmationEmailConsumer   InvoiceConsumer (max 16 concurrent)
                          "Order received"             prepare invoice (slow) → DB: Invoiced
                                       │                         │ → "Your invoice is ready"
                                       └──────────▶ SMTP (Mailpit) ◀┘
```


## 2. Tech Stack

| Layer | Technology |
|---|---|
| Framework | .NET 10 Web API (requirement: min .NET 7) |
| Database | MS SQL Server 2022 (Docker) |
| ORM | Entity Framework Core 10 — **Code First** + Migrations |
| Messaging | RabbitMQ + MassTransit 8 (v8 is Apache 2.0; v9+ is commercially licensed) |
| Email | MailKit → Mailpit (local fake SMTP server with a web UI) |
| Documentation | Swagger (Swashbuckle) |

## 3. Setup (Step by Step)

### 3.1 Prerequisites

| Tool | Check command | Install (macOS) |
|---|---|---|
| .NET SDK 8+ (project uses 10) | `dotnet --list-sdks` | `brew install --cask dotnet-sdk` or https://dotnet.microsoft.com/download |
| Docker (Docker Desktop / OrbStack) | `docker info` | `brew install --cask orbstack` |

### 3.2 Start the infrastructure (SQL Server, RabbitMQ, Mailpit)

```bash
docker compose up -d
docker ps   # 3 containers should be "Up"
```

| Service | Address |
|---|---|
| SQL Server | `localhost,1433` |
| RabbitMQ management UI | http://localhost:15672 |
| Mailpit (received emails) | http://localhost:8025 |

Credentials are not documented here; they are configured via `docker-compose.yml` and `appsettings.json`.

### 3.3 EF Core tool (only needed to add migrations)

```bash
dotnet tool restore   # installs dotnet-ef from dotnet-tools.json
```

Migrations are applied **automatically** at startup in the `Development` environment. To apply them manually:

```bash
dotnet ef database update -p src/ECommerce.Api
```

To add a new migration after a model change:

```bash
dotnet ef migrations add <Name> -p src/ECommerce.Api -o Data/Migrations
```

## 4. Running and Testing

```bash
dotnet run --project src/ECommerce.Api
```

- Swagger: http://localhost:5160/swagger
- Received emails: http://localhost:8025

## 5. Project Structure

```
ecommerce/
├── docker-compose.yml            # SQL Server, RabbitMQ, Mailpit
├── dotnet-tools.json             # dotnet-ef (local tool)
├── ECommerce.slnx
└── src/ECommerce.Api/
    ├── Program.cs                # DI, EF Core, MassTransit + Outbox, Swagger
    ├── appsettings.json          # Connection string, RabbitMQ, SMTP, invoice delay
    ├── Controllers/
    │   ├── OrdersController.cs   # POST /api/orders, GET /api/orders/{id}
    │   └── ProductsController.cs # GET /api/products
    ├── Domain/Entities.cs        # Product, Order, OrderItem, OrderStatus
    ├── Data/
    │   ├── AppDbContext.cs       # Fluent config, seed data, outbox tables
    │   └── Migrations/           # Code First migrations
    ├── Contracts/OrderCreated.cs # Queue message contract
    ├── Consumers/
    │   ├── OrderConfirmationEmailConsumer.cs  # "Order received" email
    │   └── InvoiceConsumer.cs                 # Invoice generation + invoice email
    ├── Models/OrderDtos.cs       # Request/response DTOs + validation
    └── Services/
        ├── OrderService.cs       # Order creation business logic
        ├── EmailSender.cs        # MailKit SMTP sender
        └── EmailTemplates.cs     # Email content
```

## 6. API

| Method | Endpoint | Description | Response |
|---|---|---|---|
| GET | `/api/products` | List products | 200 |
| POST | `/api/orders` | Create an order | 202 / 400 |
| GET | `/api/orders/{id}` | Order status (`Created` / `Invoiced`) | 200 / 404 |

## 7. Screenshots

![alt text](<screenshots/Screenshot 2026-10-24 at 3.25.46 PM.png>)
![alt text](<screenshots/Screenshot 2026-10-24 at 3.27.47 PM.png>)
![alt text](<screenshots/Screenshot 2026-10-24 at 3.28.42 PM.png>)
![alt text](<screenshots/Screenshot 2026-10-24 at 3.28.55 PM.png>)
