# Техническое задание: Система синхронизации маркетплейсов с Business.ru

## 1. ОБЩИЕ СВЕДЕНИЯ

### 1.1 Назначение системы
Разработка автоматизированной системы двусторонней синхронизации товаров, заказов и остатков между CRM Business.ru и популярными маркетплейсами России.

### 1.2 Цели проекта
- **Централизованное управление** товарным каталогом из Business.ru
- **Автоматическая синхронизация** товаров, цен, остатков, фото
- **Обработка заказов** с маркетплейсов и создание резервов в Business.ru
- **Двусторонняя синхронизация статусов** заказов и остатков
- **Мониторинг и логирование** всех операций

### 1.3 Основа для разработки
Существующий проект **Selen** (C:\R\MarketplaceSyncer) - Windows Forms приложение на C# с использованием Selenium WebDriver для парсинга сайтов.

### 1.4 Отличия новой системы от старой
- ✅ **API + Selenium (гибридный подход)** - API там где есть, Selenium для Drom/Avito
- ✅ **Background Service** вместо Windows Forms (UI пока не требуется)
- ✅ **PostgreSQL** вместо локальных JSON файлов
- ✅ **FluentMigrator + Linq2Db** для работы с БД
- ✅ **Модульная архитектура** с поддержкой легкого добавления новых маркетплейсов
- ✅ **Event-driven архитектура** с поддержкой webhook от Business.ru
- ✅ **Надежное атомарное резервирование** с транзакциями
- ✅ **Rate limiting** и защита от блокировок
- ✅ **Retry механизмы** и обработка ошибок
- ✅ **AI-ассистент для категоризации** (Gemini) товаров на Avito
- ✅ **Система уведомлений** через Telegram/Email

---

## 2. АРХИТЕКТУРА СИСТЕМЫ

### 2.1 Технологический стек

**Backend:**
- .NET 8.0 / C# 12
- ASP.NET Core Worker Service
- FluentMigrator (миграции БД)
- Linq2Db (ORM)
- PostgreSQL 15+ (основная БД)
- Npgsql (драйвер PostgreSQL)

**HTTP & API:**
- HttpClient с Polly (retry policies)
- System.Text.Json (сериализация)
- Rate Limiter (защита от превышения лимитов API)

**Логирование:**
- Microsoft.Extensions.Logging
- Serilog (опционально для продвинутого логирования)

**Конфигурация:**
- IOptions pattern
- appsettings.json
- Environment variables

### 2.2 Общая архитектура

**Принцип работы:** Business.ru является **основным источником истины** для карточек товаров, но система гибкая и позволяет выбирать источник.

```
                    ┌──────────────────────────────┐
                    │   Business.ru (CRM)          │
                    │   - Товары (master data)     │
                    │   - Цены                     │
                    │   - Остатки                  │
                    │   - Webhooks (события)       │
                    └──────────────┬───────────────┘
                                   │ webhooks + polling
                                   ▼
┌────────────────────────────────────────────────────────────────┐
│                     Worker Service                             │
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐   │
│  │          WebhookListener (HTTP endpoint)               │   │
│  │  - Прием событий от Business.ru                        │   │
│  │  - События: goods.updated, stock.changed, etc.         │   │
│  └──────────────────────┬─────────────────────────────────┘   │
│                         │                                       │
│                         ▼                                       │
│  ┌────────────────────────────────────────────────────────┐   │
│  │          EventQueue (внутренняя очередь)               │   │
│  │  - Буферизация событий                                 │   │
│  │  - Приоритизация (продажи > обновления карточек)       │   │
│  └──────────────────────┬─────────────────────────────────┘   │
│                         │                                       │
│                         ▼                                       │
│  ┌────────────────────────────────────────────────────────┐   │
│  │          SyncOrchestrator (координатор)                │   │
│  │  - Обработка событий из очереди                        │   │
│  │  - Фоновая синхронизация (fallback polling)            │   │
│  │  - Координация маркетплейсов                           │   │
│  │  - Управление транзакциями резервирования              │   │
│  └─────┬────────┬─────────┬─────────┬──────────┬──────────┘   │
│        │        │         │         │          │               │
│        ▼        ▼         ▼         ▼          ▼               │
│    ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  ┌─────────┐           │
│    │ Ozon │ │  WB  │ │Yandex│ │Avito │  │  Drom   │           │
│    │Service│ │Service│ │Service│ │(Sel) │  │ (XML)   │           │
│    └───┬──┘ └───┬──┘ └───┬──┘ └───┬──┘  └────┬────┘           │
│        │        │         │         │          │               │
│        └────────┴─────────┴─────────┴──────────┘               │
│                         │                                       │
│                         ▼                                       │
│  ┌────────────────────────────────────────────────────────┐   │
│  │     ReservationManager (атомарное резервирование)      │   │
│  │  - Distributed lock для предотвращения двойных продаж  │   │
│  │  - Транзакционное резервирование                       │   │
│  │  - Rollback при ошибках                                │   │
│  └──────────────────────┬─────────────────────────────────┘   │
│                         │                                       │
│                         ▼                                       │
│  ┌────────────────────────────────────────────────────────┐   │
│  │     NotificationService (уведомления)                  │   │
│  │  - Telegram Bot                                        │   │
│  │  - Email                                               │   │
│  │  - Классификация ошибок                                │   │
│  └────────────────────────────────────────────────────────┘   │
│                         │                                       │
└─────────────────────────┼───────────────────────────────────────┘
                          ▼
                ┌──────────────────────┐
                │   PostgreSQL DB      │
                │  - goods             │
                │  - sync_history      │◄── история синхронизации
                │  - sync_events       │◄── события
                │  - reservations      │◄── резервы с lock
                │  - notifications     │◄── уведомления
                └──────────────────────┘
```

**Ключевые отличия новой архитектуры:**
1. **Event-driven:** Webhook от Business.ru → мгновенная реакция
2. **Fallback polling:** Если webhook не сработал, есть фоновая синхронизация
3. **Атомарное резервирование:** Distributed lock предотвращает двойные продажи
4. **Отслеживание истории:** Каждая синхронизация логируется с деталями
5. **Приоритизация:** Продажи обрабатываются первыми
6. **Гибкость источника:** Можно настроить, откуда брать master data

### 2.3 Основные компоненты

#### 2.3.1 SyncOrchestrator
**Назначение:** Главный координатор всех процессов синхронизации

**Функционал:**
- Управление таймерами и расписанием синхронизации (каждые 15 минут)
- Координация полной (Full) и инкрементальной (Lite) синхронизации
- Контроль времени выполнения циклов (не более 10 минут на цикл)
- Управление статусами: Waiting, ActiveLite, ActiveFull, NeedUpdate
- Запуск фоновых задач: проверка резервов, архивация, очистка фото

**Режимы работы:**
1. **Full Sync** - полная синхронизация всех товаров (по требованию/при старте)
2. **Lite Sync** - инкрементальная синхронизация измененных товаров (каждые 15 мин)

#### 2.3.2 IMarketplaceService (базовый интерфейс)
`csharp
public interface IMarketplaceService
{
    string Name { get; }
    bool IsEnabled { get; }
    bool IsSyncActive { get; }
    
    // Синхронизация товаров ИЗ Business.ru НА маркетплейс
    Task SyncGoodsToMarketplaceAsync(CancellationToken ct);
    
    // Получение заказов С маркетплейса и создание резервов В Business.ru
    Task FetchOrdersAndCreateReservesAsync(CancellationToken ct);
    
    // Обновление статусов заказов
    Task UpdateOrderStatusesAsync(CancellationToken ct);
    
    // Синхронизация остатков
    Task SyncStocksAsync(CancellationToken ct);
}
`

#### 2.3.3 BusinessRuClient
**Назначение:** Единая точка доступа к Business.ru API

**Функционал:**
- Аутентификация через MD5 + токен
- CRUD операции с товарами (goods)
- Управление заказами (customerorders)
- Управление резервами (reservations)
- Управление реализациями (realizations)
- Работа с ценами (salepricelistgoodprices)
- Rate limiting (request_count * 3 мс задержка)
- Retry с экспоненциальной задержкой при ошибках

