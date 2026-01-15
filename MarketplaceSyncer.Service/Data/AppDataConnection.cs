using LinqToDB;
using LinqToDB.Data;
using MarketplaceSyncer.Service.Data.Models;

namespace MarketplaceSyncer.Service.Data;

public class AppDataConnection : DataConnection
{
    public AppDataConnection(DataOptions<AppDataConnection> options) : base(options.Options)
    {
    }

    public ITable<Group> Groups => this.GetTable<Group>();
    public ITable<Unit> Units => this.GetTable<Unit>();
    public ITable<Good> Goods => this.GetTable<Good>();
    public ITable<SyncState> SyncState => this.GetTable<SyncState>();
    public ITable<GoodImage> GoodImages => this.GetTable<GoodImage>();
    public ITable<SyncSession> SyncSessions => this.GetTable<SyncSession>();
    public ITable<SyncEvent> SyncEvents => this.GetTable<SyncEvent>();
}
