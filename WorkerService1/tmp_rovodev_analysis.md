# Анализ данных и готовность к повышению цен

## Данные из теста (товар ID: 162695)

### Получено из API:
- **Товар**: "Абсорбер заднего бампера ЗАЗ Sens"
- **4 связки** товара с прайс-листами (salepricelistgoods)
- **3 версии цены** типа 75524:

| Price ID | Price | Updated | Price List Good ID | Актуальность |
|----------|-------|---------|-------------------|--------------|
| 197927 | 800₽ | 12.02.2017 | 197926 | Старая |
| 2065375 | 1000₽ | 03.03.2022 | 2065373 | Средняя |
| 2310770 | 800₽ | 11.01.2023 | 2310769 | **ТЕКУЩАЯ** |

## Важные выводы:

### 1. Business.ru хранит ВЕРСИИ цен
- Каждая цена имеет поле `updated` (дата изменения)
- При изменении цены создается НОВАЯ запись (не обновляется старая)
- Текущая цена = запись с самой свежей датой `updated`

### 2. Проблема: Множественные цены для одного товара
**У одного товара может быть несколько прайс-листов** с одним типом цены!
В нашем примере:
- 4 прайс-листа → 3 цены типа 75524
- Нужно обрабатывать ВСЕ цены

### 3. Модели БД - ГОТОВЫ ✅

#### GoodPrice (ключевая таблица):
```
- original_price: исходная цена (при первой загрузке)
- calculated_price: вычисленная с наценкой (+15%)
- current_price: текущая цена из Business.ru
- is_processed: флаг защиты от повторного повышения
- external_price_record_id: ID записи цены в Business.ru
```

**Защита от повторного повышения:**
```
IF current_price == calculated_price AND is_processed == true
    → SKIP (уже повышено)
ELSE
    → Повысить цену
```

### 4. Отсутствующие поля в модели GoodPrice:

❌ **Нет поля для хранения `price_type_id`**
- Нужно добавить для фильтрации по типу цены (75524)

❌ **Нет поля для `price_list_good_id`**
- Это ID связки товара с прайс-листом
- Нужен для правильной идентификации в Business.ru

❌ **Нет поля для `updated` (дата обновления в Business.ru)**
- Нужно для определения актуальной версии цены

## Необходимые изменения:

### 1. Добавить поля в GoodPrice:
```csharp
[Column("price_type_id"), NotNull]
public string PriceTypeId { get; set; } = string.Empty;

[Column("price_list_good_id"), NotNull]
public string PriceListGoodId { get; set; } = string.Empty;

[Column("businessru_updated_at"), Nullable]
public DateTimeOffset? BusinessRuUpdatedAt { get; set; }
```

### 2. Логика выбора актуальной цены:
```csharp
// Для каждого price_list_good_id берем цену с максимальной датой updated
var latestPrice = prices
    .GroupBy(p => p.PriceListGoodId)
    .Select(g => g.OrderByDescending(p => ParseDate(p.Updated)).First())
    .ToArray();
```

### 3. Алгоритм повышения цен:

```
FOREACH товар:
    1. Получить price_list_goods по good_id
    2. FOREACH price_list_good_id:
        a. Получить ВСЕ цены типа 75524
        b. Взять ПОСЛЕДНЮЮ по дате (max updated)
        c. Проверить в БД:
           - Если записи нет → создать, пометить для повышения
           - Если current_price != latest_price → обновить current_price
           - Если calculated_price == current_price AND is_processed → SKIP
        d. Вычислить новую цену: (price * 1.15), округлить до 50₽
        e. Сохранить calculated_price
        f. ОТЛОЖИТЬ обновление в Business.ru (batch)

ПОСЛЕ обработки всех товаров:
    - Batch update всех цен в Business.ru
    - Пометить is_processed = true
```

### 4. Формула повышения цены:

```csharp
public static decimal CalculateIncreasedPrice(decimal originalPrice)
{
    // +15%
    var increased = originalPrice * 1.15m;
    
    // Округление до 50 рублей
    var rounded = Math.Ceiling(increased / 50m) * 50m;
    
    return rounded;
}

// Примеры:
// 800₽ → 920₽ → 950₽
// 1000₽ → 1150₽ → 1150₽
```

## Готовность к следующему шагу:

✅ API работает корректно
✅ Получаем все нужные данные
✅ Понимаем структуру версионирования
❌ **Нужно добавить поля в GoodPrice**
❌ **Нужна миграция БД**
❌ **Нужна логика выбора последней цены**

## Рекомендации:

1. **Добавить поля в модель GoodPrice** (миграция)
2. **Создать метод для получения актуальных цен** (последние по дате)
3. **Реализовать логику защиты от повторного повышения**
4. **Тестировать на ОДНОМ товаре** перед batch обновлением
5. **Логировать ВСЕ изменения** для отката при необходимости

## Следующий шаг:

Создать миграцию для добавления полей в GoodPrice?