---

## 3. СХЕМА БАЗЫ ДАННЫХ

### 3.1 Основные таблицы

#### 3.1.1 businesses (организации Business.ru)
`sql
CREATE TABLE businesses (
    id BIGSERIAL PRIMARY KEY,
    external_id VARCHAR(100) UNIQUE NOT NULL,
    organization_id VARCHAR(100) NOT NULL,
    name VARCHAR(500) NOT NULL,
    api_url VARCHAR(500) NOT NULL,
    app_id VARCHAR(200) NOT NULL,
    secret_encrypted VARCHAR(500) NOT NULL,
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
`

#### 3.1.2 goods (товары)
`sql
CREATE TABLE goods (
    id BIGSERIAL PRIMARY KEY,
    business_id BIGINT NOT NULL REFERENCES businesses(id),
    external_id VARCHAR(100) NOT NULL, -- ID в Business.ru
    name VARCHAR(1000) NOT NULL,
    description TEXT,
    part_number VARCHAR(200),
    store_code VARCHAR(200),
    price NUMERIC(18,2) DEFAULT 0,
    amount INT DEFAULT 0,
    reserve INT DEFAULT 0,
    archive BOOLEAN DEFAULT false,
    group_id VARCHAR(100),
    group_name VARCHAR(500),
    images JSONB, -- массив URL изображений
    attributes JSONB, -- атрибуты товара
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    last_synced_at TIMESTAMPTZ,
    UNIQUE(business_id, external_id)
);

CREATE INDEX idx_goods_business_id ON goods(business_id);
CREATE INDEX idx_goods_amount ON goods(amount) WHERE amount > 0;
CREATE INDEX idx_goods_updated ON goods(updated_at);
`

#### 3.1.3 marketplaces (маркетплейсы)
`sql
CREATE TABLE marketplaces (
    id BIGSERIAL PRIMARY KEY,
    code VARCHAR(50) UNIQUE NOT NULL, -- 'ozon', 'wb', 'yandex', 'avito', 'drom'
    name VARCHAR(200) NOT NULL,
    is_enabled BOOLEAN DEFAULT true,
    api_url VARCHAR(500),
    credentials JSONB, -- API ключи, токены
    rate_limit_config JSONB, -- настройки rate limiting
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Преднастроенные маркетплейсы
INSERT INTO marketplaces (code, name) VALUES
    ('ozon', 'Ozon'),
    ('wildberries', 'Wildberries'),
    ('yandex_market', 'Яндекс Маркет'),
    ('avito', 'Avito'),
    ('drom', 'Drom'),
    ('vk', 'ВКонтакте'),
    ('megamarket', 'MegaMarket'),
    ('izap24', 'iZap24');
`

#### 3.1.4 marketplace_products (товары на маркетплейсах)
`sql
CREATE TABLE marketplace_products (
    id BIGSERIAL PRIMARY KEY,
    good_id BIGINT NOT NULL REFERENCES goods(id),
    marketplace_id BIGINT NOT NULL REFERENCES marketplaces(id),
    external_marketplace_id VARCHAR(200), -- ID товара на маркетплейсе
    marketplace_sku VARCHAR(200),
    marketplace_url TEXT,
    marketplace_price NUMERIC(18,2),
    marketplace_stock INT,
    is_published BOOLEAN DEFAULT false,
    sync_enabled BOOLEAN DEFAULT true,
    last_synced_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(good_id, marketplace_id)
);

CREATE INDEX idx_mp_products_good_id ON marketplace_products(good_id);
CREATE INDEX idx_mp_products_marketplace_id ON marketplace_products(marketplace_id);
CREATE INDEX idx_mp_products_sync_enabled ON marketplace_products(sync_enabled) WHERE sync_enabled = true;
`

#### 3.1.5 marketplace_orders (заказы с маркетплейсов)
`sql
CREATE TABLE marketplace_orders (
    id BIGSERIAL PRIMARY KEY,
    marketplace_id BIGINT NOT NULL REFERENCES marketplaces(id),
    external_order_id VARCHAR(200) NOT NULL,
    order_number VARCHAR(200),
    customer_name VARCHAR(500),
    customer_phone VARCHAR(50),
    total_amount NUMERIC(18,2),
    status VARCHAR(100),
    order_date TIMESTAMPTZ,
    businessru_reserve_id VARCHAR(100), -- ID резерва в Business.ru
    businessru_order_id VARCHAR(100), -- ID заказа в Business.ru
    is_reserve_created BOOLEAN DEFAULT false,
    order_data JSONB, -- полные данные заказа
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(marketplace_id, external_order_id)
);

CREATE INDEX idx_mp_orders_marketplace_id ON marketplace_orders(marketplace_id);
CREATE INDEX idx_mp_orders_status ON marketplace_orders(status);
CREATE INDEX idx_mp_orders_reserve_created ON marketplace_orders(is_reserve_created);
`

#### 3.1.6 marketplace_order_items (товары в заказах)
`sql
CREATE TABLE marketplace_order_items (
    id BIGSERIAL PRIMARY KEY,
    order_id BIGINT NOT NULL REFERENCES marketplace_orders(id),
    good_id BIGINT REFERENCES goods(id),
    marketplace_product_id BIGINT REFERENCES marketplace_products(id),
    quantity INT NOT NULL,
    price NUMERIC(18,2),
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_mp_order_items_order_id ON marketplace_order_items(order_id);
CREATE INDEX idx_mp_order_items_good_id ON marketplace_order_items(good_id);
`

#### 3.1.7 sync_sessions (сессии синхронизации)
`sql
CREATE TABLE sync_sessions (
    id BIGSERIAL PRIMARY KEY,
    business_id BIGINT NOT NULL REFERENCES businesses(id),
    sync_type VARCHAR(50) NOT NULL, -- 'FULL', 'LITE'
    started_at TIMESTAMPTZ DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    status VARCHAR(50) NOT NULL, -- 'IN_PROGRESS', 'COMPLETED', 'FAILED'
    goods_fetched INT DEFAULT 0,
    goods_synced INT DEFAULT 0,
    orders_fetched INT DEFAULT 0,
    reserves_created INT DEFAULT 0,
    errors_count INT DEFAULT 0,
    error_details JSONB
);

CREATE INDEX idx_sync_sessions_business_started ON sync_sessions(business_id, started_at);
`

#### 3.1.8 sync_logs (детальные логи синхронизации)
`sql
CREATE TABLE sync_logs (
    id BIGSERIAL PRIMARY KEY,
    session_id BIGINT REFERENCES sync_sessions(id),
    marketplace_id BIGINT REFERENCES marketplaces(id),
    log_level VARCHAR(20) NOT NULL, -- 'INFO', 'WARNING', 'ERROR'
    operation VARCHAR(100), -- 'SYNC_GOODS', 'FETCH_ORDERS', 'CREATE_RESERVE'
    message TEXT NOT NULL,
    details JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_sync_logs_session_id ON sync_logs(session_id);
CREATE INDEX idx_sync_logs_created_at ON sync_logs(created_at);
CREATE INDEX idx_sync_logs_log_level ON sync_logs(log_level);
`

#### 3.1.9 sync_events (события синхронизации от Business.ru и маркетплейсов)
`sql
CREATE TABLE sync_events (
    id BIGSERIAL PRIMARY KEY,
    event_type VARCHAR(100) NOT NULL, -- 'goods.updated', 'stock.changed', 'order.created', 'order.cancelled'
    source VARCHAR(50) NOT NULL, -- 'business_ru', 'ozon', 'wildberries', etc.
    entity_type VARCHAR(50) NOT NULL, -- 'good', 'order', 'stock', 'price'
    entity_id VARCHAR(200), -- ID сущности (товара, заказа и т.д.)
    payload JSONB NOT NULL, -- полные данные события
    priority INT DEFAULT 5, -- 1 (highest) to 10 (lowest), продажи = 1, обновления = 5
    status VARCHAR(50) DEFAULT 'PENDING', -- 'PENDING', 'PROCESSING', 'COMPLETED', 'FAILED'
    processing_started_at TIMESTAMPTZ,
    processing_completed_at TIMESTAMPTZ,
    retry_count INT DEFAULT 0,
    max_retries INT DEFAULT 3,
    error_message TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_sync_events_status ON sync_events(status) WHERE status IN ('PENDING', 'PROCESSING');
CREATE INDEX idx_sync_events_priority ON sync_events(priority, created_at);
CREATE INDEX idx_sync_events_entity ON sync_events(entity_type, entity_id);
CREATE INDEX idx_sync_events_created_at ON sync_events(created_at);
`

