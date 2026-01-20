# Business.ru API: Цены
## Отпускные цены
### Типы цен (salepricetypes)  
**Описание модели:** Типы цен (колонки прайс-листа) продажи  
**URL ресурса:** https://myaccount.business.ru/api/rest/salepricetypes.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| name | string | post |     |     | Наименование типа цены |
| responsible_employee_id | long | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на ответственного сотрудника |
| organization_id | long | post | [organizations](https://api-online.class365.ru/api-polnoe/spisok_organizatsij_organizations/342) |     | Ссылка на организацию |
| currency_id | long |     | [currencies](https://api-online.class365.ru/api-polnoe/valyuty_currencies/350) |     | Ссылка на валюту. Если не передается в POST запросе, то по умолчанию подставляется валюта учета. |
| owner_employee_id | long |     | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на сотрудника - владельца |
| departments_ids | long\\[\\] |     |     |     | Массив ссылок на отделы |
| archive | bool |     |     |     | Флаг (0-элемент не перемещен в архив, 1-элемент перемещен в архив) |
| updated | datetime |     |     |     | Время последнего обновления |
| deleted | bool |     |     |     | Строка удалена (перемещена в корзину) |

### Список назначений цен (salepricelists)  
**Описание модели:** Список документов "Назначение отпускных цен"  
**URL ресурса:** https://myaccount.business.ru/api/rest/salepricelists.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| number | string |     |     |     | Номер документа |
| date | datetime |     |     |     | Дата документа |
| responsible_employee_id | long | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на сотрудника, ответственного за документ |
| organization_id | long | post | [organizations](https://api-online.class365.ru/api-polnoe/spisok_organizatsij_organizations/342) |     | Ссылка на организацию, оформившей документ |
| owner_employee_id | long |     | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на сотрудника - владельца |
| departments_ids | long\\[\\] |     |     |     | Массив ссылок на отделы |
| deleted | bool |     |     |     | Строка удалена (перемещена в корзину) |
| parent_price_list_id | long |     | [salepricelists](https://api-online.class365.ru/api-polnoe/spisok_naznachenij_tsen_salepricelists/331) |     | Идентификатор родительского документа назначения отпускных цен |
| goods | ObjectList |     |     |     | Позиции |
| updated | datetime |     |     |     | Время последнего обновления |

### Привязка типов цен (salepricelistspricetypes)  
**Описание модели:** Привязка типов цен продажи к назначениям отпускных цен  
**URL ресурса:** https://myaccount.business.ru/api/rest/salepricelistspricetypes.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)  

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| price_list_id | long | post | [salepricelists](https://api-online.class365.ru/api-polnoe/spisok_naznachenij_tsen_salepricelists/331) |     | Ссылка на прайс-лист |
| price_type_id | long | post | [salepricetypes](https://api-online.class365.ru/api-polnoe/tipy_tsen_salepricetypes/330) |     | Ссылка на тип цены |
| updated | datetime |     |     |     | Время последнего обновления |

### Содержимое назначений (salepricelistgoods)  
**Описание модели:** Содержимое (товары и услуги) назначений отпускных цен  
**URL ресурса:** https://myaccount.business.ru/api/rest/salepricelistgoods.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| price_list_id | long | post | [salepricelists](https://api-online.class365.ru/api-polnoe/spisok_naznachenij_tsen_salepricelists/331) |     | Ссылка на прайс-лист |
| good_id | long | post | [goods](https://api-online.class365.ru/api-polnoe/spravochnik_tovarov_i_uslug_goods/289) |     | Ссылка на товар |
| measure_id | long |     | [measures](https://api-online.class365.ru/api-polnoe/edinitsy_izmereniya_measures/349) |     | Ссылка на единицу измерения |
| modification_id | long |     | [goodsmodifications](https://api-online.class365.ru/api-polnoe/privyazka_modifikatsij_goodsmodifications/301) |     | Ссылка на модификацию товара |
| updated | datetime |     |     |     | Время последнего обновления |

### Привязка цен (salepricelistgoodprices)  
**Описание модели:** Привязка цен к товарам в назначениях отпускных цен  
**URL ресурса:** https://myaccount.business.ru/api/rest/salepricelistgoodprices.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| price_list_good_id | long | post | [salepricelistgoods](https://api-online.class365.ru/api-polnoe/soderzhimoe_naznachenij_salepricelistgoods/333) |     | Ссылка на товар в прайс-листе |
| price_type_id | long | post | [salepricetypes](https://api-online.class365.ru/api-polnoe/tipy_tsen_salepricetypes/330) |     | Ссылка на тип цены |
| price | float | post |     |     | Цена |
| updated | datetime |     |     |     | Время последнего обновления |

## Закупочные цены
### Типы цен (buypricetypes)  
**Описание модели:** Типы цен (колонки прайс-листа) закупки  
**URL ресурса:** https://myaccount.business.ru/api/rest/buypricetypes.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| name | string | post |     |     | Наименование типа цены |
| responsible_employee_id | long | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на ответственного сотрудника |
| organization_id | long | post | [organizations](https://api-online.class365.ru/api-polnoe/spisok_organizatsij_organizations/342) |     | Ссылка на организацию |
| currency_id | long |     | [currencies](https://api-online.class365.ru/api-polnoe/valyuty_currencies/350) |     | Ссылка на валюту. Если не передается в POST запросе, то по умолчанию подставляется валюта учета. |
| owner_employee_id | long |     | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на сотрудника - владельца |
| departments_ids | long\\[\\] |     |     |     | Массив ссылок на отделы |
| updated | datetime |     |     |     | Время последнего обновления |
| deleted | bool |     |     |     | Строка удалена (перемещена в корзину) |

### Список назначений цен (buypricelists)  
**Описание модели:** Список документов "Назначение закупочных цен"  
**URL ресурса:** https://myaccount.business.ru/api/rest/buypricelists.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| number | string |     |     |     | Номер документа |
| date | datetime |     |     |     | Дата документа |
| responsible_employee_id | long | post | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на сотрудника, ответственного за документ |
| organization_id | long | post | [organizations](https://api-online.class365.ru/api-polnoe/spisok_organizatsij_organizations/342) |     | Ссылка на организацию, оформившей документ |
| owner_employee_id | long |     | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на сотрудника - владельца |
| departments_ids | long\\[\\] |     |     |     | Массив ссылок на отделы |
| deleted | bool |     |     |     | Строка удалена (перемещена в корзину) |
| partner_id | long |     | [partners](https://api-online.class365.ru/api-polnoe/spravochnik_kontragentov_partners/303) |     | Ссылка на поставщика |
| goods | ObjectList |     |     |     | Позиции |
| updated | datetime |     |     |     | Время последнего обновления |

### Привязка типов цен (buypricelistspricetypes)  
**Описание модели:** Привязка типов цен закупки к назначениям закупочных цен  
**URL ресурса:** https://myaccount.business.ru/api/rest/buypricelistspricetypes.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)  

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| price_list_id | long | post | [buypricelists](https://api-online.class365.ru/api-polnoe/spisok_naznachenij_tsen_buypricelists/337) |     | Ссылка на прайс-лист |
| price_type_id | long | post | [buypricetypes](https://api-online.class365.ru/api-polnoe/tipy_tsen_buypricetypes/336) |     | Ссылка на тип цены |
| updated | datetime |     |     |     | Время последнего обновления |

### Содержимое назначений (buypricelistgoods)  
**Описание модели:** Содержимое (товары и услуги) назначений закупочных цен  
**URL ресурса:** https://myaccount.business.ru/api/rest/buypricelistgoods.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| price_list_id | long | post | [buypricelists](https://api-online.class365.ru/api-polnoe/spisok_naznachenij_tsen_buypricelists/337) |     | Ссылка на прайс-лист |
| good_id | long | post | [goods](https://api-online.class365.ru/api-polnoe/spravochnik_tovarov_i_uslug_goods/289) |     | Ссылка на товар |
| measure_id | long |     | [measures](https://api-online.class365.ru/api-polnoe/edinitsy_izmereniya_measures/349) |     | Ссылка на единицу измерения |
| modification_id | long |     | [goodsmodifications](https://api-online.class365.ru/api-polnoe/privyazka_modifikatsij_goodsmodifications/301) |     | Ссылка на модификацию товара |
| updated | datetime |     |     |     | Время последнего обновления |

### Привязка цен (buypricelistgoodprices)  
**Описание модели:** Привязка цен к товарам в назначениях закупочных цен  
**URL ресурса:** https://myaccount.business.ru/api/rest/buypricelistgoodprices.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long | delete, put |     |     | Идентификатор |
| price_list_good_id | long | post | [buypricelistgoods](https://api-online.class365.ru/api-polnoe/soderzhimoe_naznachenij_buypricelistgoods/339) |     | Ссылка на товар в прайс-листе |
| price_type_id | long | post | [buypricetypes](https://api-online.class365.ru/api-polnoe/tipy_tsen_buypricetypes/336) |     | Ссылка на тип цены |
| price | float | post |     |     | Цена |
| updated | datetime |     |     |     | Время последнего обновления |

## Текущие цены (currentprices)
**Описание модели:** Текущие цены  
**URL ресурса:** https://myaccount.business.ru/api/rest/currentprices.json  
**Разрешенные запросы:** get(чтение)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | long |     |     |     | Идентификатор |
| good_id | long |     | [goods](https://api-online.class365.ru/api-polnoe/spravochnik_tovarov_i_uslug_goods/289) |     | Ссылка на товар |
| modification_id | long |     | [goodsmodifications](https://api-online.class365.ru/api-polnoe/privyazka_modifikatsij_goodsmodifications/301) |     | Ссылка на модификацию товара |
| measure_id | long |     | [measures](https://api-online.class365.ru/api-polnoe/edinitsy_izmereniya_measures/349) |     | Ссылка на единицу измерения |
| price_type_id | long |     | [buypricetypes](https://api-online.class365.ru/api-polnoe/tipy_tsen_buypricetypes/336) [salepricetypes](https://api-online.class365.ru/api-polnoe/tipy_tsen_salepricetypes/330) |     | Ссылка на тип цены |
| price | float |     |     |     | Текущая цена |
| is_base_measure | bool |     |     |     | Флаг (0 - не базовая единица измерения, 1 - базовая единица измерения) |
| updated | datetime |     |     |     | Время последнего обновления |
