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

        public static void CleanSessions()
        {
            var cache = RedisConnectionHelp.Connection.GetDatabase();
            while (!quit)
            {
                var size = cache.SortedSetLength("recent:");
                if (size <= LIMIT)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                var endIndex = Math.Min(size - LIMIT, 100);
                var tokens = cache.SortedSetRangeByRank("recent:", 0, endIndex - 1);
                var sessionKeys = tokens.Select(t => (RedisKey)$"viewed:{t}").ToArray();
                cache.KeyDelete(sessionKeys);
                cache.HashDelete("login:", tokens);
                cache.SortedSetRemove("recent:", tokens);
            }
        }
    }
}