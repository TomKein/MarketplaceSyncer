# Business.ru API: Атрибуты (характеристики) товаров
Данный подраздел содержит модели данных, обеспечивающие внешним приложениям доступ к информации об атрибутах товаров и услуг.

## Атрибуты (attributesforgoods)
**Описание модели:** Список атрибутов товаров и услуг  
**URL ресурса:** https://myaccount.business.ru/api/rest/attributesforgoods.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)  

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | put, delete |     |     | Идентификатор |
| name | string | post |     |     | Наименование атрибута |
| selectable | bool | post |     |     | Флаг, определяющий имеет ли атрибут предопределенный набор значений (1-имеет, 0-не имеет) |
| archive | bool |     |     |     | Флаг (0-элемент не перемещен в архив, 1-элемент перемещен в архив) |
| description | string |     |     |     | Описание |
| sort | long |     |     |     | Сортировка |
| updated | datetime |     |     |     | Время последнего обновления |
| deleted | bool |     |     |     | Строка удалена (перемещена в корзину) |

## Значения атрибутов (attributesforgoodsvalues)
**Описание модели:** Список значений атрибутов товаров и услуг  
**URL ресурса:** https://myaccount.business.ru/api/rest/attributesforgoodsvalues.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)  

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | put, delete |     |     | Идентификатор |
| attribute_id | long | post | [attributesforgoods](https://api-online.class365.ru/api-polnoe/atributy_attributesforgoods/294) |     | Ссылка на атрибут |
| name | string | post |     |     | Значение |
| sort | long |     |     |     | Сортировка |
| updated | datetime |     |     |     | Время последнего обновления |

## Привязка атрибутов (goodsattributes)
**Описание модели:** Привязка атрибутов к товарам  
**URL ресурса:** https://myaccount.business.ru/api/rest/goodsattributes.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)  

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | put, delete |     |     | Идентификатор |
| good_id | long | post | [goods](https://api-online.class365.ru/api-polnoe/spravochnik_tovarov_i_uslug_goods/289) |     | Ссылка на товар |
| attribute_id | long | post | [attributesforgoods](https://api-online.class365.ru/api-polnoe/atributy_attributesforgoods/294) |     | Ссылка на атрибут |
| value_id | long |     | [attributesforgoodsvalues](https://api-online.class365.ru/api-polnoe/znacheniya_atributov_attributesforgoodsvalues/295) |     | Ссылка на значение атрибута |
| value | string |     |     |     | Значение атрибута |
| updated | datetime |     |     |     | Время последнего |
