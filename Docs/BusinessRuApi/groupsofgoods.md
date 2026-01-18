# Business.ru API: Дерево групп (groupsofgoods)

**Описание модели:** Дерево групп товаров и услуг (рубрикатор)  
**URL ресурса:** https://myaccount.business.ru/api/rest/groupsofgoods.json  
**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)  
**Специальные параметры модели:**   
**group_ids** - массив id групп товаров. При передаче в GET запросе group_ids в ответе будут возвращены ветви дерева групп, в пределах которых расположены заданные группы.

| Параметр | Тип      | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- |----------| --- | --- | --- | --- |
| id  | long     | put, delete |     |     | Идентификатор |
| name | string   | post |     |     | Наименование группы |
| parent_id | long     |     | [groupsofgoods](https://api-online.class365.ru/api-polnoe/derevo_grupp_groupsofgoods/290) |     | Ссылка на родительскую группу |
| images | images   |     |     |     | Изображение группы товаров |
| default_order | int      |     |     |     | Порядок сортировки по умолчанию |
| description | string   |     |     |     | Описание группы |
| updated | datetime |     |     |     | Время последнего обновления |
| deleted | bool     |     |     |     | Строка удалена (перемещена в корзину) |
|          |          |                       |                       |                       |          |

Пример ответа:
```json
{
    "status" : "ok",
    "result" : [ {
        "id" : "1",
        "name" : "Все товары и услуги",
        "parent_id" : null,
        "default_order" : "1",
        "description" : null,
        "updated" : "12.01.2016 08:17:01",
        "deleted" : false,
        "images" : [ ]
    } ],
    "token" : "qam2hjclqaVMsmjF6f1VLIKg2Svl0lcN",
    "request_count" : 7,
    "app_psw" : "b2140a291e323b05958badf17d87486d"
}
```