#### 3.1.10 sync_history (детальная история синхронизации каждого товара)
`sql
CREATE TABLE sync_history (
    id BIGSERIAL PRIMARY KEY,
    good_id BIGINT NOT NULL REFERENCES goods(id),
    marketplace_id BIGINT NOT NULL REFERENCES marketplaces(id),
    sync_type VARCHAR(50) NOT NULL, -- 'PRODUCT_CREATED', 'PRODUCT_UPDATED', 'STOCK_UPDATED', 'PRICE_UPDATED', 'IMAGES_UPDATED'
    status VARCHAR(50) NOT NULL, -- 'SUCCESS', 'FAILED', 'PARTIAL'
    changes JSONB, -- что именно изменилось: {"price": {"old": 100, "new": 120}, "stock": {"old": 5, "new": 3}}
    error_message TEXT,
    error_type VARCHAR(100), -- 'CODE_ERROR', 'CLASSIFICATION_ERROR', 'USER_ACTION_REQUIRED'
    marketplace_response JSONB, -- ответ от маркетплейса
    duration_ms INT, -- время выполнения операции в миллисекундах
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_sync_history_good_marketplace ON sync_history(good_id, marketplace_id, created_at DESC);
CREATE INDEX idx_sync_history_status ON sync_history(status);
CREATE INDEX idx_sync_history_error_type ON sync_history(error_type) WHERE error_type IS NOT NULL;
CREATE INDEX idx_sync_history_created_at ON sync_history(created_at);
`

#### 3.1.11 reservations_lock (атомарное резервирование для предотвращения двойных продаж)
`sql
CREATE TABLE reservations_lock (
    id BIGSERIAL PRIMARY KEY,
    good_id BIGINT NOT NULL REFERENCES goods(id),
    marketplace_id BIGINT NOT NULL REFERENCES marketplaces(id),
    order_id BIGINT REFERENCES marketplace_orders(id),
    quantity INT NOT NULL,
    lock_token UUID NOT NULL DEFAULT gen_random_uuid(), -- уникальный токен блокировки
    status VARCHAR(50) NOT NULL DEFAULT 'LOCKED', -- 'LOCKED', 'RESERVED', 'RELEASED', 'EXPIRED'
    businessru_reserve_id VARCHAR(100), -- ID резерва в Business.ru после успешного создания
    locked_at TIMESTAMPTZ DEFAULT NOW(),
    reserved_at TIMESTAMPTZ, -- когда резерв был создан в Business.ru
    released_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ DEFAULT NOW() + INTERVAL '5 minutes', -- автоматическая разблокировка через 5 минут
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_reservations_lock_good ON reservations_lock(good_id, status);
CREATE INDEX idx_reservations_lock_status ON reservations_lock(status);
CREATE INDEX idx_reservations_lock_expires ON reservations_lock(expires_at) WHERE status = 'LOCKED';
CREATE INDEX idx_reservations_lock_token ON reservations_lock(lock_token);
`

#### 3.1.12 notifications (уведомления об ошибках и событиях)
`sql
CREATE TABLE notifications (
    id BIGSERIAL PRIMARY KEY,
    notification_type VARCHAR(50) NOT NULL, -- 'ERROR', 'WARNING', 'INFO', 'SUCCESS'
    category VARCHAR(100) NOT NULL, -- 'CODE_ERROR', 'CLASSIFICATION_ERROR', 'USER_ACTION_REQUIRED', 'RESERVATION_FAILED'
    title VARCHAR(500) NOT NULL,
    message TEXT NOT NULL,
    entity_type VARCHAR(50), -- 'good', 'order', 'reservation'
    entity_id BIGINT,
    marketplace_id BIGINT REFERENCES marketplaces(id),
    severity INT DEFAULT 5, -- 1 (critical) to 10 (info)
    status VARCHAR(50) DEFAULT 'PENDING', -- 'PENDING', 'SENT', 'FAILED', 'ACKNOWLEDGED'
    sent_at TIMESTAMPTZ,
    acknowledged_at TIMESTAMPTZ,
    acknowledged_by VARCHAR(200), -- кто подтвердил (пользователь или система)
    delivery_channels JSONB, -- {"telegram": true, "email": true}
    delivery_status JSONB, -- {"telegram": "sent", "email": "failed"}
    metadata JSONB, -- дополнительные данные для контекста
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_notifications_status ON notifications(status) WHERE status = 'PENDING';
CREATE INDEX idx_notifications_category ON notifications(category);
CREATE INDEX idx_notifications_severity ON notifications(severity, created_at DESC);
CREATE INDEX idx_notifications_created_at ON notifications(created_at);
`

### 3.2 Дополнительные таблицы (из старого проекта)

#### 3.2.1 price_types, price_lists, good_prices
*Уже реализованы в текущем проекте для управления ценами*

#### 3.2.2 attributes_for_goods (атрибуты товаров)
`sql
CREATE TABLE attributes_for_goods (
    id BIGSERIAL PRIMARY KEY,
    business_id BIGINT NOT NULL REFERENCES businesses(id),
    external_id VARCHAR(100) NOT NULL,
    name VARCHAR(500) NOT NULL,
    type VARCHAR(50), -- 'string', 'number', 'select'
    created_at TIMESTAMPTZ DEFAULT NOW()
);
`

---

## 4. ФУНКЦИОНАЛЬНЫЕ ТРЕБОВАНИЯ

### 4.1 Синхронизация товаров Business.ru → Маркетплейсы

#### 4.1.1 Общий алгоритм
1. **Загрузка товаров из Business.ru** (с фильтрацией)
   - Только type=1 (товары, не услуги)
   - Только archive=0 (не архивные)
   - С фотографиями (images.count > 0)
   - С положительным остатком (amount > 0)
   - С установленной ценой (price > 0)

2. **Проверка необходимости синхронизации**
   - Товар изменился с последней синхронизации
   - Товар еще не опубликован на маркетплейсе
   - Изменились цена или остаток

3. **Публикация/обновление товара на маркетплейсе**
   - Создание нового товара (если отсутствует)
   - Обновление существующего товара
   - Загрузка фотографий
   - Обновление цен и остатков

4. **Сохранение результата в БД**
   - Запись в marketplace_products
   - Логирование операции

#### 4.1.2 Особенности для разных маркетплейсов

**Ozon:**
- API: https://api-seller.ozon.ru
- Методы: /v2/product/import, /v1/product/info/stocks
- Требует: Client-Id, Api-Key
- Ограничения: 1000 товаров в одном запросе

**Wildberries:**
- API: https://suppliers-api.wildberries.ru
- Методы: /content/v1/cards/upload, /api/v3/stocks
- Требует: Authorization token
- Особенность: баркоды обязательны

**Яндекс Маркет:**
- API: https://api.partner.market.yandex.ru
- Методы: /campaigns/{campaignId}/offer-mapping-entries/updates
- Требует: OAuth token
- Особенность: работа через кампании

**Avito, Drom:**
- API: XML фиды
- Генерация XML файлов по расписанию
- Загрузка на FTP/HTTP endpoint

#### 4.1.3 Правила формирования названий и описаний
*Из существующего кода Class365API.CheckGrammarOfTitlesAndDescriptions():*

- Вынос Б/У из начала в конец названия
- Пробелы перед/после скобок: текст(слово) → текст (слово)
- Пробелы после запятых, точек, двоеточий
- Пробелы между цифрами и словами: 10штук → 10 штук
- Замена Г на г в годах: 2020 Г → 2020 г
- Удаление упоминаний мессенджеров (Viber, WhatsApp)
- Очистка HTML entities: &nbsp;, &ndash;, &deg;
- Удаление множественных пробелов и табуляций

