using Microsoft.Extensions.Options;
using WorkerService1.Configuration;
using WorkerService1.BusinessRu.Client;
using WorkerService1.BusinessRu.Models.Requests;
using WorkerService1.BusinessRu.Models.Responses;
using WorkerService1.BusinessRu.Models.Common;

namespace WorkerService1;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    //legacy
    private const string APP_ID = "768289";
    private const string PRICE_ID = "75524";
    static RootResponse _rr = new RootResponse();
    static Dictionary<string, string> _data = new Dictionary<string, string>();

    public Worker(ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker starting at: {time}", DateTimeOffset.Now);

        try
        {
            // DEBUG: Получаем количество товаров с фильтрами из легаси
            using var scope = _serviceProvider.CreateScope();
            var businessRuClient = scope.ServiceProvider.GetRequiredService<BusinessRuClient>();
            var businessOptions = scope.ServiceProvider.GetRequiredService<IOptions<BusinessRuOptions>>().Value;

            _logger.LogInformation("=== DEBUG MODE: Checking goods count ===");
            //"75524" тип прайса

            // var priceTypes = await businessRuClient.MyRequestAsync<Good>(
            //     HttpMethod.Get,
            //     "salepricetypes",
            //     new Dictionary<string, string>(),
            //     stoppingToken
            // );
            
            // string s = await RequestAsync("get", "goods", new Dictionary<string, string>{
            //     {"archive", "0"},
            //     {"type", "1"},
            //     {"limit", "100"},
            //     {"page", "1"},
            //     {"with_additional_fields", "1"},
            //     {"with_attributes", "1"},
            //     {"with_remains", "1"},
            //     {"with_prices", "1"},
            //     {"type_price_ids[0]", "75524"} //"75524"
            // });

            // Testing code - commented out for now
            // var goods = await businessRuClient.MyRequestAsync<Good[]>(
            //     HttpMethod.Get,
            //     "goods",
            //     new Dictionary<string, string>{
            //         {"type", "1"},
            //         {"with_additional_fields", "1"},
            //         {"with_attributes", "1"},
            //         {"with_remains", "1"},
            //         {"with_prices", "1"},
            //         {"type_price_ids[0]", "75524" },
            //         {"page", "1"},
            //         {"limit", "100"},
            //     },
            //     stoppingToken
            //     );
            // s = await RequestAsync("get", "goods", new Dictionary<string, string>{
            //     {"type", "1"},
            //     {"with_additional_fields", "1"},
            //     {"with_attributes", "1"},
            //     {"with_remains", "1"},
            //     {"with_prices", "1"},
            //     {"type_price_ids[0]",SalePrice.id },
            //     {"updated[from]", _lastLiteScanTime.ToString()},
            //     {"page", i.ToString()},
            //     {"limit", "250"},
            // });
            // Testing code - commented out for now
            // var priceList = await businessRuClient.MyRequestAsync<SalePriceListGoodPrice[]>(
            //     HttpMethod.Get, 
            //     "salepricelistgoodprices", 
            //     new Dictionary<string, string>
            //     {
            //         {"count_only", "1"},
            //     }, 
            //     stoppingToken);
            
            _logger.LogInformation("Organization ID: {OrgId}", businessOptions.OrganizationId);
            
            // Сначала получаем типы цен
            var priceTypesRequest = new GetSalePriceTypesRequest(Limit: 250, Offset: 0);
            
            var priceTypesResponse = await businessRuClient.RequestAsync<GetSalePriceTypesRequest, EntityResponse[]>(
                HttpMethod.Get, 
                "salepricetypes", 
                priceTypesRequest, 
                stoppingToken);
            
            if (priceTypesResponse == null || priceTypesResponse.Length == 0)
            {
                _logger.LogError("Не удалось получить типы цен");
                return;
            }

            var salePriceId = priceTypesResponse[0].Id;
            _logger.LogInformation("Используем тип цены ID: {PriceTypeId}, Name: {PriceTypeName}", 
                salePriceId, priceTypesResponse[0].Name);

            // Используем простой запрос без дополнительных параметров
            var goodsRequest = new GetGoodsRequest(
                Type: 1,
                Archive: 0,
                Limit: 250,
                Page: 1
            );

            _logger.LogInformation("Запрашиваем товары с параметрами: type=1, archive=0, limit=250, page=1");

            var goodsResponse = await businessRuClient.RequestAsync<GetGoodsRequest, Good[]>(
                HttpMethod.Get,
                "goods",
                goodsRequest,
                stoppingToken);

            if (goodsResponse != null)
            {
                _logger.LogInformation("===========================================");
                _logger.LogInformation("Товаров получено: {Count}", goodsResponse.Length);
                _logger.LogInformation("===========================================");
                
                if (goodsResponse.Length > 0)
                {
                    _logger.LogInformation("Пример первого товара: ID={Id}, Name={Name}", 
                        goodsResponse[0].Id, goodsResponse[0].Name);
                }
            }
            else
            {
                _logger.LogError("Не удалось получить товары");
            }

            // DEBUG: Досрочный выход
            _logger.LogInformation("DEBUG MODE: Early exit");
            return;

            // TODO: Раскомментировать для полной синхронизации
            /*
            var priceSyncService = scope.ServiceProvider.GetRequiredService<IPriceSyncService>();
            var success = await priceSyncService.ExecutePriceSyncAsync(stoppingToken);

            if (success)
            {
                _logger.LogInformation("Price synchronization completed successfully");
            }
            else
            {
                _logger.LogError("Price synchronization failed");
            }
            */
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Worker");
        }

        _logger.LogInformation("Worker completed at: {time}", DateTimeOffset.Now);
    }
    
    /*
    public static async Task<string> RequestAsync(string action, string model, Dictionary<string, string> par) 
    {
        try {
            HttpResponseMessage httpResponseMessage = null;
            par["app_id"] = "768289";
            SortedDictionary<string, string> parSorted = new SortedDictionary<string, string>(new PhpKSort());
            foreach (var key in par.Keys) {
                parSorted.Add(key, par[key]);
            }
            string qstr = QueryStringBuilder.BuildQueryString(parSorted);
            for (int i = 0; i < 360; i++) {
                try {
                    while (_rr == null || _rr.token == "" || _rr.token == null) {
                        await RepairAsync();
                    }
                    byte[] hash = Encoding.UTF8.GetBytes(_rr.token + SECRET + qstr);
                    MD5 md5 = new MD5CryptoServiceProvider();
                    byte[] hashenc = md5.ComputeHash(hash);
                    qstr += "&app_psw=" + GetMd5(hashenc);

                    string url = BASE_URL + model + ".json";

                    if (action.ToUpper() == "GET") {
                        httpResponseMessage = await _hc.GetAsync(url + "?" + qstr);
                    } else if (action.ToUpper() == "PUT") {
                        HttpContent content = new StringContent(qstr);//, Encoding.UTF8, "application/json");
                        httpResponseMessage = await _hc.PutAsync(url, content);
                    } else if (action.ToUpper() == "POST") {
                        HttpContent content = new StringContent(qstr, Encoding.UTF8, "application/x-www-form-urlencoded");
                        httpResponseMessage = await _hc.PostAsync(url, content);
                    } else if (action.ToUpper() == "DELETE") {
                        //HttpContent content = new StringContent(qstr);//, Encoding.UTF8, "application/x-www-form-urlencoded");
                        httpResponseMessage = await _hc.DeleteAsync(url + "?" + qstr);
                    }
                    if (httpResponseMessage.StatusCode == HttpStatusCode.OK) {
                        var js = await httpResponseMessage.Content.ReadAsStringAsync();
                        _rr = JsonConvert.DeserializeObject<RootResponse>(js);
                        var delay = _rr.request_count * (_rr.request_count / 200);
                        if (_rr.request_count % 10 == 0)
                            Log.Add($"{L}RequestAsync: REQUEST_COUNT={_rr.request_count}, delay={delay}");
                        Thread.Sleep(_rr.request_count * 3);
                        _flagRequestActive = false;
                        return _rr != null ? JsonConvert.SerializeObject(_rr.result) : "";
                    }
                    Log.Add($"{L}RequestAsync: ошибка запроса - {httpResponseMessage.StatusCode.ToString()}");
                    await RepairAsync();
                    qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                    await Task.Delay(20000);
                } catch (Exception x) {
                    Log.Add($"{L}RequestAsync: ошибка запроса к бизнес.ру [{i}] - {x.Message}");
                    await Task.Delay(20000);
                    qstr = qstr.Contains("&app_psw=") ? qstr.Replace("&app_psw=", "|").Split('|')[0] : qstr;
                    _rr.token = "";
                    _flagRequestActive = false;
                }
            };
        } catch (Exception x) {
            Log.Add($"{L}RequestAsync - ошибка запроса к бизнес.ру - {x.Message}");
            _flagRequestActive = false;
        }
        return "";
    }
    
    public static async Task RepairAsync() {
        await Task.Delay(1000);
        _data["app_id"] = APP_ID;
        var sb = new StringBuilder();
        foreach (var key in _data.Keys.OrderBy(x => x, StringComparer.Ordinal)) {
            sb.Append("&")
                .Append(key)
                .Append("=")
                .Append(_data[key]);
        }
        sb.Remove(0, 1);//убираем первый &
        string ps = sb.ToString();
        byte[] hash = Encoding.ASCII.GetBytes(SECRET + ps);
        MD5 md5 = new MD5CryptoServiceProvider();
        byte[] hashenc = md5.ComputeHash(hash);
        ps += "&app_psw=" + GetMd5(hashenc);
        Uri url = new Uri(BASE_URL, "repair.json?" + ps);
        HttpResponseMessage res = await _hc.GetAsync(url);
        var js = await res.Content.ReadAsStringAsync();

        if (res.StatusCode != HttpStatusCode.OK || js.Contains("Превышение лимита")) {
            _rr.token = "";
            Log.Add($"{L}RepairAsync: ошибка получения токена! - {res.StatusCode.ToString()}");
            await Task.Delay(30000);
        } else {
            _rr = JsonConvert.DeserializeObject<RootResponse>(js);
            Log.Add($"{L}RepairAsync: получен новый токен!");
        }
        await Task.Delay(500);
    }
    */
    
    public class RootResponse {
        public string status { get; set; }
        public object result { get; set; }
        public string token { get; set; }
        public string app_psw { get; set; }
        public int request_count { get; set; }
    }
}