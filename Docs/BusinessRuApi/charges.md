# Business.ru API: Списания

## Список документов (charges)

**Описание модели:** Cписок документов "Cписание товаров"

**URL ресурса:** https://myaccount.business.ru/api/rest/charges.json

**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

**Разрешенные дополнительные параметры:** with_goods, get_templates, get_template_link

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id | long | delete, put | | | Идентификатор |
| date | datetime | | | | Дата и время документа |
| number | string | | | | Номер документа |
| organization_id | long | post | [organizations](https://api-online.class365.ru/api-polnoe/spisok_organizatsij_organizations/342) | | Ссылка на организацию |
| store_id | long | post | [stores](https://api-online.class365.ru/api-polnoe/spisok_skladov_stores/263) | | Ссылка на склад |
| author_employee_id | long | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) | | Ссылка на сотрудника, автора документа |
| responsible_employee_id | long | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) | | Ссылка на сотрудника, ответственного за документ |
| inventory_id | long | | [inventories](https://api-online.class365.ru/api-polnoe/spisok_dokumentov_inventories/401) | | Ссылка на инвентаризацию |
| held | bool | | | | Флаг (0-документ не проведен, 1-документ проведен) |
| sum | float | | | | Сумма |
| comment | string | | | | Примечание |
| reason | string | | | | Причина списания |
| departments_ids | long[] | | | | Массив ссылок на отделы |
| goods | ObjectList | | | | Позиции |
| updated | datetime | | | | Время последнего обновления |
| deleted | bool | | | | Строка удалена (перемещена в корзину) |
