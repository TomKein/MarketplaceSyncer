# Business.ru API: Комментарии
Работа с комментариями к сущностям (товары, заказы и т.д.) через универсальный эндпоинт `comments`.  
**URL ресурса:** https://myaccount.business.ru/api/rest/comments.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | int | delete, put |     |     | Идентификатор |
| model\_name | string | post, put |     |     | Название модели |
| time\_create | datetime |     |     |     | Дата и время создания |
| document\_id | int | post, put |     |     | Идентификатор документа |
| employee\_id | int | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на автора комментария |
| note | string | post |     |     | Комментарий |
| updated | datetime |     |     |     | Время последнего обновления |

## Особенности реализации
1.  **Универсальность**: Эндпоинт один для всех моделей, различается параметром `model_name`.
2.  **Параметры**:
    *   `model_name`: Имя модели (например, `"goods"` для товаров). **Обязательный параметр**.
    *   `document_id`: ID сущности, к которой привязан комментарий (вместо `model_id` или `good_id`).
    *   `employee_id`: ID сотрудника-автора (в запросах `GET` и `POST/PUT`). Обратите внимание, что в некоторых старых документациях может упоминаться как `author_employee_id`, но API требует `employee_id`.
3.  **Поля модели**:
    *   `note`: Текст комментария (вместо `content`).
    *   `time_create`: Дата создания (вместо `date`).
    *   `updated`: Дата обновления.
4.  **Ответы API**:
    *   **GET** (чтение): Возвращает массив объектов в поле `result`.
    *   **POST/PUT/DELETE** (изменения): Возвращает объект с полями `status` и `id` (для POST/PUT) **на корневом уровне**, без вложенного `result`.
        *   Пример успеха: `{"status": "ok", "id": 12345, "request_count": ...}`
        *   Пример ошибки: `{"status": "error", "error_code": "common:1", "error_text": "...", ...}`

## Пример JSON-объекта (Response)
```json
{
  "id": 12345,
  "model_name": "goods",
  "document_id": 162695,
  "employee_id": 10,
  "time_create": "27.10.2023 14:00:00",
  "updated": "27.10.2023 15:30:00",
  "note": "Текст комментария"
}
```

## Нюансы клиента
Для методов изменения (`Create`, `Update`, `Delete`) используется специальный метод инфраструктуры `RequestActionAsync`, который ожидает ответ с `status` и `id` в корне, а не в `result`. Это позволяет избежать ошибки `Result is null`.