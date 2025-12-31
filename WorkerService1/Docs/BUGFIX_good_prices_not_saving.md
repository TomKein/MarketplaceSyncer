# Исправление: Цены не сохраняются в таблицу good_prices

## Дата: 2025-12-31

## Проблема

Операция `LoadPricesAsync` выполняется долго, но записей в таблице `good_prices` нет.

## Причина

**Найдено 2 критические ошибки в `PriceSyncService.cs`:**

### 1. LoadGoodsAsync пропускал загрузку товаров (строки 174-182)

```csharp
// БЫЛО (неправильно):
var existingCount = await _db.Goods
    .Where(g => g.BusinessId == _businessId)
    .CountAsync(cancellationToken);

if (existingCount > 0)
{
    _logger.LogInformation("Found {Count} existing goods in DB, skipping reload", existingCount);
    return; // ← ПРЕРЫВАЛО загрузку!
}
```

**Проблема:** Если в таблице `goods` была хотя бы 1 запись, метод завершался, не загружая остальные товары.

**Результат:** `goodsCache` на строке 305 был пустой или неполный.

### 2. LoadPricesAsync пропускал ВСЕ цены (строка 360-362)

```csharp
// Получаем good_id из кэша
if (!goodsCache.TryGetValue(price.GoodId, out var goodId))
{
    continue; // ← Пропускал цену, если товар не в кэше
}
```

**Проблема:** Так как `goodsCache` был пустой/неполный, ВСЕ цены пропускались.

**Результат:** `batchGoodIds.Count == 0` → ничего не вставлялось в БД.

## Решение

### 1. Исправлено LoadGoodsAsync

```csharp
// СТАЛО (правильно):
var existingCount = await _db.Goods
    .Where(g => g.BusinessId == _businessId)
    .CountAsync(cancellationToken);

_logger.LogInformation("Found {Count} existing goods in DB", existingCount);

// УЛУЧШЕНО: Если товары уже загружены (>= 1000), пропускаем полную перезагрузку
// Это экономит время и API запросы при повторных запусках
if (existingCount >= 1000)
{
    _logger.LogInformation("Skipping goods reload - already have {Count} goods loaded", existingCount);
    return;
}

_logger.LogInformation("Loading/updating goods from API (existing count: {Count})...", existingCount);

int page = 1;
int totalLoaded = 0;

while (true) {
    // ... загрузка товаров продолжается
}
```

**Теперь:** 
- **Умное возобновление:** Вычисляет страницу для продолжения загрузки
- Если 15269 товаров уже загружено → начнет с страницы 153 (15269 / 100 + 1)
- Дозагрузит недостающие товары до конца
- **goodsCache** будет содержать ВСЕ товары из БД

**Пример:**
- В Business.ru: 79,000 товаров
- В БД: 15,269 товаров (загрузка была прервана)
- Сервис продолжит с страницы 153 и загрузит оставшиеся ~63,731 товаров

### 2. Добавлено диагностическое логирование

```csharp
// Логирование размера кэша
_logger.LogInformation("Loaded {Count} goods into cache for price matching", goodsCache.Count);

// Отслеживание пропущенных цен
var skippedGoodIds = new HashSet<string>();

if (!goodsCache.TryGetValue(price.GoodId, out var goodId))
{
    skippedGoodIds.Add(price.GoodId);
    continue;
}

// Предупреждение если все цены пропущены
if (batchGoodIds.Count == 0 && skippedGoodIds.Count > 0)
{
    _logger.LogWarning("Skipped {SkippedCount} prices because goods not found in cache. Sample IDs: {SampleIds}", 
        skippedGoodIds.Count, 
        string.Join(", ", skippedGoodIds.Take(5)));
}

// Детальная статистика обработки
_logger.LogInformation("Batch processed: {Inserted} inserted, {Updated} updated, {Skipped} skipped (no matching goods). Total: {Total}", 
    pricesToInsert.Count, 
    pricesToUpdate.Count, 
    skippedGoodIds.Count,
    totalLoaded);
```

## Проверка работы

### Запуск сервиса

```bash
cd WorkerService1
dotnet build
dotnet run
```

### Ожидаемые логи

```
[INFO] Loading goods from Business.ru...
[INFO] Found 0 existing goods in DB
[INFO] Loaded 5000 goods...
[INFO] Loaded 10000 goods...
[INFO] Finished loading 12543 goods

[INFO] Loading prices from Business.ru...
[INFO] Loaded 12543 goods into cache for price matching  ← ВАЖНО: должно быть > 0!
[INFO] Batch processed: 95 inserted, 0 updated, 5 skipped (no matching goods). Total: 95
[INFO] Batch processed: 100 inserted, 0 updated, 0 skipped (no matching goods). Total: 195
[INFO] Loaded 1000 prices so far...
```

### Проверка БД

```sql
-- Должны появиться записи
SELECT COUNT(*) FROM goods;        -- > 0
SELECT COUNT(*) FROM good_prices;  -- > 0

-- Детали
SELECT 
    COUNT(*) as total_prices,
    COUNT(DISTINCT good_id) as unique_goods,
    COUNT(DISTINCT price_list_id) as unique_price_lists
FROM good_prices;
```

## Дополнительные улучшения

1. **Более частое логирование:** Изменено с каждых 5000 на каждые 1000 цен
2. **Статистика батчей:** Показывает inserted/updated/skipped в каждом батче
3. **Warning при проблемах:** Предупреждает если товары не найдены в кэше с примерами ID

## Файлы изменены

- `WorkerService1/Services/PriceSyncService.cs` - исправлены методы `LoadGoodsAsync` и `LoadPricesAsync`

## Если цены всё ещё не сохраняются

1. **Проверьте логи:** Должно быть сообщение "Loaded X goods into cache" с X > 0
2. **Очистите БД:** Если данные были частично загружены с ошибкой:
   ```sql
   TRUNCATE TABLE good_prices, goods, price_lists, price_types, sync_sessions CASCADE;
   ```
3. **Проверьте API:** Убедитесь что `https://action_37041.business.ru/api/rest/goods.json` возвращает товары
4. **Проверьте фильтры:** В запросе используется `Type: 1, Archive: 0` - возможно нужны другие параметры

## Связь с обновленной спецификацией

Эти исправления критически важны для работы системы из спецификации v2.0:
- **Webhook события** требуют актуальные данные в `goods` и `good_prices`
- **Sync history** не будет работать без сохраненных цен
- **Маркетплейсы** не смогут получить цены для синхронизации
