# Business.ru API: Изображения товара (goodsimages)
**Описание модели:** Изображение товары  
**URL ресурса:** https://myaccount.business.ru/api/rest/goodsimages.json  
**Разрешенные запросы:** get(чтение), post(создание), delete(удаление)

| Параметр | Тип      | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- |----------| --- | --- | --- | --- |
| id  | long     | delete |     |     | Идентификатор |
| good_id | long      | post | [goods](https://api-online.class365.ru/api-polnoe/spravochnik_tovarov_i_uslug_goods/289) |     | Ссылка на товар |
| name | string   | post |     |     | Имя файла с расширением |
| url | string   | post |     |     | URL для скачивания файла |
| sort | int      |     |     |     | Сортировка |
| time_create | datetime |     |     |     | Дата создания |