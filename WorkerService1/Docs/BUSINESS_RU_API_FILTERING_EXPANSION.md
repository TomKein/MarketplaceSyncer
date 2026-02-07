# Business.ru (Class365) API: Фильтрация и Обогащение Данных

## Обзор
Документ описывает систему фильтрации запросов и обогащения ответов для API Business.ru.
Применимо ко всем GET-запросам моделей через эндпоинт: `https://online.business.ru/api/rest/{model}.json`

---

## 1. Фильтрация Данных (filters / f)

### 1.1 Базовый синтаксис
```
filters[field_name]=value          # Простое равенство
f[field_name]=value                # Краткая форма (работает аналогично)
```

### 1.2 Операторы сравнения

| Оператор | Синтаксис | Описание | Пример |
|----------|-----------|----------|---------|
| `=` | `filters[field]=value` | Равно | `filters[id]=12345` |
| `!=` | `filters[field][!=]=value` | Не равно | `filters[archive][!=]=1` |
| `>` | `filters[field][>]=value` | Больше | `filters[price][>]=1000` |
| `>=` | `filters[field][>=]=value` | Больше или равно | `filters[date][>=]=2024-01-01` |
| `<` | `filters[field][<]=value` | Меньше | `filters[quantity][<]=10` |
| `<=` | `filters[field][<=]=value` | Меньше или равно | `filters[date][<=]=2024-12-31` |
| `in` | `filters[field][in]=val1,val2,val3` | Входит в список | `filters[id][in]=123,456,789` |
| `like` | `filters[field][like]=%text%` | SQL LIKE поиск | `filters[name][like]=%iPhone%` |

### 1.3 Логические операции

#### OR (логическое ИЛИ)
```
filters[OR][0][field1]=value1&filters[OR][0][field2]=value2
```
**Пример:** Найти товары с ценой > 1000 ИЛИ количеством < 5
```
filters[OR][0][price][>]=1000&filters[OR][0][quantity][<]=5
```

#### AND (логическое И)
По умолчанию все фильтры объединяются через AND:
```
filters[archive]=0&filters[deleted]=0
```
Эквивалентно: `WHERE archive=0 AND deleted=0`

### 1.4 Фильтрация по пользовательским полям (add_fields)

Пользовательские поля имеют числовые ID:
```
filters[add_field_123]=значение
```

**Пример:** Фильтр по пользовательскому полю "Артикул поставщика" (ID=456):
```
filters[add_field_456]=ART-2024-001
```

### 1.5 Фильтрация по датам

**Формат даты:** `YYYY-MM-DD` или `YYYY-MM-DD HH:MM:SS`

**Диапазон дат:**
```
filters[created_at][>=]=2024-01-01&filters[created_at][<=]=2024-12-31
```

**Фильтр по времени обновления:**
```
filters[updated_at][>=]=2024-12-01 00:00:00
```

### 1.6 Частые паттерны фильтрации

#### Только активные записи (не архивные, не удаленные)
```
filters[archive]=0&filters[deleted]=0
```

#### Поиск по части текста
```
filters[name][like]=%Телефон%
```

#### Записи, измененные за последние 24 часа
```
filters[updated_at][>=]=2024-12-30 00:00:00
```

---

## 2. Обогащение Данных (with)

### 2.1 Базовая концепция
По умолчанию API возвращает только ID связанных сущностей. Параметр `with` позволяет **загрузить полные объекты** вместо ID.

**Без обогащения:**
```json
{
  "id": "123",
  "name": "iPhone 15",
  "organization_id": "456"
}
```

**С обогащением (`with[organization]`):**
```json
{
  "id": "123",
  "name": "iPhone 15",
  "organization": {
    "id": "456",
    "name": "ООО Рога и Копыта",
    "inn": "1234567890"
  }
}
```

### 2.2 Синтаксис обогащения

#### Одна связь
```
with[relation_name]
```

**Пример:** Загрузить товары с данными организации
```
GET /goods.json?with[organization]
```

#### Множественные связи
```
with[relation1]&with[relation2]&with[relation3]
```

**Пример:** Товары с организацией, складом и поставщиком
```
GET /goods.json?with[organization]&with[warehouse]&with[counterparty]
```

### 2.3 Вложенное обогащение (Deep Expansion)

Можно загружать связи связанных объектов:

```
with[relation1][with][relation2]
```

**Пример:** Заказы → Товары → Номенклатура
```
GET /customer_orders.json?with[items][with][good]
```

**Результат:**
```json
{
  "id": "789",
  "number": "ORD-2024-001",
  "items": [
    {
      "id": "111",
      "quantity": 2,
      "good": {
        "id": "222",
        "name": "iPhone 15",
        "article": "APPLE-IP15"
      }
    }
  ]
}
```

### 2.4 Фильтрация связанных данных

Можно фильтровать данные в связанных моделях:

```
with[relation][filters][field]=value
```

**Пример:** Загрузить заказы только с неудаленными товарами
```
GET /customer_orders.json?with[items][filters][deleted]=0
```

### 2.5 Частые связи (relations) по моделям

