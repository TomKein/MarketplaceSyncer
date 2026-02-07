# План боевого тестирования (Forward Flow)

## Цель
Проверить работоспособность прямого потока данных из Business.ru в базу данных MarketplaceSyncer по всем сущностям. Оценить объемы данных.

## Инструментарий
1.  **Консоль сервера (Logs)**: для наблюдения за ходом синхронизации.
2.  **PostgreSQL Client (pgAdmin/DBeaver)**: для проверки данных в БД.
3.  **Кабинет Business.ru**: для внесения тестовых изменений.

---

## Этап 1: Справочники и метаданные (Initial Sync)

**Что проверяем:** Загрузка базовых справочников, необходимых для работы товаров.

### Шаги:
1.  Запустить приложение с пустой базой (или очистить таблицы).
2.  Дождаться выполнения шагов `SyncGroupsAsync`, `SyncCountriesAsync`, `SyncCurrenciesAsync`, `SyncMeasuresAsync`, `SyncPriceTypesAsync`, `SyncStoresAsync`.
3.  Проверить логи на наличие ошибок и кол-во загруженных объектов.

### SQL для проверки и оценки объемов:
```sql
SELECT 'Groups' as Entity, count(*) FROM groups
UNION ALL
SELECT 'Countries', count(*) FROM countries
UNION ALL
SELECT 'Currencies', count(*) FROM currencies
UNION ALL
SELECT 'Measures', count(*) FROM measures
UNION ALL
SELECT 'PriceTypes', count(*) FROM price_types
UNION ALL
SELECT 'Stores', count(*) FROM stores;
```

**Критерии успеха:**
*   Таблицы не пусты.
*   Количество записей соответствует (примерно) данным в кабинете Business.ru.
*   Поля `LastSyncedAt` обновлены.

---

## Этап 2: Атрибуты (Attributes)

**Что проверяем:** Загрузка определений атрибутов и их возможных значений.

### Шаги:
1.  Дождаться выполнения `SyncAttributesAndValuesAsync`.

### SQL для проверки:
```sql
SELECT 'Attributes' as Entity, count(*) FROM attributes
UNION ALL
SELECT 'AttributeValues', count(*) FROM attribute_values;
```

**Критерии успеха:**
*   Связи `attribute_values` -> `attributes` корректны.

---

## Этап 3: Товары (Goods & Relations)

**Что проверяем:** Основная масса данных. Пагинация, маппинг полей, вложенные сущности (цены, остатки, атрибуты).

### Шаги:
1.  Дождаться начала загрузки товаров (`SyncGoodsGenericAsync`).
2.  Следить за логами: `Страница X/Y`. Проверить, что пагинация работает (обрабатываются все страницы).
3.  После завершения товаров — проверка загрузки `GoodsMeasures` (связи товаров и ед. измерения).

### SQL для проверки:
```sql
-- Общее количество товаров
SELECT count(*) as TotalGoods FROM goods;

-- Проверка связей с ценами
SELECT count(*) as TotalPrices FROM good_prices;

-- Проверка связей с остатками по складам
SELECT count(*) as TotalStoreStock FROM store_goods;

-- Проверка заполненности атрибутов товаров
SELECT count(*) as GoodAttributesLinks FROM good_attributes;

-- Проверка связей с доп. единицами измерения
SELECT count(*) as GoodMeasureLinks FROM goods_measures;
```

**Детальная проверка (выборочно для 1-2 товаров):**
Найдите ID любого товара в Business.ru и проверьте его данные в БД:
```sql
SELECT * FROM goods WHERE "Id" = <YOUR_GOOD_ID>;
SELECT * FROM good_prices WHERE "GoodId" = <YOUR_GOOD_ID>;
SELECT * FROM store_goods WHERE "GoodId" = <YOUR_GOOD_ID>;
```

**Критерии успеха:**
*   Количество товаров совпадает с `Total` в кабинете.
*   Цены разложены по типам цен (розничная, закупочная и т.д.).
*   Остатки разложены по складам.

---

## Этап 4: Изображения (Images)

**Что проверяем:** Скачивание бинарных данных, сортировка, хеширование.

### Шаги:
1.  Дождаться этапа `SyncImagesGenericAsync`.
2.  Это самый долгий этап. Можно прервать и проверить, что сохранения идут пачками.

### SQL для проверки:
```sql
SELECT count(*) FROM good_images;

-- Проверка размера самого большого изображения (в байтах)
SELECT max(length("Data")) FROM good_images;

-- Проверка дубликатов хешей (сколько картинок переиспользуется)
SELECT "Hash", count(*) as cnt FROM good_images GROUP BY "Hash" HAVING count(*) > 1 ORDER BY cnt DESC LIMIT 5;
```

**Критерии успеха:**
*   В поле `Data` есть байты (не NULL).
*   Поле `ContentType` заполнено (image/jpeg и т.д.).
*   Поле `Sort` соответствует порядку в карточке товара.

---

## Этап 5: Проверка обновлений (Updates / Delta Sync)

**Что проверяем:** Реакция системы на изменения в Business.ru.

### Сценарий теста:
1.  **Подготовка:** Выберите тестовый товар. Запишите его текущее имя, цену, остаток на складе.
2.  **Действие:** В кабинете Business.ru измените:
    *   Имя товара (напр. добавьте " TEST").
    *   Цену (измените рублевую цену).
    *   Остаток (проведите оприходование или списание на 1 шт).
    *   *(Опционально)* Измените порядок картинок или добавьте новую.
3.  **Ожидание:** Дождитесь срабатывания таймера инкрементальной синхронизации (1 мин) или перезапустите сервис.
4.  **Проверка:**

```sql
SELECT "Name", "Price", "InternalUpdatedAt" FROM goods WHERE "Id" = <TEST_ID>;
SELECT * FROM good_prices WHERE "GoodId" = <TEST_ID>;
SELECT * FROM store_goods WHERE "GoodId" = <TEST_ID>;
```

**Критерии успеха:**
*   Изменения появились в БД.
*   Поле `InternalUpdatedAt` обновилось.
*   Для картинок: добавилась новая запись или обновился `Sort`.

---

## Отчет о тестировании (Заполнить по факту)

| Сущность | Кол-во в БД | Время полной загрузки | Примечания/Ошибки |
|---|---|---|---|
| Groups | | | |
| Countries | | | |
| Currencies | | | |
| Measures | | | |
| Stores | | | |
| Attributes | | | |
| Goods | | | |
| Prices (Links) | | | |
| Stocks (Links) | | | |
| Images | | | |

