# План задачи: Синхронизация цен Business.ru

## Цель задачи
Выгрузить из Business.ru цены на все товары, сохранить их в PostgreSQL БД, увеличить на 15% и обновить цены обратно в Business.ru.

## Технические требования
- **БД**: PostgreSQL (уже развернута)
- **ORM**: Linq2Db
- **Миграции**: FluentMigrator
- **API**: Business.ru API (Class365)
- **Эталонный код**: `C:\R\MarketplaceSyncer\Base\Class365API.cs` → метод `UpdatePrices`

## Ключевые принципы
1. ✅ **Избегать длинных циклов** - писать данные в БД порциями
2. ✅ **Сохранять обе цены** (старую и новую) для предотвращения повторного повышения
3. ✅ **Проектировать схему БД** с учетом будущего клонирования Business.ru
4. ✅ **Синхронизация маркетплейсов** - готовить инфраструктуру для главной миссии

---

## Этап 1: Проектирование схемы БД

### Таблица: `businesses` (справочник организаций)
Для поддержки множественных клонов Business.ru
```sql
- id (bigserial, PK)
- external_id (varchar, уникальный ID из Business.ru)
- organization_id (varchar, ID организации из Business.ru)
- name (varchar, название организации)
- api_url (varchar, базовый URL API)
- app_id (varchar)
- secret_encrypted (varchar, зашифрованный секрет)
- is_active (boolean)
- created_at (timestamptz)
- updated_at (timestamptz)
```

### Таблица: `goods` (товары)
Хранение информации о товарах из Business.ru
```sql
- id (bigserial, PK)
- business_id (bigint, FK → businesses.id)
- external_id (varchar, ID товара из Business.ru)
- name (varchar, название товара)
- part_number (varchar, артикул)
- store_code (varchar, код магазина)
- archive (boolean, в архиве?)
- created_at (timestamptz)
- updated_at (timestamptz)
- last_synced_at (timestamptz)
- UNIQUE(business_id, external_id)
```

### Таблица: `price_types` (типы цен)
Справочник типов цен (Розничная, Оптовая и т.д.)
```sql
- id (bigserial, PK)
- business_id (bigint, FK → businesses.id)
- external_id (varchar, ID типа цены из Business.ru)
- name (varchar, название типа цены)
- is_sale_price (boolean, тип отпускной цены?)
- is_buy_price (boolean, тип закупочной цены?)
- created_at (timestamptz)
- updated_at (timestamptz)
- UNIQUE(business_id, external_id)
```

### Таблица: `price_lists` (прайс-листы)
Прайс-листы связывают типы цен с товарами
```sql
- id (bigserial, PK)
- business_id (bigint, FK → businesses.id)
- external_id (varchar, ID прайс-листа из Business.ru)
- price_type_id (bigint, FK → price_types.id)
- name (varchar)
- is_active (boolean)
- created_at (timestamptz)
- updated_at (timestamptz)
- UNIQUE(business_id, external_id)
```

### Таблица: `good_prices` (цены товаров) - КЛЮЧЕВАЯ!
Хранение истории цен с возможностью отслеживания изменений

**ПРИМЕЧАНИЕ**: Флаг `is_processed` используется для защиты от случайных повторных запусков.
Альтернатива - сравнение `calculated_price` с `current_price` перед обновлением.
Используем оба подхода для максимальной надежности.

```sql
- id (bigserial, PK)
- business_id (bigint, FK → businesses.id)
- good_id (bigint, FK → goods.id)
- price_list_id (bigint, FK → price_lists.id)
- external_price_record_id (varchar, ID из salepricelistgoodprices)
- original_price (numeric(18,2), исходная цена из Business.ru при первой загрузке)
- calculated_price (numeric(18,2), вычисленная цена +15%)
- current_price (numeric(18,2), текущая цена в Business.ru, обновляется при каждой синхронизации)
- price_increase_percent (numeric(5,2), процент повышения, по умолчанию 15.00)
- calculation_date (timestamptz, дата расчета)
- updated_in_businessru_at (timestamptz, дата обновления в Business.ru)
- is_processed (boolean, обработана ли запись? Защита от повторного запуска)
- currency_id (varchar)
- created_at (timestamptz)
- updated_at (timestamptz)
- UNIQUE(business_id, good_id, price_list_id)
- INDEX на (is_processed, business_id)
- INDEX на (calculation_date)
```

**Логика защиты от двойного повышения:**
```csharp
// Перед обновлением проверяем оба условия:
if (is_processed == false && Math.Abs(current_price - calculated_price) > 0.01m)
{
    // Можно обновлять
}
```

