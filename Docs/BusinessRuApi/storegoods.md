# Business.ru API: Остатки на складах

## Текущие остатки товаров на складах (storegoods)

**Описание модели:** Текущие остатки товаров на складах

**URL ресурса:** https://myaccount.business.ru/api/rest/storegoods.json

**Разрешенные запросы:** get(чтение)

Модель storegoods также отдает свободные остатки комплектов. Комплект в модели goods имеет признак type равный 3.

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id | long | | | | Идентификатор |
| store_id | long | | [stores](https://api-online.class365.ru/api-polnoe/spisok_skladov_stores/263) | | Ссылка на склад |
| good_id | long | | [goods](https://api-online.class365.ru/api-polnoe/spravochnik_tovarov_i_uslug_goods/289) | | Ссылка на товар |
| modification_id | long | | [goodsmodifications](https://api-online.class365.ru/api-polnoe/privyazka_modifikatsij_goodsmodifications/301) | | Ссылка на модификацию товара |
| amount | float | | | | Остаток товара на складе |
| reserved | float | | | | Зарезервировано товара на складе |
| remains_min | float | | | | Неснижаемый остаток |
| updated | datetime | | | | Время последнего обновления |
