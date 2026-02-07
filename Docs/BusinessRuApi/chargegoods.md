# Business.ru API: Содержимое списаний

## Содержимое документов (chargegoods)

**Описание модели:** Cодержимое (товары) списаний товаров

**URL ресурса:** https://myaccount.business.ru/api/rest/chargegoods.json

**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id | long | delete, put | | | Идентификатор |
| charge_id | long | post | [charges](https://api-online.class365.ru/api-polnoe/spisok_dokumentov_charges/404) | | Ссылка на списание |
| good_id | long | post | [goods](https://api-online.class365.ru/api-polnoe/spravochnik_tovarov_i_uslug_goods/289) | | Ссылка на товар |
| amount | float | post | | | Количество |
| sum | float | | | | Cумма |
| price | float | | | | Цена |
| measure_id | long | | [measures](https://api-online.class365.ru/api-polnoe/edinitsy_izmereniya_measures/349) | | Ссылка на единицу измерения |
| modification_id | long | | [goodsmodifications](https://api-online.class365.ru/api-polnoe/privyazka_modifikatsij_goodsmodifications/301) | | Ссылка на модификацию товара |
| price_sale | float | | | | Цена продажи |
| marking_number | string[] | | | | Массив, содержащий коды маркировок |
| serial_number | string[] | | | | Массив серийных номеров |
| updated | datetime | | | | Время последнего обновления |
