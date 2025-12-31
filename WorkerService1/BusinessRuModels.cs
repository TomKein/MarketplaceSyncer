using System;
using System.Text.Json.Serialization;

namespace BusinessRu.ApiClient;


public sealed record ApiResponse<T>([property: JsonPropertyName("result")] T? Result, [property: JsonPropertyName("request_count")] int RequestCount = 0);

public sealed record EntityResponse([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("created_at")] DateTime CreatedAt);

public sealed record GetEntityRequest([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("fields")] string[]? Fields = null);

public sealed record GetGoodsRequest([property: JsonPropertyName("type")] int? Type = null, [property: JsonPropertyName("archive")] int? Archive = null, [property: JsonPropertyName("limit")] int? Limit = null, [property: JsonPropertyName("page")] int? Page = null);

public sealed record GetSalePriceListGoodPricesRequest([property: JsonPropertyName("limit")] int? Limit = null, [property: JsonPropertyName("offset")] int? Offset = null);

public sealed record GetSalePriceListGoodsRequest([property: JsonPropertyName("limit")] int? Limit = null, [property: JsonPropertyName("offset")] int? Offset = null);

public sealed record GetSalePriceListsRequest([property: JsonPropertyName("limit")] int? Limit = null);

public sealed record GetSalePriceTypesRequest([property: JsonPropertyName("limit")] int? Limit = null, [property: JsonPropertyName("offset")] int? Offset = null);

public sealed record Good([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string? Name = null, [property: JsonPropertyName("part_number")] string? PartNumber = null, [property: JsonPropertyName("store_code")] string? StoreCode = null, [property: JsonPropertyName("archive")] bool Archive = false);

public sealed record SalePriceList([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string? Name = null, [property: JsonPropertyName("price_type_id")] string? PriceTypeId = null, [property: JsonPropertyName("active")] bool Active = false);

public sealed record SalePriceListGood([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("good_id")] string? GoodId = null, [property: JsonPropertyName("price_list_id")] string? PriceListId = null);

public sealed record SalePriceListGoodPrice([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("price_list_good_id")] string? PriceListGoodId = null, [property: JsonPropertyName("price_type_id")] string? PriceTypeId = null, [property: JsonPropertyName("price")] string? Price = null, [property: JsonPropertyName("updated")] string? Updated = null);

public sealed record SalePriceType([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("responsible_employee_id")] string? ResponsibleEmployeeId = null, [property: JsonPropertyName("organization_id")] string? OrganizationId = null, [property: JsonPropertyName("currency_id")] string? CurrencyId = null, [property: JsonPropertyName("owner_employee_id")] string? OwnerEmployeeId = null, [property: JsonPropertyName("archive")] bool Archive = false, [property: JsonPropertyName("updated")] string? Updated = null, [property: JsonPropertyName("deleted")] bool Deleted = false, [property: JsonPropertyName("departments_ids")] int[]? DepartmentsIds = null);

public sealed record TokenResponse([property: JsonPropertyName("token")] string? Token);

public sealed record UpdatePriceRequest([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("price")] string Price);