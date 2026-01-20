using LinqToDB;
using LinqToDB.Data;
using MarketplaceSyncer.Service.Data.Models;
using Attribute = MarketplaceSyncer.Service.Data.Models.Attribute;

namespace MarketplaceSyncer.Service.Data;

public class AppDataConnection : DataConnection
{
    public AppDataConnection(DataOptions<AppDataConnection> options) : base(options.Options)
    {
    }

    public ITable<Group> Groups => this.GetTable<Group>();
    public ITable<Measure> Measures => this.GetTable<Measure>();
    public ITable<Country> Countries => this.GetTable<Country>();
    public ITable<Currency> Currencies => this.GetTable<Currency>();
    public ITable<GoodsMeasure> GoodsMeasures => this.GetTable<GoodsMeasure>();
    public ITable<Good> Goods => this.GetTable<Good>();
    public ITable<SyncState> SyncState => this.GetTable<SyncState>();
    public ITable<GoodImage> GoodImages => this.GetTable<GoodImage>();
    public ITable<SyncSession> SyncSessions => this.GetTable<SyncSession>();
    public ITable<SyncEvent> SyncEvents => this.GetTable<SyncEvent>();
    public ITable<Store> Stores => this.GetTable<Store>();
    public ITable<StoreGood> StoreGoods => this.GetTable<StoreGood>();
    public ITable<Attribute> Attributes => this.GetTable<Attribute>();
    public ITable<AttributeValue> AttributeValues => this.GetTable<AttributeValue>();
    public ITable<GoodAttribute> GoodAttributes => this.GetTable<GoodAttribute>();
    public ITable<PriceType> PriceTypes => this.GetTable<PriceType>();
    public ITable<GoodPrice> GoodPrices => this.GetTable<GoodPrice>();
}
