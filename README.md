# NotifyFlow

<img src="assets/notifyflow-banner.png" alt="NotifyFlow banner" width="100%" />

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-13-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Minimal%20APIs-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-4-FF6600?logo=rabbitmq&logoColor=white)](https://www.rabbitmq.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-planned-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-planned-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)](https://docs.docker.com/compose/)
[![Status](https://img.shields.io/badge/status-scaffold-yellow)](#roadmap)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Laboratório de **Event-Driven Architecture** em C# / .NET 10.

Não é um produto. É um projeto para estudar, quebrar, e explicar em entrevista: publicação de eventos, consumo com ACK/NACK, Outbox, retry, DLQ e idempotência.

---

## O que o sistema faz

```
HTTP  →  API  →  RabbitMQ  →  Worker  →  notificação (e-mail / webhook)
```

1. Uma API HTTP recebe eventos (ex.: usuário cadastrado, pedido confirmado)
2. A API publica esses eventos no RabbitMQ
3. Um Worker consome as mensagens
4. O Worker dispara notificações

```mermaid
flowchart LR
    Client[HTTP Client] --> Api[NotifyFlow.Api]
    Api -->|publish| Exchange[notifyflow.events]
    Exchange -->|notifications| Queue[notifyflow.notifications]
    Queue --> Worker[NotifyFlow.Worker]
    Worker --> Email[E-mail]
    Worker --> Webhook[Webhook]
```

---

## Solution

| Projeto | Tipo | Papel |
| --- | --- | --- |
| `NotifyFlow.Contracts` | classlib | Contratos de eventos compartilhados |
| `NotifyFlow.Api` | webapi (Minimal APIs) | Recebe HTTP e publica no RabbitMQ |
| `NotifyFlow.Worker` | worker | Consome filas e envia notificações |

```
NotifyFlow/
├── src/
│   ├── NotifyFlow.Contracts/     # EventEnvelope + eventos de domínio
│   ├── NotifyFlow.Api/           # Minimal APIs + publisher
│   └── NotifyFlow.Worker/        # BackgroundService + handlers
├── infrastructure/
│   └── docker-compose.yml        # RabbitMQ 4 (management plugin)
└── NotifyFlow.slnx
```

API e Worker referenciam `Contracts`. Sem MassTransit, MediatR ou AutoMapper — o client RabbitMQ é usado direto (`RabbitMQ.Client` 7.x).

---

## Conceitos em estudo

| Conceito | Onde entra | Status |
| --- | --- | --- |
| Exchanges, queues, bindings | API publisher + Worker consumer | 🔲 |
| ACK / NACK | Worker (`autoAck=false`) | 🔲 |
| Transactional Outbox | API + PostgreSQL | 🔲 |
| Retry + exponential backoff | Worker / broker | 🔲 |
| Dead Letter Queue | RabbitMQ DLX | 🔲 |
| Idempotência no consumidor | Worker + Redis | 🔲 |
| `BackgroundService` | `NotificationConsumer` | 🔲 |

---

## Stack

| Camada | Tecnologia |
| --- | --- |
| Runtime | .NET 10 / C# |
| API | ASP.NET Core Minimal APIs |
| Mensageria | RabbitMQ 4 + `RabbitMQ.Client` 7.x |
| Persistência (planejado) | PostgreSQL — Outbox |
| Cache (planejado) | Redis — idempotência |
| Infra local | Docker Compose |

---

## Roadmap

Marque conforme o laboratório avançar.

### Fase 0 — Scaffold
- [x] Solution .NET 10 com 3 projetos
- [x] Referências Api/Worker → Contracts
- [x] `RabbitMQ.Client` 7.x
- [x] Docker Compose com RabbitMQ 4
- [ ] Contratos de eventos
- [ ] Publisher na API
- [ ] Consumer no Worker

### Fase 1 — Publish / Consume
- [ ] `EventEnvelope` + eventos de domínio
- [ ] Endpoints HTTP (`202 Accepted`)
- [ ] Exchange `direct` + queue durable + binding
- [ ] Consumer com QoS, ACK e NACK
- [ ] Provider fake de notificação (console)

### Fase 2 — Confiabilidade
- [ ] Transactional Outbox (PostgreSQL)
- [ ] Retry com exponential backoff
- [ ] Dead Letter Queue
- [ ] Idempotência no consumidor (Redis)

### Fase 3 — Observabilidade
- [ ] Logs estruturados de correlação (`EventId` / `CorrelationId`)
- [ ] Health checks
- [ ] Métricas básicas do consumer

---

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## Como rodar (estado atual)

Hoje o repositório é scaffold. A API e o Worker sobem, mas ainda não publicam nem consomem eventos.

### Infra

```bash
docker compose -f infrastructure/docker-compose.yml up -d
```

| Serviço | URL |
| --- | --- |
| AMQP | `amqp://guest:guest@localhost:5672` |
| Management UI | http://localhost:15672 (`guest` / `guest`) |

### Projetos

```bash
dotnet run --project src/NotifyFlow.Api
dotnet run --project src/NotifyFlow.Worker
```

API local: `http://localhost:5172`

---

## Eventos previstos

| EventType | Payload |
| --- | --- |
| `user.registered` | `UserId`, `Email`, `Name` |
| `password.reset.requested` | `UserId`, `Email`, `ResetToken` |
| `order.confirmed` | `OrderId`, `UserId`, `Email`, `Total` |

Envelope comum: `EventId`, `EventType`, `OccurredAt`, `Source`, `CorrelationId`, `Payload`.

---

## Topologia RabbitMQ (alvo)

| Recurso | Valor |
| --- | --- |
| Exchange | `notifyflow.events` (`direct`, durable) |
| Queue | `notifyflow.notifications` (durable) |
| Routing key | `notifications` |
| Delivery | persistent |
| Consume | `prefetchCount=1`, `autoAck=false` |

---

## Convenções

- Nullable habilitado, implicit usings, `net10.0`
- Sem banco na Fase 1 — a API publica direto no broker
- Sem autenticação nesta versão de laboratório
- README acompanha o código: atualize badges, roadmap e “como rodar” a cada fase

---

## License

MIT — ver [LICENSE](LICENSE).
