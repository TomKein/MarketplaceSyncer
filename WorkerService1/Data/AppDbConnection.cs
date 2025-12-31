using LinqToDB;
using LinqToDB.Data;
using WorkerService1.Data.Models;

namespace WorkerService1.Data;

/// <summary>
/// Подключение к БД через Linq2Db
/// </summary>
public class AppDbConnection : DataConnection
{
    public AppDbConnection(string connectionString)
        : base(ProviderName.PostgreSQL, connectionString)
    {
    }

    public AppDbConnection(DataOptions<AppDbConnection> options)
        : base(options.Options)
    {
    }

    // Таблицы
    public ITable<Business> Businesses => this.GetTable<Business>();
    public ITable<Good> Goods => this.GetTable<Good>();
    public ITable<PriceType> PriceTypes => this.GetTable<PriceType>();
    public ITable<PriceList> PriceLists => this.GetTable<PriceList>();
    public ITable<GoodPrice> GoodPrices => this.GetTable<GoodPrice>();
    public ITable<PriceUpdateLog> PriceUpdateLogs => this.GetTable<PriceUpdateLog>();
    public ITable<SyncSession> SyncSessions => this.GetTable<SyncSession>();
}
