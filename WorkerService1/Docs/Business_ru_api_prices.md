# Business.ru (Class365) API: Отпускные цены

Данный документ описывает работу с отпускными ценами товаров через модели `salepricetypes`, `salepricelists` и `salepricelistgoodprices`.

## 1. Модель: Типы отпускных цен (salepricetypes)
Используется для получения справочника типов цен (например, "Оптовая", "Розничная").

**Эндпоинт:** `salepricetypes.json`
**Методы:** GET, POST, PUT, DELETE

### Поля:
| Поле | Тип | Описание |
| :--- | :--- | :--- |
| `id` | string | ID типа цены |
| `name` | string | Наименование |
| `deleted` | bool | Признак удаления |

---

## 2. Модель: Прайс-листы (salepricelists)
Связующее звено между типом цены и списком товаров.

**Эндпоинт:** `salepricelists.json`
**Методы:** GET, POST, PUT, DELETE

### Поля:
| Поле | Тип | Описание |
| :--- | :--- | :--- |
| `id` | string | ID прайс-листа |
| `name` | string | Наименование |
| `price_type_id` | string | ID из `salepricetypes` |
| `active` | bool | Статус активности |

---

## 3. Модель: Цены товаров в прайс-листах (salepricelistgoodprices)
**Ключевая модель для изменения цен.** Связывает товар с конкретным прайс-листом и устанавливает цену.

**Эндпоинт:** `salepricelistgoodprices.json`
**Методы:** GET, POST, PUT, DELETE

### Поля:
| Поле | Тип | Описание |
| :--- | :--- | :--- |
| `id` | string | **Важно:** Уникальный ID записи (не ID товара) |
| `price_list_id` | string | ID прайс-листа |
| `price_list_good_id` | string | ID привязки товара к прайс-листу |
| `good_id` | string | ID товара (номенклатуры) |
| `price` | decimal | Цена (формат 0.00) |
| `currency_id` | string | ID валюты |

### Фильтрация:
- По ID товара в прайс-листе: `price_list_good_id={id}`
- По ID прайс-листа: `price_list_id={id}`

## Получение цен товара (два шага)

Чтобы получить цены конкретного товара, нужно выполнить два запроса:

### Шаг 1: Получить связи товара с прайс-листами
**Endpoint:** `GET /salepricelistgoods.json`
**Фильтр:** `good_id={id товара}`

Возвращает записи `SalePriceListGood` с ID связок товара с прайс-листами.

### Шаг 2: Получить цены по ID связок
**Endpoint:** `GET /salepricelistgoodprices.json`
**Фильтр:** `price_list_good_id={id из шага 1}`

Возвращает фактические цены для каждой связки.

### Пример процесса:
1. `GET /salepricelistgoods.json?good_id=12345` → получаем `[{id: "100"}, {id: "101"}]`
2. `GET /salepricelistgoodprices.json?price_list_good_id=100` → цены для связки 100
3. `GET /salepricelistgoodprices.json?price_list_good_id=101` → цены для связки 101

### В коде:
```csharp
// Автоматически выполняет оба шага
var prices = await client.GetGoodPricesAsync(goodId, limit: 10, ct);
```

### Специфика обновления (PUT):
Для изменения цены 80 000 товаров необходимо:
1. Выполнить `GET` к `salepricelistgoodprices` для получения `id` привязки и текущей `price`.
2. Выполнить `PUT` с передачей `id` и новой `price`.

---

## Фильтрация и поиск (Общие правила)
Применимо к `GET` запросам:
- `limit`: количество записей (макс. 250).
- `offset`: смещение для пагинации.
- `f[field_name]`: фильтр по полю (например, `f[price_list_id]=123`).
- `f[updated_at][from]`: фильтр по дате изменения.

## Особенности работы
1. **Bulk-запросы:** Пакетное обновление (массивом) не поддерживается. Каждый товар обновляется отдельным `PUT` запросом.
2. **Округление:** API принимает до 2 знаков после запятой.
3. **Лимиты:** Параметр `request_count` в ответе указывает на текущую нагрузку. Рекомендуется пауза `request_count * 3` мс между запросами.