# План интеграции Price List Session в Batch процесс

## Что нужно сделать:

### 1. Создать ProductionPriceUpdateRunner
```csharp
// Принимает priceListId как параметр
// Обрабатывает ВСЕ товары батчами по 250
// Логирует прогресс
// Обрабатывает ошибки
```

### 2. Изменить процесс:
**Было (старое):**
- Получить товар
- Найти его в существующих прайс-листах
- Получить последнюю цену
- Создать новую цену в СУЩЕСТВУЮЩЕМ прайс-листе

**Станет (новое):**
- Создать НОВЫЙ прайс-лист сессии (1 раз в начале)
- Для каждого товара:
  - Получить текущую цену из СТАРОГО прайс-листа
  - Добавить товар в НОВЫЙ прайс-лист
  - Создать цену в НОВОМ прайс-листе
  - Сохранить в БД с новым price_list_id

### 3. Параметры запуска:
```bash
# Вариант 1: Создать прайс-лист автоматически
dotnet run -- --update-all-prices

# Вариант 2: Использовать существующий прайс-лист
dotnet run -- --update-all-prices --price-list-id=4257744

# Вариант 3: Dry-run (без реального обновления)
dotnet run -- --update-all-prices --dry-run
```

### 4. Логирование прогресса:
```
[00:00] Starting price update for 28118 goods
[00:01] Created session price list: 4257750
[00:05] Batch 1/113: Processing goods 1-250
[00:10] Batch 1/113: 250 goods processed, 245 prices created, 5 skipped
[00:15] Batch 2/113: Processing goods 251-500
...
[02:30] COMPLETED: 28118 goods, 27500 prices created, 618 skipped
[02:30] Session price list: 4257750
```

### 5. Обработка ошибок:
- Логировать все ошибки
- Продолжать при ошибке на одном товаре
- Сохранять прогресс в БД
- Возможность возобновить с определённого батча

## Структура кода:

```
Services/
└── ProductionPriceUpdateRunner.cs
    ├── RunUpdateAsync(string? priceListId, bool dryRun)
    ├── ProcessBatchAsync(batch, priceListId)
    ├── ProcessSingleGoodAsync(good, priceListId)
    └── LogProgress(batchNum, totalBatches, stats)

TestMode/
└── ProductionUpdateTester.cs
    └── RunProductionTestAsync()
```

## Вопросы для уточнения:

1. Создавать прайс-лист автоматически или передавать ID?
2. Dry-run нужен?
3. Возможность возобновить прерванное обновление?
4. Какие товары обновлять? (все, или только с определенным фильтром?)
5. Нужно ли деактивировать старые прайс-листы после обновления?
