# Business.ru API - Goods (Товары)

## Endpoint
`GET https://myaccount.business.ru/api/rest/goods.json`

## Parameters
*   `group_ids`: Array of group IDs.
*   `with_attributes`: `1` to include attributes.
*   `with_barcodes`: `1` to include barcodes.
*   `with_remains`: `1` to include remains.
    *   `store_ids`: Array of store IDs (optional).
*   `filter_positive_remains`: `1` to return only goods with positive total remains.
*   `filter_positive_free_remains`: `1` to return only goods with positive free remains.
*   **`with_prices`: `1` to include sales prices.**
    *   `type_price_ids`: Array of price type IDs (optional).
*   `with_modifications`: `1` to include modifications.

## Core Fields
*   `id`: int (Unique ID)
*   `name`: string
*   `full_name`: string
*   `nds_id`: int
*   `group_id`: int
*   `part`: string (Article/SKU)
*   `store_code`: string
*   `type`: int (1: Product, 2: Service, 3: Kit)
*   `archive`: bool
*   `description`: string
*   `weight`: float
*   `volume`: float
*   `cost`: float
*   `measure_id`: int
*   `marking_type`: int
*   `updated`: datetime

## Response Structure
```json
{
  "status": "ok",
  "result": [
    {
      "id": 12345,
      "name": "Product Name",
      "part": "SKU-001",
      "prices": [
        { "price": 100.0, "type_id": 1, "currency": "RUB" }
      ],
      "remains": [
        { "store_id": 10, "total": 50.0, "reserved": 5.0 }
      ],
      "barcodes": [
        { "value": "4601234567890", "type": 1 }
      ]
    }
  ]
}
```