### 4.2 Получение заказов с маркетплейсов

#### 4.2.1 Общий алгоритм
1. **Запрос новых заказов с маркетплейса**
   - Фильтр по статусам: awaiting_deliver, delivering
   - Фильтр по дате: за последние 24 часа

2. **Парсинг данных заказа**
   - Извлечение товаров и количества
   - Извлечение контактов покупателя
   - Расчет общей суммы

3. **Поиск товаров в Business.ru**
   - Сопоставление по SKU/артикулу
   - Проверка наличия остатков

4. **Создание резерва в Business.ru** (через Class365API.MakeReserve)
   `
   customerorders (заказ покупателя)
       ↓
   reservations (резерв товара)
       ↓
   customerordergoods (товары в заказе)
   `

5. **Сохранение в БД**
   - marketplace_orders
   - marketplace_order_items
   - Связь с businessru_reserve_id

#### 4.2.2 Параметры создания резерва
- author_employee_id (из конфига)
- responsible_employee_id (из конфига)
- organization_id (из конфига)
- partner_id = 1511892 (клиент "Маркетплейсы")
- status_id = "Маркетплейсы"
- request_source_id (источник заказа: Ozon, WB, Yandex и т.д.)
- comment = номер заказа + информация о маркетплейсе

### 4.3 Обновление статусов заказов

#### 4.3.1 Business.ru → Маркетплейс
- Резерв переведен в реализацию → обновить статус на маркетплейсе
- Товар отгружен → отметить как shipped
- Товар доставлен → отметить как delivered

#### 4.3.2 Маркетплейс → Business.ru
- Заказ отменен → отменить резерв
- Возврат товара → создать документ возврата

### 4.4 Синхронизация остатков

#### 4.4.1 Business.ru → Маркетплейсы
- Обновление остатков на маркетплейсах при изменении в Business.ru
- Batch обновление (пакетами по 100-1000 товаров)
- Rate limiting для защиты от блокировок

#### 4.4.2 Маркетплейсы → Business.ru
- Получение актуальных остатков с маркетплейсов
- Сверка с остатками в Business.ru
- Уведомление о расхождениях

### 4.5 Event-driven синхронизация через Webhook от Business.ru

**Проблема:** Polling каждые 15 минут недостаточно быстрый для оперативной реакции на изменения.

**Решение:** Business.ru поддерживает webhook, которые могут отправлять события в реальном времени.

#### 4.5.1 Настройка webhook в Business.ru

**Документация:** https://www.business.ru/articles/dev/webhooks/

**Настройка через UI Business.ru:**
1. Настройки → Интеграции → Webhooks
2. Создать новый webhook
3. URL: `https://your-worker-service.com/api/webhook/business-ru`
4. Выбрать события:
   - `goods.created` - создан новый товар
   - `goods.updated` - обновлен товар
   - `goods.deleted` - товар удален/архивирован
   - `stocks.changed` - изменился остаток
   - `prices.changed` - изменилась цена
   - `customerorders.created` - создан заказ (опционально)
   - `reservations.created` - создан резерв (опционально)
5. Настроить фильтры (например, только type=1 для товаров)
6. Включить подпись HMAC для безопасности

#### 4.5.2 Обработка webhook событий

**Архитектура обработки:**

```
Business.ru Webhook
        ↓
WebhookListener (HTTP endpoint)
        ↓
Проверка HMAC подписи
        ↓
Сохранение в sync_events (status=PENDING)
        ↓
Возврат 200 OK (быстро!)
        ↓
EventQueue (фоновая обработка)
        ↓
Приоритизация:
  - order.created (priority=1) → немедленно
  - stock.changed (priority=2) → высокий
  - price.changed (priority=3) → средний
  - goods.updated (priority=5) → обычный
        ↓
SyncOrchestrator → MarketplaceService
        ↓
Обновление на маркетплейсах
        ↓
Сохранение в sync_history
```

**Endpoint реализация:**

```csharp
[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    [HttpPost("business-ru")]
    public async Task<IActionResult> HandleBusinessRuWebhook(
        [FromBody] BusinessRuWebhookPayload payload,
        [FromHeader(Name = "X-Business-Signature")] string signature)
    {
        // 1. Проверка HMAC подписи
        if (!_webhookValidator.ValidateSignature(payload, signature))
            return Unauthorized();
        
        // 2. Сохранение события в очередь
        var eventEntity = new SyncEvent
        {
            EventType = payload.Event,
            Source = "business_ru",
            EntityType = payload.EntityType,
            EntityId = payload.EntityId,
            Payload = JsonSerializer.SerializeToDocument(payload),
            Priority = DeterminePriority(payload.Event),
            Status = "PENDING"
        };
        
        await _db.InsertAsync(eventEntity);
        
        // 3. Быстрый возврат 200 OK
        return Ok(new { received = true, eventId = eventEntity.Id });
    }
    
    private int DeterminePriority(string eventType) => eventType switch
    {
        "customerorders.created" => 1,  // Заказы - максимальный приоритет
        "reservations.created" => 1,    // Резервы - тоже критично
        "stocks.changed" => 2,          // Остатки - высокий приоритет
        "prices.changed" => 3,          // Цены - средний приоритет
        "goods.updated" => 5,           // Обновления товаров - обычный
        _ => 5
    };
}
```

#### 4.5.3 Fallback механизм

**На случай если webhook не сработал:**

1. **Периодический polling** (каждые 15 минут) как резервный механизм
2. **Проверка последнего события:** если не было событий > 30 минут → запустить полный sync
3. **Мониторинг webhook:** если webhook недоступен → переключиться на polling

### 4.6 Атомарное резервирование (решение проблемы двойных продаж)

**Критическая проблема:** При одновременной продаже товара на нескольких маркетплейсах возможны двойные продажи из-за race condition.

**Решение:** Distributed lock + транзакционное резервирование.

#### 4.6.1 Алгоритм атомарного резервирования

```sql
-- 1. Попытка захвата блокировки (atomic operation)
INSERT INTO reservations_lock (good_id, marketplace_id, order_id, quantity, lock_token)
VALUES ($1, $2, $3, $4, gen_random_uuid())
ON CONFLICT (good_id) WHERE status = 'LOCKED' DO NOTHING
RETURNING lock_token;

-- Если вернулся lock_token → блокировка захвачена, иначе → товар уже заблокирован
```

**Полный алгоритм:**

```csharp
public async Task<ReservationResult> CreateReserveAsync(
    long goodId, 
    long marketplaceId, 
    long orderId, 
    int quantity)
{
    using var transaction = await _db.BeginTransactionAsync();
    
    try
    {
        // 1. Проверка доступного остатка
        var good = await _db.Goods
            .Where(g => g.Id == goodId)
            .FirstOrDefaultAsync();
        
        if (good == null)
            return ReservationResult.Failed("Товар не найден");
        
        var availableStock = good.Amount - good.Reserve;
        if (availableStock < quantity)
            return ReservationResult.Failed($"Недостаточно товара. Доступно: {availableStock}");
        
        // 2. Атомарный захват блокировки (5 минут)
        var lockToken = await _db.Execute<Guid?>(
            @"INSERT INTO reservations_lock (good_id, marketplace_id, order_id, quantity, status, expires_at)
              VALUES (@goodId, @marketplaceId, @orderId, @quantity, 'LOCKED', NOW() + INTERVAL '5 minutes')
              ON CONFLICT (good_id) WHERE status = 'LOCKED' DO NOTHING
              RETURNING lock_token",
            new { goodId, marketplaceId, orderId, quantity });
        
        if (lockToken == null)
        {
            // Товар уже заблокирован другим процессом
            await transaction.RollbackAsync();
            return ReservationResult.Failed("Товар временно заблокирован другим заказом");
        }
        
        // 3. Создание резерва в Business.ru (внешний API вызов)
        var reserveResponse = await _businessRuClient.CreateReservationAsync(new ReservationRequest
        {
            GoodId = good.ExternalId,
            Quantity = quantity,
            MarketplaceOrderId = orderId.ToString(),
            // ... другие параметры
        });
        
        if (!reserveResponse.Success)
        {
            // Откат блокировки
            await _db.Execute(
                "UPDATE reservations_lock SET status = 'RELEASED', released_at = NOW() WHERE lock_token = @lockToken",
                new { lockToken });
            
            await transaction.RollbackAsync();
            return ReservationResult.Failed($"Ошибка создания резерва в Business.ru: {reserveResponse.Error}");
        }
        
        // 4. Успешное резервирование - обновление статуса блокировки
        await _db.Execute(
            @"UPDATE reservations_lock 
              SET status = 'RESERVED', 
                  businessru_reserve_id = @reserveId, 
                  reserved_at = NOW() 
              WHERE lock_token = @lockToken",
            new { reserveId = reserveResponse.ReservationId, lockToken });
        
        // 5. Обновление заказа
        await _db.Execute(
            @"UPDATE marketplace_orders 
              SET businessru_reserve_id = @reserveId, 
                  is_reserve_created = true, 
                  updated_at = NOW() 
              WHERE id = @orderId",
            new { reserveId = reserveResponse.ReservationId, orderId });
        
        await transaction.CommitAsync();
        
        return ReservationResult.Success(reserveResponse.ReservationId, lockToken);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        
        // Логирование ошибки и создание уведомления
        await _notificationService.CreateNotificationAsync(new NotificationRequest
        {
            Type = "ERROR",
            Category = "CODE_ERROR",
            Title = "Ошибка резервирования",
            Message = $"Не удалось создать резерв для товара {goodId}: {ex.Message}",
            EntityType = "reservation",
            EntityId = orderId,
            Severity = 1
        });
        
        throw;
    }
}
```