#### Товары (goods)
- `organization` — Организация
- `warehouse` — Склад
- `counterparty` — Контрагент (поставщик)
- `unit` — Единица измерения
- `currency` — Валюта
- `good_group` — Группа товаров

#### Заказы покупателей (customer_orders)
- `organization` — Организация
- `counterparty` — Контрагент (покупатель)
- `items` — Позиции заказа
- `employee` — Ответственный сотрудник
- `warehouse` — Склад

#### Цены (salepricelistgoodprices)
- `price_list` — Прайс-лист
- `good` — Товар
- `currency` — Валюта

### 2.6 Пользовательские поля (add_fields)

Для загрузки всех пользовательских полей:
```
with_add_fields=1
```

**Результат:**
```json
{
  "id": "123",
  "name": "iPhone 15",
  "add_fields": {
    "456": "ART-2024-001",
    "789": "Новинка"
  }
}
```

---

## 3. Управление Выводом

### 3.1 Выбор полей (fields)

Для уменьшения размера ответа можно запросить только нужные поля:

```
fields=id,name,price
```

**Пример:**
```
GET /goods.json?fields=id,name,article&limit=10
```

**Результат:**
```json
{
  "status": "ok",
  "result": [
    {"id": "123", "name": "iPhone 15", "article": "APPLE-IP15"},
    {"id": "124", "name": "iPhone 14", "article": "APPLE-IP14"}
  ]
}
```

### 3.2 Подсчет записей (request_count)

Для получения общего количества записей без загрузки данных:

```
request_count=1
```

**Результат:**
```json
{
  "status": "ok",
  "result": [],
  "total_count": 15324
}
```

---

## 4. Пагинация и Сортировка

### 4.1 Пагинация

| Параметр | Описание | Значение по умолчанию | Максимум |
|----------|----------|----------------------|----------|
| `limit` | Количество записей | 100 | 1000 |
| `offset` | Пропустить записей | 0 | - |

**Пример:** Получить записи с 101 по 200
```
GET /goods.json?limit=100&offset=100
```

### 4.2 Сортировка

```
sort=field_name         # По возрастанию
sort=-field_name        # По убыванию
```

**Примеры:**
```
sort=name               # Сортировка по имени (A-Z)
sort=-price             # Сортировка по цене (от большей к меньшей)
sort=-created_at        # Сначала новые записи
```

**Множественная сортировка:**
```
sort=-created_at,name   # Сначала по дате (убыв), затем по имени (возр)
```

---

## 5. Комплексные Примеры для LLM

### Пример 1: Получить активные товары с ценами
```
GET /goods.json?
  filters[archive]=0&
  filters[deleted]=0&
  with[organization]&
  with_add_fields=1&
  limit=100&
  sort=-created_at
```

### Пример 2: Заказы за декабрь с позициями и товарами
```
GET /customer_orders.json?
  filters[date][>=]=2024-12-01&
  filters[date][<=]=2024-12-31&
  with[items][with][good]&
  with[counterparty]&
  sort=-date
```

### Пример 3: Цены для конкретного прайс-листа
```
GET /salepricelistgoodprices.json?
  filters[price_list_id]=12345&
  filters[price][>]=0&
  with[good]&
  fields=id,good_id,price&
  limit=1000
```

### Пример 4: Поиск товаров по артикулу (частичное совпадение)
```
GET /goods.json?
  filters[article][like]=%APPLE%&
  filters[archive]=0&
  with[organization]&
  sort=article
```

### Пример 5: Товары с низким остатком ИЛИ высокой ценой
```
GET /goods.json?
  filters[OR][0][quantity][<]=5&
  filters[OR][0][price][>]=10000&
  filters[deleted]=0&
  with[warehouse]&
  sort=-price
```

---

## 6. Важные Замечания

### 6.1 Производительность
- **Лимит запросов:** API имеет rate limiting (~500 запросов за 5 минут)
- **Оптимизация:** Используйте `fields` для уменьшения размера ответа
- **Батчинг:** Для массовых операций используйте `limit=1000` и пагинацию

### 6.2 Кодирование
- Все параметры должны быть URL-encoded
- Hex-коды в верхнем регистре (`%2A`, не `%2a`)
- Строки в UTF-8

### 6.3 Типичные ошибки
- ❌ `with=organization` → ✅ `with[organization]`
- ❌ `filters.price>1000` → ✅ `filters[price][>]=1000`
- ❌ Забыли `filters[deleted]=0` → Получили удаленные записи
- ❌ `limit=5000` → Максимум 1000, используйте пагинацию

---

## 7. Референсы

- **Официальная документация:** https://api-online.class365.ru/api-polnoe/get-zapros/370
- **Авторизация и подписи:** См. `BUSINESS_RU_API_REQUIREMENTS.md`
- **Работа с ценами:** См. `BUSINESS_RU_API_PRICES.md`
- **Базовые запросы:** См. `BUSINESS_RU_API_COMMON_QUERIES.md`

---

**Последнее обновление:** 2024-12-31  
**Версия:** 1.0
