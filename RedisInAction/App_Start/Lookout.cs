using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;
using RedisInAction.App_Data;
using StackExchange.Redis;

namespace RedisInAction {
    public static class Lookout {
        private static bool quit;

        public static void Quit() {
            quit = true;
        }

        private const int LIMIT = 1000_0000;

        public static void CleanFullSessions() {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            while(!quit) {
                var size = redis.SortedSetLength("recent:");
                if(size <= LIMIT) {
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

        public static void CacheRows() {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            while(!quit) {
                var next = redis.SortedSetRangeByRankWithScores("schedule:", 0, 0).FirstOrDefault();
                var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
                if(!next.Element.HasValue || next.Score > timestamp) {
                    Thread.Sleep(50);
                    continue;
                }
                var delay = redis.SortedSetScore("delay:", next.Element);
                if(delay == null || delay < 0) {
                    redis.SortedSetRemove("delay:", next.Element);
                    redis.SortedSetRemove("schedule:", next.Element);
                    redis.KeyDelete($"inv:{next.Element}");
                    continue;
                }
                var row = Inventory.Get(next.Element);
                redis.SortedSetAdd("schedule:", next.Element, timestamp + delay.Value);
                redis.StringSet($"inv:{next.Element}", JsonConvert.SerializeObject(row));
            }
        }

        public static void RescaleViewed() {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            while(!quit) {
                // 删除所有排名在20000名之后的商品
                redis.SortedSetRemoveRangeByRank("viewed:", 20000, -1);
                redis.SortedSetCombineAndStore(SetOperation.Intersect, "viewed:", new RedisKey[] { "viewed:" }, new[] { 0.5 });
                Thread.Sleep(new TimeSpan(0, 5, 0));
            }
        }
    }
}