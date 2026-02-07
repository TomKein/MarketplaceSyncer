# Legacy Project Data Audit (Business.Ru)

## Requested API Parameters (Legacy)
In `Class365API.cs` (`GetBusGoodsAsync2`, `GoLiteSync`):
- `archive`: "0"
- `type`: "1"  (Goods)
- `limit`, `page`: Pagination
- `with_additional_fields`: "1"
- `with_attributes`: "1"
- `with_remains`: "1"
- `with_prices`: "1"
- `type_price_ids[0]`: (Specific Sale Price ID)
- `updated[from]`: (For incremental sync)

## Data Objects & Fields (Legacy)

### core (GoodObject)
- `id`
- `name`
- `full_name`
- `group_id`
- `part` (SKU)
- `store_code`
- `archive`
- `description`
- `updated`
- `updated_remains_prices`
- `measure_id`
- `weight`
- `volume`
- `length`, `width`, `height`
- `hscode_id`
- `country_id`

### Collections
- **`remains`**: List of store remains (`total`, `reserved`).
- **`prices`**: List of prices (`price`, `price_type`).
- **`attributes`**: List of attributes (`attribute_id`, `value`). Used for:
    - Images (via `with_additional_fields` probably?) -> Note: Legacy has `public List<BusImage> images`.
    - Urls (Ozon, WB, Drom, VK link stored in custom attributes or fields).
    - Specifications: Validity, PackQuantity, Complectation, Garanty, Alternatives, FabricBoxCount, Color, TechType, DangerClass, OEM, ManufactureCountry, MotorType, Placement, Material, ExpirationDays, Place, Side, QuantOfSell, Keywords, Thickness, Height, Length, Volume.

### Custom Fields (Mapped to custom IDs)
- `drom`, `vk`, `ozon`, `wb` (links).

## New Project Status (MarketplaceSyncer.Service)

### `GetGoodsAsync` Implementation
Currently uses `QueryAsync` ("resource"="good") requesting ONLY:
- `id`
- `name`
- `part_number`
- `store_code`
- `archive`

### Missing in New Project
- **Parameters**: `with_prices`, `with_remains`, `with_attributes`, `with_additional_fields`.
- **Model (`Good.cs`)**:
    - `Prices` property exists but is not populated by the current call.
    - Missing `Remains`.
    - Missing `Attributes`.
    - Missing `Images`.
    - Missing `Description`, `Weight`, `Volume`, `MeasureId`, `GroupId`.
    - Missing `Full Name`.

## Recommendation
Update `BusinessRuClient.GetGoodsAsync` to use `RequestAsync` with the full set of legacy parameters (`with_prices=1`, etc.) and update the `Good` model to deserialize all critical fields, especially Prices, Remains, and Attributes.