### Таблица: `price_update_logs` (журнал обновлений)
Аудит всех изменений цен
```sql
- id (bigserial, PK)
- good_price_id (bigint, FK → good_prices.id)
- old_price (numeric(18,2))
- new_price (numeric(18,2))
- action_type (varchar, 'FETCH', 'CALCULATE', 'UPDATE', 'ERROR')
- error_message (text, null если успех)
- created_at (timestamptz)
- INDEX на (created_at)
```

### Таблица: `sync_sessions` (сессии синхронизации)
Отслеживание запусков процесса
```sql
- id (bigserial, PK)
- business_id (bigint, FK → businesses.id)
- started_at (timestamptz)
- completed_at (timestamptz, null если в процессе)
- status (varchar, 'IN_PROGRESS', 'COMPLETED', 'FAILED')
- goods_fetched (int)
- prices_fetched (int)
- prices_calculated (int)
- prices_updated (int)
- errors_count (int)
- error_details (jsonb)
```

---

## Этап 2: API эндпоинты Business.ru

### Получение товаров (goods)
```http
GET /goods.json
Параметры:
- type=1 (товары, не услуги)
- archive=0 (не архивные)
- limit=250 (макс. за запрос)
- page={N} (пагинация)
- with_prices=1 (с ценами)
```

### Получение типов цен (salepricetypes)
```http
GET /salepricetypes.json
Параметры:
- limit=250
```

### Получение прайс-листов (salepricelists)
```http
GET /salepricelists.json
Параметры:
- limit=250
- f[active]=1 (только активные)
```

### Получение цен товаров (salepricelistgoodprices) - КЛЮЧЕВОЙ!
```http
GET /salepricelistgoodprices.json
Параметры:
- limit=250
- offset={N} (пагинация через offset вместо page)
- f[price_list_id]={ID} (опционально, фильтр по прайс-листу)
```

### Обновление цены (salepricelistgoodprices)
```http
PUT /salepricelistgoodprices.json
Параметры:
- id={external_price_record_id} (ID записи из salepricelistgoodprices)
- price={новая_цена} (формат: 12345.67)
```

---

## Этап 3: Алгоритм работы

### Шаг 1: Инициализация (один раз)
1. Создать запись в `businesses` для текущей организации
2. Загрузить типы цен → таблица `price_types`
3. Загрузить прайс-листы → таблица `price_lists`

### Шаг 2: Загрузка товаров (порциями по 250)
```
ДЛЯ КАЖДОЙ страницы (page=1, 2, 3...):
  1. GET /goods.json?limit=250&page={N}
  2. Сохранить в goods (BATCH INSERT/UPDATE)
  3. Логировать прогресс каждые 1000 товаров
  ЕСЛИ получено < 250 → ВЫХОД
```

### Шаг 3: Загрузка цен (порциями через offset)
```
ДЛЯ КАЖДОГО активного прайс-листа:
  offset = 0
  ЦИКЛ:
    1. GET /salepricelistgoodprices.json?limit=250&offset={offset}&f[price_list_id]={ID}
    2. Преобразовать в good_prices:
       - original_price = текущая цена из API
       - current_price = текущая цена из API
       - calculated_price = NULL (пока не вычислена)
       - is_processed = FALSE
    3. UPSERT в good_prices (BATCH)
    4. offset += 250
    5. Логировать каждые 5000 цен
    ЕСЛИ получено < 250 → ВЫХОД из цикла
```

### Шаг 4: Расчет новых цен (в БД, без API)
```sql
UPDATE good_prices
SET 
  calculated_price = original_price * 1.15,
  price_increase_percent = 15.00,
  calculation_date = NOW()
WHERE 
  is_processed = FALSE 
  AND calculated_price IS NULL
  AND business_id = {current_business_id}
```

### Шаг 5: Обновление цен в Business.ru (порциями по 100)
```
ВЫБРАТЬ good_prices WHERE is_processed = FALSE LIMIT 100:
  ДЛЯ КАЖДОЙ записи:
    1. PUT /salepricelistgoodprices.json
       - id = external_price_record_id
       - price = calculated_price
    2. ЕСЛИ успех:
       - UPDATE good_prices SET 
           is_processed = TRUE,
           updated_in_businessru_at = NOW(),
           current_price = calculated_price
       - INSERT в price_update_logs (action_type='UPDATE')
    3. ЕСЛИ ошибка:
       - INSERT в price_update_logs (action_type='ERROR')
    4. Пауза request_count * 3 мс (защита от rate limit)
  
  COMMIT транзакции каждые 100 записей
  Логировать прогресс
```

