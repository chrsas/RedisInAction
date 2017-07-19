using System;
using System.Configuration;
using StackExchange.Redis;

namespace RedisInAction {
    public class RedisConnectionHelp {
        private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new Lazy<ConnectionMultiplexer>(() => {
            var cacheConnection = ConfigurationManager.AppSettings["CacheConnection"];
            return ConnectionMultiplexer.Connect(cacheConnection);
        });

        public static ConnectionMultiplexer Connection => LazyConnection.Value;
    }
}