#### 4.6.2 Автоматическая очистка истекших блокировок

**Фоновая задача (каждую минуту):**

```csharp
public async Task CleanupExpiredLocksAsync()
{
    // Освобождение истекших блокировок
    var expiredLocks = await _db.Execute(
        @"UPDATE reservations_lock 
          SET status = 'EXPIRED', released_at = NOW()
          WHERE status = 'LOCKED' AND expires_at < NOW()
          RETURNING id, good_id, marketplace_id, order_id");
    
    foreach (var lock in expiredLocks)
    {
        // Создание уведомления об истекшей блокировке
        await _notificationService.CreateNotificationAsync(new NotificationRequest
        {
            Type = "WARNING",
            Category = "RESERVATION_FAILED",
            Title = "Блокировка истекла",
            Message = $"Резервирование товара {lock.GoodId} не было завершено в течение 5 минут",
            EntityType = "reservation",
            EntityId = lock.Id,
            Severity = 3
        });
    }
}
```

#### 4.6.3 Гарантии надежности

1. **PostgreSQL ACID транзакции:** Все операции атомарны
2. **Timeout блокировки:** Автоматическое освобождение через 5 минут
3. **Retry механизм:** При ошибке API Business.ru - повтор с экспоненциальной задержкой
4. **Rollback:** При любой ошибке - полный откат всех изменений
5. **Уведомления:** Все критические ошибки отправляются администратору
6. **Мониторинг:** Отслеживание всех блокировок и резервов в реальном времени

---

## 5. ДОПОЛНИТЕЛЬНЫЙ ФУНКЦИОНАЛ

### 5.1 Фоновые задачи (из старого проекта)

#### 5.1.1 Проверка резервов (CheckReserve)
- Поиск резервов без ответственного
- Автоматическое назначение ответственного

#### 5.1.2 Добавление артикулов (AddPartNums)
- Автозаполнение артикулов из описания товара
- Regex: извлечение номеров после символа №

#### 5.1.3 Очистка старых URL (ClearOldUrls)
- Удаление ссылок на маркетплейсы из архивных товаров
- Удаление ссылок из товаров без остатка

#### 5.1.4 Проверка архивного статуса (CheckArchiveStatus)
- Разархивация товаров с положительным остатком
- Автоматический возврат в активные

#### 5.1.5 Проверка дублей (CheckDubles)
- Поиск дублирующихся названий
- Переименование товаров без остатка (добавление точки)
- Сокращение названий с апострофами

#### 5.1.6 Удаление фото (PhotoClear)
- Удаление фото из карточек без остатка
- Отсрочка удаления для новых товаров (30 дней)
- Учет количества реализаций (+10 дней за каждую)

#### 5.1.7 Архивация (Archivate)
- Перемещение в архив карточек без фото и остатка
- Задержка 3 года для старых товаров
- Задержка 1 день для новых товаров

#### 5.1.8 Корректировка цен в реализациях (CheckRealisations)
- Автокоррекция цен в отгрузках на маркетплейсы
- Формула: цена = 75% от розничной

### 5.2 Утилиты

#### 5.2.1 Генерация XML фидов
- Avito XML (YoulaXml.cs)
- Drom XML
- Формат YML (Яндекс Маркет)

#### 5.2.2 Загрузка фото
- Конвертация в JPEG
- Ресайз до допустимых размеров
- Добавление watermark (опционально)

#### 5.2.3 Работа с атрибутами
- Автозаполнение атрибутов товаров
- Копирование размеров (width, height, length)

### 5.3 Система классификации ошибок и уведомлений

**Проблема:** Необходимо различать типы ошибок и автоматически определять, кто должен их исправить.

#### 5.3.1 Типы ошибок

**1. CODE_ERROR (ошибки в коде)**
- Описание: Техническая ошибка в логике приложения
- Примеры:
  - NullReferenceException
  - Timeout при API запросе
  - Ошибка парсинга JSON
- Действие: Уведомление разработчику → исправление кода
- Приоритет: Критический (severity=1)

**2. CLASSIFICATION_ERROR (ошибки категоризации/оформления)**
- Описание: Товар не соответствует требованиям маркетплейса
- Примеры:
  - Неправильная категория для Avito
  - Отсутствует обязательный атрибут (цвет, размер)
  - Некорректное описание
  - Недопустимые ключевые слова
- Действие: AI пытается исправить → если не получается → уведомление клиенту
- Приоритет: Средний (severity=5)

**3. USER_ACTION_REQUIRED (требуется действие клиента)**
- Описание: Проблема, которую может решить только клиент
- Примеры:
  - Недостаточно качественные фотографии
  - Товар требует сертификацию
  - Нарушение правил маркетплейса
  - Необходимо заполнить дополнительные данные
- Действие: Уведомление клиенту через Telegram/Email
- Приоритет: Высокий (severity=3)

**4. MARKETPLACE_ERROR (ошибки маркетплейса)**
- Описание: Временные проблемы на стороне маркетплейса
- Примеры:
  - API недоступен (503)
  - Rate limit exceeded
  - Маркетплейс на обслуживании
- Действие: Автоматический retry → если не помогает → уведомление разработчику
- Приоритет: Средний (severity=5)

#### 5.3.2 Классификатор ошибок

```csharp
public class ErrorClassifier
{
    public ErrorCategory ClassifyError(Exception ex, string? marketplaceResponse = null)
    {
        // 1. Проверка на ошибки кода
        if (ex is NullReferenceException or ArgumentNullException or InvalidOperationException)
            return new ErrorCategory("CODE_ERROR", 1, "Техническая ошибка в коде");
        
        // 2. Проверка на ошибки маркетплейса
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode == HttpStatusCode.TooManyRequests)
                return new ErrorCategory("MARKETPLACE_ERROR", 5, "Rate limit превышен", retry: true);
            
            if (httpEx.StatusCode == HttpStatusCode.ServiceUnavailable)
                return new ErrorCategory("MARKETPLACE_ERROR", 5, "Маркетплейс недоступен", retry: true);
        }
        
        // 3. Анализ ответа маркетплейса
        if (!string.IsNullOrEmpty(marketplaceResponse))
        {
            var errorPatterns = new Dictionary<string, ErrorCategory>
            {
                { "категория", new ErrorCategory("CLASSIFICATION_ERROR", 5, "Неверная категория", aiCanFix: true) },
                { "атрибут", new ErrorCategory("CLASSIFICATION_ERROR", 5, "Отсутствует обязательный атрибут", aiCanFix: true) },
                { "описание содержит запрещенные слова", new ErrorCategory("CLASSIFICATION_ERROR", 5, "Запрещенные слова в описании", aiCanFix: true) },
                { "требуется модерация", new ErrorCategory("USER_ACTION_REQUIRED", 3, "Требуется ручная модерация") },
                { "фотография", new ErrorCategory("USER_ACTION_REQUIRED", 3, "Проблема с фотографиями") },
                { "сертификат", new ErrorCategory("USER_ACTION_REQUIRED", 3, "Требуется сертификация") }
            };
            
            foreach (var (pattern, category) in errorPatterns)
            {
                if (marketplaceResponse.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return category;
            }
        }
        
        // По умолчанию - ошибка кода
        return new ErrorCategory("CODE_ERROR", 1, "Неизвестная ошибка");
    }
}

public record ErrorCategory(
    string Type, 
    int Severity, 
    string Description, 
    bool Retry = false, 
    bool AiCanFix = false);
```