---

## Этап 4: Защита от повторного повышения

### Логика проверки:
```sql
-- Перед новым прогоном проверяем, есть ли уже обработанные цены
SELECT COUNT(*) FROM good_prices 
WHERE is_processed = TRUE 
  AND calculation_date > NOW() - INTERVAL '7 days'
  AND business_id = {current_business_id}

-- Если > 0, то предупреждение:
-- "Обнаружены обработанные цены за последние 7 дней. 
--  Продолжить можно только после сброса флага is_processed 
--  или удаления старых записей."
```

### Режим повторного запуска:
- Опция `--reset-processed-flag` для сброса `is_processed = FALSE`
- Опция `--recalculate` для пересчета на основе `current_price` (а не `original_price`)

---

## Этап 5: Обработка ошибок

### Rate Limiting
- Учитывать `request_count` в ответе API
- Динамическая пауза: `Math.Max(100, request_count * 3)` мс

### Сетевые ошибки
- Retry с экспоненциальной задержкой (1s, 2s, 4s, 8s, 16s)
- Макс. 5 попыток
- Логирование в `price_update_logs`

### Ошибки транзакций БД
- Использовать транзакции для батчей
- Rollback при ошибке
- Повтор батча с меньшим размером (250 → 100 → 50 → 10)

---

## Этап 6: Мониторинг и логирование

### Метрики
- Товаров загружено / всего
- Цен загружено / всего
- Цен обновлено / всего
- Ошибок / всего
- Время работы / ETA

### Лог-сообщения
```
[INFO] Starting price sync session for business_id={ID}
[INFO] Fetching goods: page {N}, total loaded: {COUNT}
[INFO] Fetching prices: offset {N}, total loaded: {COUNT}
[INFO] Calculating prices for {COUNT} goods...
[INFO] Updating prices: batch {N}, success: {X}, errors: {Y}
[INFO] Price sync completed. Updated {X} prices in {TIME}
[ERROR] Failed to update price for good_id={ID}: {MESSAGE}
```

---

## Этап 7: Расширяемость для будущей синхронизации

### Таблица: `marketplace_products` (товары маркетплейсов)
```sql
- id (bigserial, PK)
- good_id (bigint, FK → goods.id)
- marketplace_type (varchar, 'OZON', 'WILDBERRIES', 'YANDEX_MARKET', ...)
- external_marketplace_id (varchar, ID товара на маркетплейсе)
- marketplace_sku (varchar)
- marketplace_price (numeric(18,2))
- last_synced_at (timestamptz)
- sync_enabled (boolean)
```

### Таблица: `sync_rules` (правила синхронизации)
```sql
- id (bigserial, PK)
- business_id (bigint, FK → businesses.id)
- marketplace_type (varchar)
- price_markup_percent (numeric(5,2))
- sync_interval_minutes (int)
- is_active (boolean)
```

---

## Этап 8: Конфигурация (appsettings.json)

```json
{
  "BusinessRu": {
    "BaseUrl": "https://api.business.ru/",
    "OrganizationId": "XXXXX",
    "AppId": "XXXXX",
    "Secret": "XXXXX",
    "PageLimit": 250,
    "RateLimitDelayMs": 100,
    "MaxRetries": 5
  },
  "PriceSync": {
    "DefaultMarkupPercent": 15.0,
    "BatchSize": 100,
    "ProtectFromDuplicateRunDays": 7,
    "PriceComparisonTolerance": 0.01
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=marketplace_sync;Username=postgres;Password=XXXXX;Include Error Detail=true"
  }
}
```

**ПРИМЕЧАНИЕ**: Все значения с `XXXXX` заменить на реальные данные перед запуском.

---

## План согласования с заказчиком

### ✅ СОГЛАСОВАННЫЕ ТРЕБОВАНИЯ:
1. **Типы цен**: ВСЕ типы цен (не фильтруем)
2. **Закупочные цены**: НЕТ, только `salepricelistgoodprices`
3. **Размер батча**: 100 записей (вынести в IOptions)
4. **Откат цен**: Механизм не нужен, но данные сохраняем (original_price)
5. **Частота запуска**: ОДИН РАЗ (повышение налога)

---

## Следующие шаги:
1. **Согласовать план** с заказчиком
2. **Уточнить схему БД** (добавить/убрать таблицы)
3. **Перейти к этапу 1**: Создание миграций FluentMigrator
4. **Перейти к этапу 2**: Создание моделей Linq2Db
5. **...далее по плану**
