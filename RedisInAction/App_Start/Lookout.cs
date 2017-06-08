using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using StackExchange.Redis;

namespace RedisInAction
{
    public static class Lookout
    {
        private static bool quit;

        public static void Quit()
        {
            quit = true;
        }

        private const int LIMIT = 1000_0000;

        public static void CleanFullSessions()
        {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            while (!quit)
            {
                var size = redis.SortedSetLength("recent:");
                if (size <= LIMIT)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                var endIndex = Math.Min(size - LIMIT, 100);
                var tokens = redis.SortedSetRangeByRank("recent:", 0, endIndex - 1);
                var sessionKeys = tokens.Select(t => (RedisKey)$"viewed:{t}").
                    Union(tokens.Select(t => (RedisKey)$"cart:{t}")).ToArray();
                redis.KeyDelete(sessionKeys);
                redis.HashDelete("login:", tokens);
                redis.SortedSetRemove("recent:", tokens);
            }
        }
    }
}