#### 5.3.3 Telegram Bot для уведомлений

**Настройка:**
1. Создать бота через @BotFather
2. Получить токен бота
3. Получить Chat ID клиента
4. Настроить в appsettings.json

**Реализация:**

```csharp
public class TelegramNotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;
    private readonly string _chatId;
    
    public async Task SendNotificationAsync(Notification notification)
    {
        var message = FormatMessage(notification);
        
        var payload = new
        {
            chat_id = _chatId,
            text = message,
            parse_mode = "HTML",
            disable_web_page_preview = true
        };
        
        var response = await _httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{_botToken}/sendMessage",
            payload);
        
        if (response.IsSuccessStatusCode)
        {
            await UpdateNotificationStatus(notification.Id, "SENT", "telegram");
        }
        else
        {
            await UpdateNotificationStatus(notification.Id, "FAILED", "telegram");
        }
    }
    
    private string FormatMessage(Notification notification)
    {
        var emoji = notification.NotificationType switch
        {
            "ERROR" => "❌",
            "WARNING" => "⚠️",
            "INFO" => "ℹ️",
            "SUCCESS" => "✅",
            _ => "📢"
        };
        
        var categoryEmoji = notification.Category switch
        {
            "CODE_ERROR" => "🔧",
            "CLASSIFICATION_ERROR" => "🤖",
            "USER_ACTION_REQUIRED" => "👤",
            "RESERVATION_FAILED" => "📦",
            "MARKETPLACE_ERROR" => "🏪",
            _ => ""
        };
        
        return $@"{emoji} <b>{notification.Title}</b>

{categoryEmoji} Категория: {TranslateCategory(notification.Category)}
⏰ Время: {notification.CreatedAt:dd.MM.yyyy HH:mm}

{notification.Message}

{(notification.EntityType != null ? $"🔗 {notification.EntityType} ID: {notification.EntityId}" : "")}";
    }
}
```

#### 5.3.4 Email уведомления

**Реализация через SMTP:**

```csharp
public class EmailNotificationService : INotificationService
{
    private readonly SmtpClient _smtpClient;
    private readonly string _fromEmail;
    private readonly string _toEmail;
    
    public async Task SendNotificationAsync(Notification notification)
    {
        var subject = $"[{notification.NotificationType}] {notification.Title}";
        var body = GenerateHtmlBody(notification);
        
        var message = new MailMessage(_fromEmail, _toEmail)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        
        try
        {
            await _smtpClient.SendMailAsync(message);
            await UpdateNotificationStatus(notification.Id, "SENT", "email");
        }
        catch (Exception ex)
        {
            await UpdateNotificationStatus(notification.Id, "FAILED", "email");
            throw;
        }
    }
    
    private string GenerateHtmlBody(Notification notification)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; }}
        .notification {{ border: 1px solid #ddd; padding: 20px; border-radius: 5px; }}
        .error {{ border-left: 4px solid #f44336; }}
        .warning {{ border-left: 4px solid #ff9800; }}
        .info {{ border-left: 4px solid #2196f3; }}
        .success {{ border-left: 4px solid #4caf50; }}
        .metadata {{ background: #f5f5f5; padding: 10px; margin-top: 10px; }}
    </style>
</head>
<body>
    <div class='notification {notification.NotificationType.ToLower()}'>
        <h2>{notification.Title}</h2>
        <p><strong>Категория:</strong> {TranslateCategory(notification.Category)}</p>
        <p><strong>Время:</strong> {notification.CreatedAt:dd.MM.yyyy HH:mm:ss}</p>
        <p><strong>Серьезность:</strong> {notification.Severity}/10</p>
        <hr>
        <p>{notification.Message}</p>
        {(notification.EntityType != null ? $"<div class='metadata'><strong>{notification.EntityType}</strong> ID: {notification.EntityId}</div>" : "")}
    </div>
</body>
</html>";
    }
}
```

#### 5.3.5 Обработка уведомлений

**Фоновая задача (каждые 30 секунд):**

```csharp
public class NotificationProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Получение необработанных уведомлений
                var pendingNotifications = await _db.Notifications
                    .Where(n => n.Status == "PENDING")
                    .OrderBy(n => n.Severity) // Сначала критические
                    .ThenBy(n => n.CreatedAt)
                    .Take(10)
                    .ToListAsync();
                
                foreach (var notification in pendingNotifications)
                {
                    // Определение каналов доставки
                    var channels = DetermineChannels(notification);
                    
                    if (channels.Telegram)
                        await _telegramService.SendNotificationAsync(notification);
                    
                    if (channels.Email)
                        await _emailService.SendNotificationAsync(notification);
                    
                    // Обновление статуса
                    notification.Status = "SENT";
                    notification.SentAt = DateTimeOffset.Now;
                    await _db.UpdateAsync(notification);
                }
                
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки уведомлений");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
    
    private (bool Telegram, bool Email) DetermineChannels(Notification notification)
    {
        return notification.Category switch
        {
            "CODE_ERROR" => (true, true), // Критично - оба канала
            "RESERVATION_FAILED" => (true, true), // Критично
            "USER_ACTION_REQUIRED" => (true, false), // Telegram для быстрой реакции
            "CLASSIFICATION_ERROR" => (false, false), // AI обрабатывает автоматически
            "MARKETPLACE_ERROR" => (false, true), // Email для отчетности
            _ => (false, true)
        };
    }
}
```

### 5.4 AI для автоматической категоризации товаров (Gemini)

**Проблема:** На Avito очень сложная структура категорий, детерминированный алгоритм не справляется.

**Решение:** Использование Google Gemini (бесплатная модель) для автоматического выбора категории.

#### 5.4.1 Интеграция с Google Gemini API

**Настройка:**
1. Получить API ключ: https://makersuite.google.com/app/apikey
2. Использовать модель `gemini-1.5-flash` (бесплатная)
3. Rate limit: 15 запросов в минуту (бесплатный tier)

**Конфигурация:**

```json
{
  "AI": {
    "Provider": "Gemini",
    "ApiKey": "your-api-key-here",
    "Model": "gemini-1.5-flash",
    "MaxTokens": 1024,
    "Temperature": 0.1,
    "RateLimitPerMinute": 15
  }
}
```

#### 5.4.2 Реализация сервиса категоризации

