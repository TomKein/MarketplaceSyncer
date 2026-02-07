using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Http;
using WorkerService1.BusinessRu.Models.Requests;
using WorkerService1.BusinessRu.Models.Responses;
using ApiGood = WorkerService1.BusinessRu.Models.Responses.Good;
using DbGood = WorkerService1.Data.Models.Good;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkerService1.Configuration;
using WorkerService1.Data;
using WorkerService1.Data.Models;

namespace WorkerService1.Services;

public class PriceSyncService : IPriceSyncService
{
	private readonly BusinessRuClient _apiClient;

	private readonly AppDbConnection _db;

	private readonly BusinessRuOptions _businessOptions;

	private readonly PriceSyncOptions _syncOptions;

	private readonly ILogger<PriceSyncService> _logger;

	private long _businessId;

	private long _currentSessionId;

	public PriceSyncService(BusinessRuClient apiClient, AppDbConnection db, IOptions<BusinessRuOptions> businessOptions, IOptions<PriceSyncOptions> syncOptions, ILogger<PriceSyncService> logger)
	{
		_apiClient = apiClient;
		_db = db;
		_businessOptions = businessOptions.Value;
		_syncOptions = syncOptions.Value;
		_logger = logger;
	}

	public async Task<bool> ExecutePriceSyncAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		_logger.LogInformation("=== Starting price synchronization ===");
		try
		{
			await InitializeBusinessAsync(cancellationToken);
			await CreateSyncSessionAsync(cancellationToken);
			await LoadDictionariesAsync(cancellationToken);
			await LoadGoodsAsync(cancellationToken);
			await LoadPricesAsync(cancellationToken);
			await CalculatePricesAsync(cancellationToken);
			await UpdatePricesInBusinessRuAsync(cancellationToken);
			await CompleteSyncSessionAsync(success: true, cancellationToken);
			_logger.LogInformation("=== Price synchronization completed successfully ===");
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Price synchronization failed");
			await CompleteSyncSessionAsync(success: false, cancellationToken, ex.Message);
			return false;
		}
	}

	private async Task InitializeBusinessAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Initializing business record...");
		Business business = await _db.Businesses.FirstOrDefaultAsync((Business b) => b.OrganizationId == _businessOptions.OrganizationId, cancellationToken);
		if (business == null)
		{
			_businessId = await DataExtensions.InsertWithInt64IdentityAsync(obj: new Business
			{
				ExternalId = _businessOptions.OrganizationId,
				OrganizationId = _businessOptions.OrganizationId,
				Name = "Organization " + _businessOptions.OrganizationId,
				ApiUrl = _businessOptions.BaseUrl,
				AppId = _businessOptions.AppId,
				SecretEncrypted = _businessOptions.Secret,
				IsActive = true,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			}, dataContext: _db, tableName: null, databaseName: null, schemaName: null, serverName: null, tableOptions: TableOptions.NotSet, token: cancellationToken);
			_logger.LogInformation("Created new business record with ID: {BusinessId}", _businessId);
		}
		else
		{
			_businessId = business.Id;
			_logger.LogInformation("Using existing business record with ID: {BusinessId}", _businessId);
		}
	}

	private async Task CreateSyncSessionAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Creating sync session...");
		_currentSessionId = await _db.SyncSessions.InsertWithInt64IdentityAsync(() => new SyncSession
		{
			BusinessId = _businessId,
			StartedAt = DateTimeOffset.UtcNow,
			Status = "IN_PROGRESS",
			GoodsFetched = 0,
			PricesFetched = 0,
			PricesCalculated = 0,
			PricesUpdated = 0,
			ErrorsCount = 0
		}, cancellationToken);
		_logger.LogInformation("Created sync session with ID: {SessionId}", _currentSessionId);
	}

	private async Task LoadDictionariesAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Loading price types...");
		GetSalePriceTypesRequest priceTypesRequest = new GetSalePriceTypesRequest(_businessOptions.PageLimit);
		SalePriceType[] priceTypesResponse = await _apiClient.RequestAsync<GetSalePriceTypesRequest, SalePriceType[]>(HttpMethod.Get, "salepricetypes", priceTypesRequest, cancellationToken);
		if (priceTypesResponse != null && priceTypesResponse.Length > 0)
		{
			SalePriceType[] array = priceTypesResponse;
			foreach (SalePriceType pt in array)
			{
				if (await _db.PriceTypes.FirstOrDefaultAsync((PriceType p) => p.BusinessId == _businessId && p.ExternalId == pt.Id, cancellationToken) == null)
				{
					await DataExtensions.InsertAsync(obj: new PriceType
					{
						BusinessId = _businessId,
						ExternalId = pt.Id,
						Name = (pt.Name ?? "Unknown"),
						IsSalePrice = ((pt.Name?.Contains("Розничн") ?? false) || (pt.Name?.Contains("Отпускн") ?? false)),
						IsBuyPrice = (pt.Name?.Contains("Закуп") ?? false),
						CreatedAt = DateTimeOffset.UtcNow,
						UpdatedAt = DateTimeOffset.UtcNow
					}, dataContext: _db, tableName: null, databaseName: null, schemaName: null, serverName: null, tableOptions: TableOptions.NotSet, token: cancellationToken);
				}
			}
			_logger.LogInformation("Loaded {Count} price types", priceTypesResponse.Length);
		}
		_logger.LogInformation("Price lists will be created dynamically from price data");
	}

	private async Task LoadGoodsAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Loading goods from Business.ru...");
		int existingCount = await _db.Goods.Where((DbGood good) => good.BusinessId == _businessId).CountAsync(cancellationToken);
		_logger.LogInformation("Found {Count} existing goods in DB", existingCount);
		int startPage = 1;
		if (existingCount > 0)
		{
			startPage = existingCount / _businessOptions.PageLimit + 1;
			_logger.LogInformation("Resuming goods load from page {Page} (already have {Count} goods)", startPage, existingCount);
		}
		else
		{
			_logger.LogInformation("Starting fresh goods load from page 1");
		}
		int page = startPage;
		int totalLoaded = existingCount;
		while (true)
		{
			GetGoodsRequest request = new GetGoodsRequest(null, null, _businessOptions.PageLimit, page);
			ApiGood[] response = await _apiClient.RequestAsync<GetGoodsRequest, ApiGood[]>(HttpMethod.Get, "goods", request, cancellationToken);
			if (response == null || response.Length == 0)
			{
				break;
			}
			List<string> externalIds = response.Select((ApiGood r) => r.Id).ToList();
			Dictionary<string, DbGood> existingGoods = await _db.Goods.Where((DbGood good) => good.BusinessId == _businessId && externalIds.Contains(good.ExternalId)).ToDictionaryAsync((DbGood good) => good.ExternalId, cancellationToken);
			List<DbGood> goodsToInsert = new List<DbGood>();
			List<DbGood> goodsToUpdate = new List<DbGood>();
			ApiGood[] array = response;
			foreach (ApiGood g in array)
			{
				if (existingGoods.TryGetValue(g.Id, out var existingGood))
				{
					existingGood.Name = g.Name ?? existingGood.Name;
					existingGood.PartNumber = g.PartNumber;
					existingGood.StoreCode = g.StoreCode;
					existingGood.Archive = g.Archive;
					existingGood.UpdatedAt = DateTimeOffset.UtcNow;
					existingGood.LastSyncedAt = DateTimeOffset.UtcNow;
					goodsToUpdate.Add(existingGood);
				}
				else
				{
					goodsToInsert.Add(new DbGood
					{
						BusinessId = _businessId,
						ExternalId = g.Id,
						Name = (g.Name ?? "Unknown"),
						PartNumber = g.PartNumber,
						StoreCode = g.StoreCode,
						Archive = g.Archive,
						CreatedAt = DateTimeOffset.UtcNow,
						UpdatedAt = DateTimeOffset.UtcNow,
						LastSyncedAt = DateTimeOffset.UtcNow
					});
				}
				existingGood = null;
			}
			using (List<DbGood>.Enumerator enumerator = goodsToInsert.GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					await DataExtensions.InsertAsync(obj: enumerator.Current, dataContext: _db, tableName: null, databaseName: null, schemaName: null, serverName: null, tableOptions: TableOptions.NotSet, token: cancellationToken);
				}
			}
			if (goodsToUpdate.Count > 0)
			{
				using List<DbGood>.Enumerator enumerator2 = goodsToUpdate.GetEnumerator();
				while (enumerator2.MoveNext())
				{
					await DataExtensions.UpdateAsync(obj: enumerator2.Current, dataContext: _db, tableName: null, databaseName: null, schemaName: null, serverName: null, tableOptions: TableOptions.NotSet, token: cancellationToken);
				}
			}
			totalLoaded += response.Length;
			if ((totalLoaded - existingCount) % 1000 == 0 && totalLoaded > existingCount)
			{
				_logger.LogInformation("Loaded {Count} goods so far... (total in DB: {Total})", totalLoaded - existingCount, totalLoaded);
			}
			await _db.SyncSessions.Where((SyncSession s) => s.Id == _currentSessionId).Set((SyncSession s) => s.GoodsFetched, totalLoaded).UpdateAsync(cancellationToken);
			if (response.Length < _businessOptions.PageLimit)
			{
				break;
			}
			page++;
		}
		_logger.LogInformation("Loaded total {Count} goods", totalLoaded);
	}

	private async Task LoadPricesAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Loading prices from Business.ru...");
		int lastLoadedCount = await _db.GoodPrices.Where((GoodPrice gp) => gp.BusinessId == _businessId).CountAsync(cancellationToken);
		int offset = 0;
		int totalLoaded = 0;
		if (lastLoadedCount > 0)
		{
			_logger.LogInformation("Found {Count} existing prices, resuming from offset {Offset}", lastLoadedCount, lastLoadedCount);
			offset = lastLoadedCount;
			totalLoaded = lastLoadedCount;
		}
		Dictionary<string, long> goodsCache = await _db.Goods.Where((DbGood g) => g.BusinessId == _businessId).ToDictionaryAsync((DbGood g) => g.ExternalId, (DbGood g) => g.Id, cancellationToken);
		_logger.LogInformation("Loaded {Count} goods into cache for price matching", goodsCache.Count);
		Dictionary<string, PriceList> priceListsCache = await _db.PriceLists.Where((PriceList pl) => pl.BusinessId == _businessId).ToDictionaryAsync((PriceList pl) => pl.ExternalId, cancellationToken);
		PriceType? defaultPriceType = await _db.PriceTypes.FirstOrDefaultAsync((PriceType pt) => pt.BusinessId == _businessId, cancellationToken);
		if (defaultPriceType == null)
		{
			defaultPriceType = new PriceType
			{
				BusinessId = _businessId,
				ExternalId = "default",
				Name = "Default Price Type",
				IsSalePrice = true,
				IsBuyPrice = false,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			};
			defaultPriceType.Id = await _db.InsertWithInt64IdentityAsync(defaultPriceType, null, null, null, null, TableOptions.NotSet, cancellationToken);
		}
		_logger.LogInformation("Loading price list goods mapping...");
		Dictionary<string, (string goodId, string priceListId)> priceListGoodsMap = new Dictionary<string, (string, string)>();
		int plgOffset = 0;
		SalePriceListGood[] plgResponse;
		do
		{
			GetSalePriceListGoodsRequest plgRequest = new GetSalePriceListGoodsRequest(_businessOptions.PageLimit, plgOffset);
			plgResponse = await _apiClient.RequestAsync<GetSalePriceListGoodsRequest, SalePriceListGood[]>(HttpMethod.Get, "salepricelistgoods", plgRequest, cancellationToken);
			if (plgResponse == null || plgResponse.Length == 0)
			{
				break;
			}
			int addedInBatch = 0;
			SalePriceListGood[] array = plgResponse;
			foreach (SalePriceListGood plg in array)
			{
				if (!string.IsNullOrEmpty(plg.Id) && !string.IsNullOrEmpty(plg.GoodId) && !string.IsNullOrEmpty(plg.PriceListId) && !priceListGoodsMap.ContainsKey(plg.Id))
				{
					priceListGoodsMap[plg.Id] = (plg.GoodId, plg.PriceListId);
					addedInBatch++;
				}
			}
			plgOffset += plgResponse.Length;
			_logger.LogInformation("Loaded {Count} price list goods, added {Added} new mappings (total: {Total})", plgResponse.Length, addedInBatch, priceListGoodsMap.Count);
			if (addedInBatch == 0)
			{
				_logger.LogWarning("API returned duplicate data, stopping pagination for salepricelistgoods");
				break;
			}
		}
		while (plgResponse.Length >= _businessOptions.PageLimit);
		_logger.LogInformation("Total price list goods mappings loaded: {Count}", priceListGoodsMap.Count);
		while (true)
		{
			GetSalePriceListGoodPricesRequest request = new GetSalePriceListGoodPricesRequest(_businessOptions.PageLimit, offset);
			SalePriceListGoodPrice[] response = await _apiClient.RequestAsync<GetSalePriceListGoodPricesRequest, SalePriceListGoodPrice[]>(HttpMethod.Get, "salepricelistgoodprices", request, cancellationToken);
			if (response == null || response.Length == 0)
			{
				break;
			}
			List<GoodPrice> pricesToInsert = new List<GoodPrice>();
			List<GoodPrice> pricesToUpdate = new List<GoodPrice>();
			List<long> batchGoodIds = new List<long>();
			List<long> batchPriceListIds = new List<long>();
			Dictionary<(long goodId, long priceListId), (string priceId, decimal currentPrice, string? currencyId)> priceDataMap = new Dictionary<(long, long), (string, decimal, string?)>();
			HashSet<string> skippedGoodIds = new HashSet<string>();
			HashSet<string> skippedNoMapping = new HashSet<string>();
			HashSet<string> skippedNoPriceType = new HashSet<string>();
			SalePriceListGoodPrice[] array2 = response;
			foreach (SalePriceListGoodPrice price in array2)
			{
				if (string.IsNullOrEmpty(price.PriceListGoodId) || string.IsNullOrEmpty(price.Price))
				{
					continue;
				}
				if (!priceListGoodsMap.TryGetValue(price.PriceListGoodId, out var mapping))
				{
					skippedNoMapping.Add(price.PriceListGoodId);
					continue;
				}
				if (!goodsCache.TryGetValue(mapping.goodId, out var goodId))
				{
					skippedGoodIds.Add(mapping.goodId);
					continue;
				}
				if (!priceListsCache.TryGetValue(mapping.priceListId, out var priceList))
				{
					long priceTypeId = defaultPriceType.Id;
					if (!string.IsNullOrEmpty(price.PriceTypeId))
					{
						PriceType? priceType = await _db.PriceTypes.FirstOrDefaultAsync((PriceType pt) => pt.BusinessId == _businessId && pt.ExternalId == price.PriceTypeId, cancellationToken);
						if (priceType != null)
						{
							priceTypeId = priceType.Id;
						}
						else
						{
							skippedNoPriceType.Add(price.PriceTypeId);
						}
					}
					priceList = new PriceList
					{
						BusinessId = _businessId,
						ExternalId = mapping.priceListId,
						PriceTypeId = priceTypeId,
						Name = "Price List " + mapping.priceListId,
						IsActive = true,
						CreatedAt = DateTimeOffset.UtcNow,
						UpdatedAt = DateTimeOffset.UtcNow
					};
					long plId = await _db.InsertWithInt64IdentityAsync(priceList, null, null, null, null, TableOptions.NotSet, cancellationToken);
					priceList.Id = plId;
					priceListsCache[mapping.priceListId] = priceList;
				}
				batchGoodIds.Add(goodId);
				batchPriceListIds.Add(priceList.Id);
				priceDataMap[(goodId, priceList.Id)] = (price.Id, decimal.Parse(price.Price), null);
				mapping = default((string, string));
				priceList = null;
			}
			if (skippedGoodIds.Count > 0 || skippedNoMapping.Count > 0)
			{
				_logger.LogWarning("Skip stats: {NoMapping} no mapping, {NoGoods} goods not in cache. Sample missing mappings: {MappingSample}. Sample missing goods: {GoodsSample}", skippedNoMapping.Count, skippedGoodIds.Count, string.Join(", ", skippedNoMapping.Take(5)), string.Join(", ", skippedGoodIds.Take(5)));
			}
			if (batchGoodIds.Count == 0)
			{
				if (skippedGoodIds.Count > 0)
				{
					_logger.LogWarning("\ufe0f ENTIRE BATCH SKIPPED! All {SkippedCount} prices have no matching goods in cache. This is NOT NORMAL - check if goods loaded correctly. Sample missing good IDs from Business.ru: {SampleIds}. Total goods in cache: {CacheSize}", skippedGoodIds.Count, string.Join(", ", skippedGoodIds.Take(10)), goodsCache.Count);
				}
				offset += _businessOptions.PageLimit;
				continue;
			}
			if (skippedGoodIds.Count > 0)
			{
				double skipPercentage = (double)skippedGoodIds.Count * 100.0 / (double)(skippedGoodIds.Count + batchGoodIds.Count);
				if (skipPercentage > 10.0)
				{
					_logger.LogWarning("\ufe0f High skip rate in batch: {SkippedCount} prices skipped ({Percentage:F1}% of batch) - goods not found in cache. Sample missing good IDs: {SampleIds}. This may indicate incomplete goods loading or wrong filters.", skippedGoodIds.Count, skipPercentage, string.Join(", ", skippedGoodIds.Take(5)));
				}
				else
				{
					_logger.LogDebug("Skipped {SkippedCount} prices in this batch ({Percentage:F1}%) - goods not in cache", skippedGoodIds.Count, skipPercentage);
				}
			}
			Dictionary<(long GoodId, long PriceListId), GoodPrice> existingPrices = await _db.GoodPrices.Where((GoodPrice gp) => gp.BusinessId == _businessId && batchGoodIds.Contains(gp.GoodId) && batchPriceListIds.Contains(gp.PriceListId)).ToDictionaryAsync((GoodPrice gp) => (GoodId: gp.GoodId, PriceListId: gp.PriceListId), cancellationToken);
			foreach (KeyValuePair<(long, long), (string, decimal, string?)> kvp in priceDataMap)
			{
				var (goodId2, priceListId) = kvp.Key;
				var (priceId, currentPrice, currencyId) = kvp.Value;
				if (existingPrices.TryGetValue((goodId2, priceListId), out var existingPrice))
				{
					existingPrice.CurrentPrice = currentPrice;
					existingPrice.UpdatedAt = DateTimeOffset.UtcNow;
					pricesToUpdate.Add(existingPrice);
				}
				else
				{
					pricesToInsert.Add(new GoodPrice
					{
						BusinessId = _businessId,
						GoodId = goodId2,
						PriceListId = priceListId,
						ExternalPriceRecordId = priceId,
						OriginalPrice = currentPrice,
						CurrentPrice = currentPrice,
						CurrencyId = currencyId,
						IsProcessed = false,
						CreatedAt = DateTimeOffset.UtcNow,
						UpdatedAt = DateTimeOffset.UtcNow
					});
				}
				existingPrice = null;
			}
			using (List<GoodPrice>.Enumerator enumerator2 = pricesToInsert.GetEnumerator())
			{
				while (enumerator2.MoveNext())
				{
					await DataExtensions.InsertAsync(obj: enumerator2.Current, dataContext: _db, tableName: null, databaseName: null, schemaName: null, serverName: null, tableOptions: TableOptions.NotSet, token: cancellationToken);
				}
			}
			if (pricesToUpdate.Count > 0)
			{
				using List<GoodPrice>.Enumerator enumerator3 = pricesToUpdate.GetEnumerator();
				while (enumerator3.MoveNext())
				{
					await DataExtensions.UpdateAsync(obj: enumerator3.Current, dataContext: _db, tableName: null, databaseName: null, schemaName: null, serverName: null, tableOptions: TableOptions.NotSet, token: cancellationToken);
				}
			}
			totalLoaded += pricesToInsert.Count + pricesToUpdate.Count;
			int batchTotal = pricesToInsert.Count + pricesToUpdate.Count + skippedGoodIds.Count;
			double skipPercentageTotal = ((batchTotal > 0) ? ((double)skippedGoodIds.Count * 100.0 / (double)batchTotal) : 0.0);
			_logger.LogInformation("Batch processed: {Inserted} inserted, {Updated} updated, {Skipped} skipped ({SkipPercent:F1}%). Total saved: {Total}", pricesToInsert.Count, pricesToUpdate.Count, skippedGoodIds.Count, skipPercentageTotal, totalLoaded);
			int pricesLoadedThisSession = totalLoaded - lastLoadedCount;
			if (pricesLoadedThisSession > 0 && pricesLoadedThisSession % 1000 == 0)
			{
				_logger.LogInformation("Loaded {Count} prices so far... (total in DB: {Total})", pricesLoadedThisSession, totalLoaded);
			}
			await _db.SyncSessions.Where((SyncSession s) => s.Id == _currentSessionId).Set((SyncSession s) => s.PricesFetched, totalLoaded).UpdateAsync(cancellationToken);
			if (response.Length < _businessOptions.PageLimit)
			{
				break;
			}
			offset += _businessOptions.PageLimit;
		}
		_logger.LogInformation("Loaded total {Count} prices", totalLoaded);
	}

	private async Task CalculatePricesAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Calculating new prices (+{Markup}%, rounding to {Rounding})...", _syncOptions.DefaultMarkupPercent, _syncOptions.PriceRoundingStep);
		int updatedCount = await _db.GoodPrices.Where((GoodPrice gp) => gp.BusinessId == _businessId && !gp.IsProcessed && gp.CalculatedPrice == (decimal?)null).Set((GoodPrice gp) => gp.CalculatedPrice, (GoodPrice gp) => Sql.Round(gp.OriginalPrice * (1m + _syncOptions.DefaultMarkupPercent / 100m) / _syncOptions.PriceRoundingStep) * (decimal?)_syncOptions.PriceRoundingStep).Set((GoodPrice gp) => gp.PriceIncreasePercent, _syncOptions.DefaultMarkupPercent)
			.Set((GoodPrice gp) => gp.CalculationDate, DateTimeOffset.UtcNow)
			.Set((GoodPrice gp) => gp.UpdatedAt, DateTimeOffset.UtcNow)
			.UpdateAsync(cancellationToken);
		await _db.SyncSessions.Where((SyncSession s) => s.Id == _currentSessionId).Set((SyncSession s) => s.PricesCalculated, updatedCount).UpdateAsync(cancellationToken);
		_logger.LogInformation("Calculated {Count} new prices", updatedCount);
	}

	private async Task UpdatePricesInBusinessRuAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Updating prices in Business.ru...");
		int totalUpdated = 0;
		int totalErrors = 0;
		int batchSize = _syncOptions.BatchSize;
		while (true)
		{
			List<GoodPrice> pricesToUpdate = await _db.GoodPrices.Where((GoodPrice gp) => gp.BusinessId == _businessId && !gp.IsProcessed && gp.CalculatedPrice != (decimal?)null && Math.Abs(gp.CurrentPrice - gp.CalculatedPrice.Value) > _syncOptions.PriceComparisonTolerance).Take(batchSize).ToListAsync(cancellationToken);
			if (pricesToUpdate.Count == 0)
			{
				break;
			}
			foreach (GoodPrice priceRecord in pricesToUpdate)
			{
				try
				{
					UpdatePriceRequest updateRequest = new UpdatePriceRequest(priceRecord.ExternalPriceRecordId, (priceRecord.CalculatedPrice ?? 0).ToString("F2", CultureInfo.InvariantCulture));
					if (!string.IsNullOrEmpty(await _apiClient.RequestAsync<UpdatePriceRequest, string>(HttpMethod.Put, "salepricelistgoodprices", updateRequest, cancellationToken)))
					{
						priceRecord.IsProcessed = true;
						priceRecord.UpdatedInBusinessRuAt = DateTimeOffset.UtcNow;
						priceRecord.CurrentPrice = priceRecord.CalculatedPrice ?? 0;
						priceRecord.UpdatedAt = DateTimeOffset.UtcNow;
						await _db.UpdateAsync(priceRecord, null, null, null, null, TableOptions.NotSet, cancellationToken);
						await _db.InsertAsync(new PriceUpdateLog
						{
							GoodPriceId = priceRecord.Id,
							OldPrice = priceRecord.OriginalPrice,
							NewPrice = priceRecord.CalculatedPrice ?? 0,
							ActionType = "UPDATE",
							CreatedAt = DateTimeOffset.UtcNow
						}, null, null, null, null, TableOptions.NotSet, cancellationToken);
						totalUpdated++;
					}
					else
					{
						_logger.LogWarning("Failed to update price for record {RecordId} (result was null)", priceRecord.ExternalPriceRecordId);
						await _db.InsertAsync(new PriceUpdateLog
						{
							GoodPriceId = priceRecord.Id,
							OldPrice = priceRecord.OriginalPrice,
							NewPrice = priceRecord.CalculatedPrice ?? 0,
							ActionType = "ERROR",
							ErrorMessage = "API returned false",
							CreatedAt = DateTimeOffset.UtcNow
						}, null, null, null, null, TableOptions.NotSet, cancellationToken);
						totalErrors++;
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error updating price for record {RecordId}", priceRecord.ExternalPriceRecordId);
					await _db.InsertAsync(new PriceUpdateLog
					{
						GoodPriceId = priceRecord.Id,
						OldPrice = priceRecord.OriginalPrice,
						NewPrice = priceRecord.CalculatedPrice ?? 0,
						ActionType = "ERROR",
						ErrorMessage = ex.Message,
						CreatedAt = DateTimeOffset.UtcNow
					}, null, null, null, null, TableOptions.NotSet, cancellationToken);
					totalErrors++;
				}
			}
			_logger.LogInformation("Updated {Updated} prices, {Errors} errors (batch of {BatchSize})", totalUpdated, totalErrors, pricesToUpdate.Count);
			await _db.SyncSessions.Where((SyncSession s) => s.Id == _currentSessionId).Set((SyncSession s) => s.PricesUpdated, totalUpdated).Set((SyncSession s) => s.ErrorsCount, totalErrors)
				.UpdateAsync(cancellationToken);
		}
		_logger.LogInformation("Total updated {Updated} prices with {Errors} errors", totalUpdated, totalErrors);
	}

	private async Task CompleteSyncSessionAsync(bool success, CancellationToken cancellationToken, string? errorMessage = null)
	{
		if (string.IsNullOrEmpty(errorMessage))
		{
			await _db.SyncSessions.Where((SyncSession s) => s.Id == _currentSessionId).Set((SyncSession s) => s.CompletedAt, DateTimeOffset.UtcNow).Set((SyncSession s) => s.Status, success ? "COMPLETED" : "FAILED")
				.UpdateAsync(cancellationToken);
		}
		else
		{
			string errorJson = JsonSerializer.Serialize(new
			{
				error = errorMessage,
				timestamp = DateTimeOffset.UtcNow
			});
			using DbCommand cmd = _db.CreateCommand();
			cmd.CommandText = "UPDATE sync_sessions \r\n                  SET completed_at = @completedAt, \r\n                      status = @status, \r\n                      error_details = @errorDetails::jsonb \r\n                  WHERE id = @id";
			DbParameter completedAtParam = cmd.CreateParameter();
			completedAtParam.ParameterName = "completedAt";
			completedAtParam.Value = DateTimeOffset.UtcNow;
			cmd.Parameters.Add(completedAtParam);
			DbParameter statusParam = cmd.CreateParameter();
			statusParam.ParameterName = "status";
			statusParam.Value = (success ? "COMPLETED" : "FAILED");
			cmd.Parameters.Add(statusParam);
			DbParameter errorDetailsParam = cmd.CreateParameter();
			errorDetailsParam.ParameterName = "errorDetails";
			errorDetailsParam.Value = errorJson;
			cmd.Parameters.Add(errorDetailsParam);
			DbParameter idParam = cmd.CreateParameter();
			idParam.ParameterName = "id";
			idParam.Value = _currentSessionId;
			cmd.Parameters.Add(idParam);
			await cmd.ExecuteNonQueryAsync(cancellationToken);
		}
		_logger.LogInformation("Sync session {SessionId} completed with status: {Status}", _currentSessionId, success ? "SUCCESS" : "FAILED");
	}
}
