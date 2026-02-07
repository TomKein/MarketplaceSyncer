# Business.ru API: Товары
Справочник товаров и услуг (goods)

**Описание модели:** Cправочник товаров и услуг

**URL ресурса:** https://myaccount.business.ru/api/rest/goods.json

**Разрешенные запросы:** get(чтение), post(создание), put(изменение), delete(удаление)

**Специальные параметры модели:** 

**group_ids** - массив id групп товаров (модель groupsofgoods). При передаче в GET запросе group_ids в ответе будут возвращены товары в этих группах и во всех их дочерних группах.

**with_attributes** - при передаче в GET запросе with_attributes=1 в ответ будет добавлен массив атрибутов товара.

**with_barcodes** - при передаче в GET запросе with_barcodes =1 в ответ добавляется массив штрихкодов товара.

**with_remains** - при передаче в GET запросе with_remains=1 в ответ добавляется массив остатков товаров по складам, указанным в store_ids. Если store_ids не передается, то возвращается массив остатков товаров по всем складам. По каждому складу выводится общий остаток товара (total), а также зарезервированное на данном складе количество товара (reserved).

**store_ids** - массив id складов (модель stores), по которым следует возвратить остатки. Используется совместно с with_remains.

**filter_positive_remains** - при передаче в GET запросе filter_positive_remains =1 в ответе будут возвращены товары с положительным суммарным остатком по складам, указанным в store_ids. Если store_ids не передается, то будут возвращены товары с положительным суммарным остатком по всем складам. Используется совместно с with_remains.

**filter_positive_free_remains** - при передаче в GET запросе filter_positive_free_remains =1 в ответе будут возвращены товары с положительным суммарным свободным остатком по складам, указанным в store_ids. Если store_ids не передается, то будут возвращены товары с положительным суммарным свободным остатком по всем складам. Используется совместно с with_remains.

**with_prices** - при передаче в GET запросе with_prices =1 в ответ добавляется массив отпускных цен по типам, указанным в type_price_ids. Если type_price_ids не передается, то будут возвращены все отпускные цены.

**type_price_ids** - массив id типов цен (модель salepricetypes), по которым следует возвратить цены. Используется совместно с with_prices.

**with_modifications** - при передаче в GET запросе with_modifications =1 в ответ добавляется массив модификаций товара. При одновременном указании параметров with_attributes, with_barcodes, with_remains, with_prices, соответствующая информация будет возвращена для каждой модификации товара.