```csharp
public class GeminiCategorizationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly RateLimiter _rateLimiter;
    
    public async Task<CategorySuggestion> SuggestCategoryAsync(Good good, string marketplace)
    {
        // Подготовка контекста для AI
        var prompt = BuildCategoryPrompt(good, marketplace);
        
        // Вызов Gemini API
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 1024
            }
        };
        
        await _rateLimiter.WaitAsync();
        
        var response = await _httpClient.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1/models/gemini-1.5-flash:generateContent?key={_apiKey}",
            request);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gemini API error: {error}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var aiResponse = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        
        if (string.IsNullOrEmpty(aiResponse))
            throw new Exception("Empty response from Gemini");
        
        // Парсинг ответа AI
        return ParseCategoryResponse(aiResponse, marketplace);
    }
    
    private string BuildCategoryPrompt(Good good, string marketplace)
    {
        var availableCategories = GetMarketplaceCategories(marketplace);
        
        return $@"Ты - эксперт по категоризации товаров для маркетплейса {marketplace}.

ТОВАР:
Название: {good.Name}
Описание: {good.Description}
{(good.Attributes != null ? $"Атрибуты: {JsonSerializer.Serialize(good.Attributes)}" : "")}

ДОСТУПНЫЕ КАТЕГОРИИ {marketplace}:
{string.Join("\n", availableCategories.Select(c => $"- {c.Id}: {c.Name} (родитель: {c.ParentName})"))}

ЗАДАЧА:
Выбери НАИБОЛЕЕ ПОДХОДЯЩУЮ категорию для этого товара.

ФОРМАТ ОТВЕТА (JSON):
{{
    ""category_id"": ""123"",
    ""category_name"": ""Название категории"",
    ""confidence"": 0.95,
    ""reasoning"": ""Краткое объяснение выбора"",
    ""suggested_attributes"": {{
        ""attribute_name"": ""value""
    }}
}}

ОТВЕТ:";
    }
    
    private CategorySuggestion ParseCategoryResponse(string aiResponse, string marketplace)
    {
        // Извлечение JSON из ответа (AI может добавить текст до/после)
        var jsonMatch = Regex.Match(aiResponse, @"\{[\s\S]*\}");
        if (!jsonMatch.Success)
            throw new Exception("Не удалось найти JSON в ответе AI");
        
        var suggestion = JsonSerializer.Deserialize<CategorySuggestion>(jsonMatch.Value);
        
        if (suggestion == null || suggestion.Confidence < 0.7)
        {
            // Низкая уверенность - требуется ручная проверка
            throw new LowConfidenceException($"AI не уверен в категории (confidence: {suggestion?.Confidence})");
        }
        
        return suggestion;
    }
}

public class CategorySuggestion
{
    public string CategoryId { get; set; }
    public string CategoryName { get; set; }
    public double Confidence { get; set; }
    public string Reasoning { get; set; }
    public Dictionary<string, string>? SuggestedAttributes { get; set; }
}
```

#### 5.4.3 Обработка ошибок категоризации

**Workflow:**

```
1. Попытка публикации товара на Avito
        ↓
2. Ошибка: "Неверная категория"
        ↓
3. Создание sync_event (type=CLASSIFICATION_ERROR)
        ↓
4. AI пытается подобрать правильную категорию
        ↓
   ┌────┴────┐
   ↓         ↓
Успех    Неудача (confidence < 0.7)
   ↓         ↓
Повтор    Уведомление клиенту
публикации   (USER_ACTION_REQUIRED)
```

**Код обработки:**

```csharp
public async Task HandleClassificationErrorAsync(SyncEvent syncEvent)
{
    try
    {
        var good = await _db.Goods.FindAsync(syncEvent.EntityId);
        var marketplace = syncEvent.Source;
        
        // Вызов AI для подбора категории
        var suggestion = await _geminiService.SuggestCategoryAsync(good, marketplace);
        
        // Обновление товара с новой категорией
        await UpdateGoodCategoryAsync(good.Id, marketplace, suggestion);
        
        // Повторная попытка публикации
        await _syncOrchestrator.RetryPublishAsync(good.Id, marketplace);
        
        // Логирование успеха
        await _db.InsertAsync(new SyncHistory
        {
            GoodId = good.Id,
            MarketplaceId = GetMarketplaceId(marketplace),
            SyncType = "CATEGORY_FIXED_BY_AI",
            Status = "SUCCESS",
            Changes = JsonSerializer.SerializeToDocument(new
            {
                ai_suggestion = suggestion,
                confidence = suggestion.Confidence
            })
        });
    }
    catch (LowConfidenceException ex)
    {
        // AI не уверен - нужна помощь клиента
        await _notificationService.CreateNotificationAsync(new NotificationRequest
        {
            Type = "WARNING",
            Category = "USER_ACTION_REQUIRED",
            Title = $"Требуется выбор категории для товара",
            Message = $"AI не смог определить категорию с достаточной уверенностью.\n\n" +
                      $"Товар: {good.Name}\n" +
                      $"Маркетплейс: {marketplace}\n" +
                      $"Уверенность AI: {ex.Confidence:P0}\n\n" +
                      $"Пожалуйста, выберите категорию вручную.",
            EntityType = "good",
            EntityId = good.Id,
            Severity = 3
        });
    }
    catch (Exception ex)
    {
        // Ошибка в коде AI
        await _notificationService.CreateNotificationAsync(new NotificationRequest
        {
            Type = "ERROR",
            Category = "CODE_ERROR",
            Title = "Ошибка AI категоризации",
            Message = $"Не удалось использовать AI для категоризации товара {good.Id}: {ex.Message}",
            EntityType = "good",
            EntityId = good.Id,
            Severity = 1
        });
    }
}
```

---

## 6. НЕФУНКЦИОНАЛЬНЫЕ ТРЕБОВАНИЯ

### 6.1 Производительность
- Полная синхронизация: не более 10 минут
- Инкрементальная синхронизация: не более 5 минут
- Batch операции: по 100-250 записей
- Rate limiting: соблюдение лимитов API каждого маркетплейса

### 6.2 Надежность
- Retry механизмы с экспоненциальной задержкой
- Транзакции для критичных операций
- Логирование всех операций
- Мониторинг ошибок

### 6.3 Безопасность
- Шифрование API ключей в БД
- Использование Environment Variables для секретов
- Ограничение доступа к БД

### 6.4 Масштабируемость
- Легкое добавление новых маркетплейсов
- Возможность отключения отдельных маркетплейсов
- Параллельная синхронизация нескольких маркетплейсов

### 6.5 Мониторинг
- Статус синхронизации в реальном времени
- История сессий синхронизации
- Уведомления об ошибках (опционально)

---

## 7. КОНФИГУРАЦИЯ

### 7.1 Структура appsettings.json

\\\json
{
  "BusinessRu": {
    "BaseUrl": "https://api.business.ru/",
    "OrganizationId": "75519",
    "AppId": "XXXXX",
    "Secret": "XXXXX",
    "PageLimit": 250,
    "RateLimitDelayMs": 100,
    "MaxRetries": 5,
    "ResponsibleEmployeeId": "76221",
    "AuthorEmployeeId": "76221",
    "PartnerId": "1511892"
  },
  "SyncSettings": {
    "CheckIntervalMinutes": 15,
    "SyncStartHour": 0,
    "SyncStopHour": 24,
    "LiteScanTimeShiftMinutes": 5,
    "MaxCycleDurationMinutes": 10
  },
  "Marketplaces": {
    "Ozon": {
      "Enabled": true,
      "ClientId": "XXXXX",
      "ApiKey": "XXXXX",
      "RateLimitPerSecond": 10
    },
    "Wildberries": {
      "Enabled": true,
      "ApiKey": "XXXXX",
      "RateLimitPerSecond": 5
    },
    "YandexMarket": {
      "Enabled": true,
      "OAuth": "XXXXX",
      "CampaignId": "XXXXX"
    },
    "Avito": {
      "Enabled": true,
      "UserId": "XXXXX",
      "ClientId": "XXXXX",
      "ClientSecret": "XXXXX"
    },
    "Drom": {
      "Enabled": true,
      "FeedUrl": "https://example.com/drom.xml"
    }
  },
  "BackgroundTasks": {
    "CheckReserve": true,
    "AddPartNums": true,
    "ClearOldUrls": true,
    "CheckArchiveStatus": true,
    "CheckDubles": true,
    "CheckDublesCount": 100,
    "PhotoClear": true,
    "PhotosCheckCount": 10,
    "PhotosCheckDays": 30,
    "Archivate": true,
    "ArchivateCount": 50,
    "CheckRealisations": true,
    "CheckGrammar": true,
    "DescriptionsCheckCount": 100
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=marketplace_sync;Username=postgres;Password=XXXXX"
  }
}
\\\

---

## 8. ЭТАПЫ РАЗРАБОТКИ

