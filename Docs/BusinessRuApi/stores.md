# Business.ru API: Склады

## Список складов (stores)

**Описание модели:** Cписок складов

**URL ресурса:** https://myaccount.business.ru/api/rest/stores.json

**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id | long | delete, put | | | Идентификатор |
| name | string | post | | | Название склада |
| responsible_employee_id | long | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) | | Ссылка на ответственного сотрудника |
| debit_type | long | | | | Флаг (1-Списание по FIFO, 2-Списание по среднему) |
| deny_negative_balance | bool | | | | Флаг (0-Разрешить продажу с возникновением отрицательных остатков, 1-Запретить продажу с возникновением отрицательных остатков) |
| updated | datetime | | | | Время последнего обновления |
| deleted | bool | | | | Строка удалена (перемещена в корзину) |