| Параметр | Тип | Обязателен в запросах | Внешний ключ к модели | Значение по умолчанию | Описание |
| --- | --- | --- | --- | --- | --- |
| id  | int | put, delete |     |     | Идентификатор |
| name | string | post |     |     | Наименование товара |
| full_name | string |     |     |     | Полное наименование товара, используемое в печатных документах |
| nds_id | int |     | [nds](https://api-online.class365.ru/api-polnoe/stavki_nds_nds/361) |     | Ссылка на НДС |
| group_id | int |     | [groupsofgoods](https://api-online.class365.ru/api-polnoe/derevo_grupp_groupsofgoods/290) |     | Ссылка на группу товаров |
| part | string |     |     |     | Артикул |
| store_code | string |     |     |     | Код товара на складе |
| type | int |     |     |     | Тип позиции (1-товар, 2-услуга, 3-комплект) |
| archive | bool |     |     |     | Флаг (0-товар не перемещен в архив, 1-товар перемещен в архив) |
| description | string |     |     |     | Описание товара |
| country_id | int |     | [countries](https://api-online.class365.ru/api-polnoe/strany_countries/355) |     | Ссылка на страну производителя |
| allow_serialnumbers | bool |     |     |     | Флаг (0-учет по серийным номерам запрещен, 1-учет по серийным номерам разрешен) |
| allow_serialnumbers_unique | bool |     |     |     | Флаг (0-не проверять серийные номера на уникальность, 1-проверять серийные номера на уникальность) |
| weight | float |     |     |     | Вес товара |
| volume | float |     |     |     | Объем товара |
| code | int |     |     |     | Идентификатор товара |
| store_box | string |     |     |     | Ячейка |
| store_section | int |     |     |     | Секция |
| remains_min | float |     |     |     | Минимально допустимый остаток товара на складе |
| partner_id | int |     | [partners](https://api-online.class365.ru/api-polnoe/spravochnik_kontragentov_partners/303) |     | Ссылка на поставщика |
| responsible_employee_id | int |     | [employees](https://api-online.class365.ru/api-polnoe/spisok_sotrudnikov_employees/343) |     | Ссылка на сотрудника, ответственного за товар |
| images | images |     |     |     | Изображения товара |
| feature | int |     |     |     | Особенность учёта: 1-весовой, 2-Алкоголь, 3-Пиво |
| weighing_plu | int |     |     |     | Идентификатор товара, под которым он числится в весах |
| departments_ids | int\\[\\] |     |     |     | Массив ссылок на отделы |
| cost | float |     |     |     | Себестоимость |
| measure_id | int |     | [measures](https://api-online.class365.ru/api-polnoe/edinitsy_izmereniya_measures/349) |     | Ссылка на основную единицу измерения товара |
| good_type_code | string |     |     |     | Код вида товара |
| payment_subject_sign | int |     |     |     | Признак предмета расчета (1-Товар, 2-Подакцизный товар, 3-Работа, 4-Услуга, 7-Лотерейный билет, 12-Составной предмет расчета, 13-Иной предмет расчета) |
| marking_type | int |     |     |     | Товарная группа: 1 - Табачная продукция, 2 - Товары из натурального меха, 3 - Обувные товары, 4 - Лекарственные препараты для медицинского применения, 5 - Молочная продукция, 6 - Медицинские изделия, 7 - Велосипеды и велосипедные рамы, 8 - Фотокамеры (кроме кинокамер), фотовспышки и лампы-вспышки, 9 - Шины и покрышки пневматические резиновые новые, 10 - Предметы одежды, белье постельное, столовое, туалетное и кухонное, 11 - Духи и туалетная вода, 12 - Средства индивидуальной защиты, 13 - Альтернативная табачная продукция, 14 - Никотиносодержащая продукция, 15 - Упакованная вода, 16 - Пиво, напитки, изготавливаемые на основе пива, слабоалкогольные напитки, 17 - Биологические активные добавки к пище, 19 - Антисептики и дезинфицирующие средства, 21 - Морепродукты, 22 - Безалкогольное пиво, 23 - Соковая продукция и безалкогольные напитки, 26 - Ветеринарные препараты, 27 - Игры и игрушки для детей, 28 - Радиоэлектронная продукция, 31 - Титановая металлопродукция, 32 - Консервированная продукция, 33 - Растительные масла, 34 - Оптоволокно и оптоволоконная продукция, 35 - Парфюмерные и косметические средства и бытовая химия, 36 - Печатная продукция, 37 - Бакалейная продукция, 39 - Строительные материалы, 40 - Пиротехнические изделия и средства обеспечения пожарной безопасности и пожаротушения, 41 - Отопительные приборы, 42 - Кабельно-проводниковая продукция, 43 - Моторные масла, |
| allow_marking | bool |     |     |     | Признак маркируемого товара. 1-Маркируемый товар, 0-Немаркируемый товар |
| taxation | int |     |     |     | Система налогообложения: 0 - "ОСН" , 1 - "УСН доход" , 2 - "УСН доход-расход" , 4 - "ЕСН" , 5 - "Патент", 7 - "Из розничной точки" |
| require_marking_for_sale | bool |     |     |     | Признак обязательности ввода кода маркировки при продаже. 1-Запрашивать КМ при продаже товара, 0-Не запрашивать КМ при продаже товара |
| length | float |     |     |     | Длина |
| width | float |     |     |     | Ширина |
| height | float |     |     |     | Высота |
| oversized | bool |     |     |     | Крупногабаритный |
| pack | int |     |     |     | Количество упаковок |
| hscode_id | string |     |     |     | КодТНВЭД |
| updated_remains_prices | datetime |     |     |     | Время последнего обновления, включая цены и остатки |
| allow_egais | bool |     |     |     | Учет в ЕГАИС |
| allow_weight | bool |     |     |     | Весовой товар |
| updated | datetime |     |     |     | Время последнего обновления |
| deleted | bool |     |     |     | Строка удалена (перемещена в корзину) |

## Response Structure
```json
{
  "status" : "ok",
  "result" : [ {
    "id" : "162695",
    "name" : "Абсорбер заднего бампера ЗАЗ Sens",
    "full_name" : "Абсорбер заднего бампера  ЗАЗ Sens",
    "nds_id" : 2,
    "group_id" : "762321",
    "part" : "96303225",
    "store_code" : null,
    "type" : "1",
    "archive" : false,
    "description" : "<p>Абсорбер заднего бампера ZAZ Sens / ЗАЗ Сенс</p><p>№ 96303225</p><p>GM</p><p>Б/У оригинал, в отличном состоянии!!</p>",
    "country_id" : null,
    "allow_serialnumbers" : false,
    "allow_serialnumbers_unique" : false,
    "weight" : "3",
    "volume" : null,
    "code" : "7388",
    "store_box" : null,
    "store_section" : null,
    "remains_min" : null,
    "partner_id" : null,
    "responsible_employee_id" : null,
    "feature" : null,
    "weighing_plu" : null,
    "cost" : "640.00",
    "measure_id" : "1",
    "good_type_code" : null,
    "payment_subject_sign" : 1,
    "marking_type" : null,
    "allow_marking" : false,
    "taxation" : 7,
    "require_marking_for_sale" : false,
    "length" : "150",
    "width" : "42",
    "height" : "30",
    "oversized" : false,
    "pack" : null,
    "hscode_id" : null,
    "updated_remains_prices" : "03.01.2026 22:45:21.586874",
    "allow_egais" : false,
    "allow_weight" : false,
    "agent_type" : null,
    "updated" : "28.06.2025 11:15:27",
    "deleted" : false,
    "departments_ids" : null,
    "images" : [ {
      "name" : "683781322_w1280_h1280_623418440_w1280_h1280_311___w640_h640_1408074752.jpg",
      "url" : "https://action_37041.business.ru/file/get/?id=335145",
      "sort" : 0,
      "id" : 335145
    }, {
      "name" : "683780884_w1280_h1280_623418440_w1280_h1280_311___w640_h640_1408074752.jpg",
      "url" : "https://action_37041.business.ru/file/get/?id=335148",
      "sort" : 1,
      "id" : 335148
    }, {
      "name" : "623418439_w1280_h1280_311378075_w640_h640_1408075862.jpg",
      "url" : "https://action_37041.business.ru/file/get/?id=335151",
      "sort" : 2,
      "id" : 335151
    }, {
      "name" : "623418438_w1280_h1280_311378067_w640_h640_1408074712.jpg",
      "url" : "https://action_37041.business.ru/file/get/?id=335154",
      "sort" : 3,
      "id" : 335154
    } ],
    "209334" : "http://baza.drom.ru/bulletin/44561306/edit",
    "209360" : "https://vk.com/market-23570129?w=product-23570129_13971272",
    "854879" : "",
    "854882" : "",
    "attributes" : [ {
      "id" : "2552216",
      "attribute" : {
        "id" : "2543011",
        "model" : "attributesforgoods",
        "name" : "Применимость",
        "sort" : 0
      },
      "value" : {
        "id" : "2551147",
        "model" : "attributesforgoodsvalues",
        "name" : "ЗАЗ, Sens, I (2004—2017)",
        "sort" : 2722883
      }
    }, {
      "id" : "2552235",
      "attribute" : {
        "id" : "75579",
        "model" : "attributesforgoods",
        "name" : "Производитель",
        "sort" : 77371
      },
      "value" : {
        "id" : "2303654",
        "model" : "attributesforgoodsvalues",
        "name" : "GENERAL MOTORS",
        "sort" : 2543192
      }
    } ],
    "remains" : [ {
      "store" : {
        "id" : "76726",
        "model" : "stores",
        "name" : "Масла (УСН)"
      },
      "amount" : {
        "total" : "0",
        "reserved" : "0",
        "remains_min" : null
      }
    }, {
      "store" : {
        "id" : "1204484",
        "model" : "stores",
        "name" : "Основной склад (ЕНВД)"
      },
      "amount" : {
        "total" : "1",
        "reserved" : "0",
        "remains_min" : null
      }
    } ],
    "prices" : [ {
      "price_type" : {
        "id" : "75524",
        "model" : "salepricetypes",
        "name" : "Розничная",
        "organization" : {
          "id" : "75519",
          "model" : "organizations",
          "name" : "РАДЧЕНКО И. Г."
        },
        "currency" : {
          "id" : "1",
          "model" : "currencies",
          "name" : "Российский рубль",
          "short_name" : "руб."
        }
      },
      "measure_id" : "1",
      "price" : "950"
    } ]
  } ],
  "token" : "lVBjmZ3iK9t5Zris0Qqx4rzdM2fPJ9vS",
  "request_count" : 3,
  "app_psw" : "9f60f89cf99edc709d7c0d41ace1a3e6"
}
```