### Этап 1: Инфраструктура (базовый фундамент)
- ✅ Схема БД PostgreSQL + миграции FluentMigrator
- ✅ Базовые модели Linq2Db
- ✅ BusinessRuClient с rate limiting
- ✅ Конфигурация через IOptions
- ⏳ SyncOrchestrator (главный координатор)

### Этап 2: Интеграция с маркетплейсами
- ⏳ IMarketplaceService (базовый интерфейс)
- ⏳ OzonService
- ⏳ WildberriesService
- ⏳ YandexMarketService
- ⏳ AvitoService (XML фид)
- ⏳ DromService (XML фид)

### Этап 3: Синхронизация товаров
- ⏳ Загрузка товаров из Business.ru
- ⏳ Публикация товаров на маркетплейсы
- ⏳ Обновление цен и остатков
- ⏳ Загрузка фотографий

### Этап 4: Обработка заказов
- ⏳ Получение заказов с маркетплейсов
- ⏳ Создание резервов в Business.ru
- ⏳ Синхронизация статусов заказов

### Этап 5: Фоновые задачи
- ⏳ Проверка резервов
- ⏳ Добавление артикулов
- ⏳ Очистка старых URL
- ⏳ Проверка архивного статуса
- ⏳ Проверка дублей
- ⏳ Удаление фото
- ⏳ Архивация
- ⏳ Корректировка цен в реализациях

### Этап 6: Мониторинг и отчетность
- ⏳ Dashboard (опционально)
- ⏳ Email уведомления об ошибках
- ⏳ Статистика синхронизации

---

## 9. МИГРАЦИЯ СО СТАРОЙ СИСТЕМЫ

### 9.1 Что переносим
- Логика Business.ru API (✅ частично готова)
- Алгоритмы обработки текста (названия, описания)
- Правила фильтрации товаров
- Логику создания резервов

### 9.2 Что НЕ переносим
- Selenium WebDriver (заменяем на API)
- Windows Forms UI (пока не нужен)
- Локальные JSON файлы (заменяем на PostgreSQL)

### 9.3 Совместимость
- Сохранение совместимости с ID товаров Business.ru
- Сохранение структуры данных для возможного возврата

---

## 10. ТЕСТИРОВАНИЕ

### 10.1 Unit тесты
- Тестирование BusinessRuClient
- Тестирование сервисов маркетплейсов
- Тестирование утилит (парсинг, форматирование)

### 10.2 Integration тесты
- Тестирование с тестовыми аккаунтами маркетплейсов
- Тестирование БД операций

### 10.3 E2E тесты
- Полный цикл синхронизации на тестовых данных

---

## 11. ДОКУМЕНТАЦИЯ

### 11.1 Техническая документация
- README с описанием архитектуры
- API документация для каждого маркетплейса
- Схема БД с описанием таблиц

### 11.2 Инструкции
- Руководство по настройке
- Руководство по добавлению нового маркетплейса
- Troubleshooting guide

---

## 12. ИЗВЕСТНЫЕ ПРОБЛЕМЫ ИЗ СТАРОЙ СИСТЕМЫ

### 12.1 Business.ru API
- **Баг с ценами:** необходимость костыля UpdatePrices() - изменение цены на +0.01, потом обратно
- **Rate limiting:** жесткие ограничения, нужна задержка request_count * 3 мс
- **Токен устаревание:** периодически требуется RepairAsync() для обновления токена

### 12.2 Маркетплейсы
- **Ozon:** требует баркоды, сложная категоризация
- **Wildberries:** долгая модерация товаров (до 3 дней)
- **Яндекс Маркет:** работа через кампании, не всегда очевидная логика
- **Avito:** частые изменения в XML схеме

---

## ПРИЛОЖЕНИЯ

### A. Примеры эндпоинтов Business.ru API

\\\
GET /goods.json?type=1&archive=0&limit=250&page=1&with_prices=1&type_price_ids[0]=75524
GET /salepricetypes.json?limit=250
GET /salepricelists.json?limit=250&f[active]=1
GET /salepricelistgoodprices.json?limit=250&offset=0
PUT /salepricelistgoodprices.json?id=XXX&price=1234.56
POST /customerorders.json
POST /reservations.json
POST /customerordergoods.json
\\\

### B. Примеры эндпоинтов маркетплейсов

**Ozon:**
\\\
POST /v2/product/import
GET /v1/product/info/stocks
POST /v1/product/import/prices
\\\

**Wildberries:**
\\\
POST /content/v1/cards/upload
GET /api/v3/stocks
POST /api/v3/stocks
\\\

**Яндекс Маркет:**
\\\
POST /campaigns/{campaignId}/offer-mapping-entries/updates
GET /campaigns/{campaignId}/orders
\\\

---

## КОНТАКТЫ И ВОПРОСЫ

По всем вопросам обращаться к разработчику.

**Дата создания:** 2025-12-31
**Последнее обновление:** 2025-12-31
**Версия:** 2.0
**Статус:** Обновлено с учетом требований клиента

---

## ИСТОРИЯ ИЗМЕНЕНИЙ

### Версия 2.0 (2025-12-31)

**Критические изменения на основе фидбека клиента:**

1. **Пункт 1.4 - Гибридный подход вместо полного отказа от Selenium**
   - ❌ Удалено: "Отказ от Selenium - переход на API"
   - ✅ Добавлено: Гибридный подход - API где есть, Selenium для Drom и частично Avito
   - **Причина:** У Drom нет открытого API

2. **Раздел 2.2 - Гибкая архитектура источника истины**
   - ❌ Удалено: Жесткая централизация на Business.ru
   - ✅ Добавлено: Business.ru как основной источник с возможностью выбора
   - ✅ Добавлено: Event-driven архитектура с webhook
   - **Причина:** Нужна гибкость выбора master source для карточек товаров

3. **Раздел 3.1 - Новые таблицы для отслеживания**
   - ✅ Добавлена: `sync_events` - очередь событий с приоритизацией
   - ✅ Добавлена: `sync_history` - детальная история синхронизации каждого товара
   - ✅ Добавлена: `reservations_lock` - атомарное резервирование
   - ✅ Добавлена: `notifications` - система уведомлений
   - **Причина:** Необходимо знать что обновлено, а что нет

4. **Раздел 4.5 - Webhook интеграция с Business.ru**
   - ✅ Добавлен: Полный раздел про webhook от Business.ru
   - ✅ Добавлен: Event-driven обработка с приоритизацией
   - ✅ Добавлен: Fallback polling как резервный механизм
   - **Причина:** Нужна оперативная синхронизация, 15 минут - слишком долго

5. **Раздел 4.6 - Атомарное резервирование**
   - ✅ Добавлен: Distributed lock для предотвращения двойных продаж
   - ✅ Добавлен: Транзакционное резервирование с rollback
   - ✅ Добавлен: Автоматическая очистка истекших блокировок
   - **Причина:** Главная проблема - ненадежное резервирование и двойные продажи

6. **Раздел 5.3 - Система классификации ошибок**
   - ✅ Добавлена: Классификация на CODE_ERROR, CLASSIFICATION_ERROR, USER_ACTION_REQUIRED, MARKETPLACE_ERROR
   - ✅ Добавлена: Telegram Bot для уведомлений
   - ✅ Добавлена: Email уведомления
   - **Причина:** Нужно автоматически определять, кто должен исправлять ошибки

7. **Раздел 5.4 - AI для категоризации**
   - ✅ Добавлена: Интеграция с Google Gemini (бесплатная модель)
   - ✅ Добавлена: Автоматический подбор категорий для Avito
   - ✅ Добавлена: Обработка ошибок с низкой уверенностью AI
   - **Причина:** Сложный выбор категорий на Avito, детерминировать крайне сложно

**Основные акценты новой версии:**
- 🚀 **Быстрая реакция:** Webhook + event queue с приоритизацией
- 🔒 **Надежное резервирование:** Distributed lock, транзакции, rollback
- 📊 **Полное отслеживание:** История всех синхронизаций
- 🤖 **Умная автоматизация:** AI для категоризации + автоклассификация ошибок
- 📢 **Проактивные уведомления:** Telegram/Email с правильной маршрутизацией
- 🎯 **Следующий этап:** Отчетность (после стабилизации основного функционала)

