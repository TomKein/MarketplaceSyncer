# Используемые Endpoints Business.ru (Legacy)

Анализ файла `Legacy\Selen\Base\Class365API.cs`.

## Справочники и настройки
| Endpoint | Метод | Описание |
| :--- | :--- | :--- |
| `customerorderstatus` | GET | Статусы заказов |
| `requestsource` | GET | Источники заказов |
| `attributesforgoods` | GET | Атрибуты товаров |
| `salepricetypes` | GET | Типы цен продажи |
| `buypricetypes` | GET | Типы цен закупки |
| `countries` | GET | Страны |
| `repair.json` | GET | Обновление токена (autorization repair) |
| `groupsofgoods` | GET | Группы товаров |

## Товары (Goods)
| Endpoint | Метод | Описание |
| :--- | :--- | :--- |
| `goods` | GET | Получение списка товаров. <br>Параметры: `archive`, `type=1`, `updated[from]`, `updated_remains_prices[from]`, `with_remains`, `with_prices` |
| `goods` | PUT | Обновление товара: <br>- Переименование (`name`) <br>- Артикул (`part`) <br>- Код склада (`store_code`) <br>- Смена группы (`group_id`) <br>- Описание (`description`) <br>- Архивация (`archive=0/1`) <br>- Удаление фото (`images="[]"`) <br>- Габариты (`width`, `height`, `length`) <br>- Очистка ссылок на маркетплейсы (поля 209334, 209360 и т.д.) |

## Атрибуты товаров
| Endpoint | Метод | Описание |
| :--- | :--- | :--- |
| `goodsattributes` | GET | Получение значений атрибутов конкретного товара |
| `goodsattributes` | DELETE | Удаление значения атрибута |

## Заказы и Резервы
| Endpoint | Метод | Описание |
| :--- | :--- | :--- |
| `customerorders` | GET | Поиск заказа по `request_source_id` и `comment` |
| `customerorders` | POST | Создание заказа покупателя (статус "Маркетплейсы") |
| `customerordergoods` | POST | Добавление позиций (товаров) в заказ покупателя |
| `reservations` | GET | Получение резервов (фильтр по `updated[from]`, `responsible_employee_id`) |
| `reservations` | POST | Создание резерва на основании заказа (`customer_order_id`) |
| `reservations` | PUT | Обновление резерва (смена `responsible_employee_id`, изменение `comment`) |

## Реализации (Отгрузки)
| Endpoint | Метод | Описание |
| :--- | :--- | :--- |
| `realizations` | GET | Получение реализаций (фильтр по `updated[from]`, `reservation_id`) |
| `realizations` | PUT | Обновление реализации. Основное использование: снятие проведения (`held=0`) и проведение (`held=1`) для разблокировки редактирования связанных сущностей. |
| `realizationgoods` | GET | Получение товаров в реализациях (для аналитики/очистки фото при продажах) |
| `realizationgoods` | PUT | Изменение позиций в реализации (коррекция цены `price` и суммы `sum`). **Внимание**: используется формула `0.75 * busPrice` для маркетплейсов. |

## Цены и Документы
| Endpoint | Метод | Описание |
| :--- | :--- | :--- |
| `salepricelistgoodprices` | GET/PUT | Получение и обновление цен в прайс-листах продаж. Используется хак с изменением цены на 1 копейку для триггера обновления. |
| `buypricelistgoodprices` | GET/PUT | Цены закупок (аналогично продажам). |
| `remaingoods` | GET | Получение товаров из документов ввода начальных остатков. |
| `remaingoods` | PUT | Изменение цены закупки в документах ввода остатков. |
| `postinggoods` | GET | Получение товаров из оприходований. |
| `postinggoods` | PUT | Изменение цены закупки и суммы в оприходованиях. |

## Контрагенты
| Endpoint | Метод | Описание |
| :--- | :--- | :--- |
| `partnercontactinfo` | GET | Поиск контактной информации (телефонов) для выявления дублей контрагентов. |

