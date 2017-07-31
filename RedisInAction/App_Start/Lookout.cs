using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;
using RedisInAction.App_Data;
using RedisInAction.Controllers;
using StackExchange.Redis;

namespace RedisInAction {
    public static class Lookout {
        private static bool quit;

        public static void Quit() {
            quit = true;
        }

        public const int LIMIT = 1000_0000;
        public const int SAMPLE_COUNT = 100;

        public static void CleanFullSessions() {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            while(!quit) {
                try {
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
                } catch(RedisTimeoutException e) {
                    new LogController { Redis = redis }.Post(nameof(RedisTimeoutException), e.Message);
                }
            }
        }

        public static void CacheRows() {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            while(!quit) {
                try {
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
                } catch(RedisTimeoutException e) {
                    new LogController { Redis = redis }.Post(nameof(RedisTimeoutException), e.Message);
                }
            }
        }

        public static void RescaleViewed() {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            while(!quit) {
                // 删除所有排名在20000名之后的商品
                try {
                    redis.SortedSetRemoveRangeByRank("viewed:", 20000, -1);
                    redis.SortedSetCombineAndStore(SetOperation.Intersect, "viewed:", new RedisKey[] { "viewed:" }, new[] { 0.5 });
                    Thread.Sleep(new TimeSpan(0, 5, 0));
                } catch(RedisTimeoutException e) {
                    new LogController { Redis = redis }.Post(nameof(RedisTimeoutException), e.Message);
                }
            }
        }

        public static void CleanCounters() {
            var redis = RedisConnectionHelp.Connection.GetDatabase();
            var passes = default(int);
            while(!quit) {
                var start = DateTime.Now;
                var index = default(int);
                while(index < redis.SortedSetLength("known:")) {
                    var hash = redis.SortedSetRangeByRank("known:", index, index).FirstOrDefault();
                    index++;
                    if(hash.IsNullOrEmpty)
                        break;
                    var prec = int.Parse(hash.ToString().Split(':').First());
                    var bprec = Math.Max((int)Math.Floor(prec / 60.0), 1);
                    if(passes % bprec != 0)
                        continue;
                    var hkey = $"count:{hash}";
                    var cutoff = DateTimeOffset.Now.AddSeconds(SAMPLE_COUNT * prec).ToUnixTimeSeconds();
                    var samples = redis.HashKeys(hkey).Select(rv => (int)rv).OrderBy(rv => rv);
                    var removeItems = samples.Where(s => s <= cutoff).Select(s => (RedisValue)s).ToArray();
                    if(removeItems.Any()) {
                        redis.HashDelete(hkey, removeItems);
                        if(removeItems.Length == samples.Count()) {
                            if(!redis.KeyExists(hkey)) {
                                var trans = redis.CreateTransaction();
                                trans.AddCondition(Condition.KeyNotExists(hkey));
                                trans.SortedSetRemoveAsync("known:", hash);
                                trans.Execute();
                                index--;
                            }
                        }
                    }
                    passes++;
                    var duration = Math.Min((int)(DateTime.Now - start).TotalSeconds, 60);
                    Thread.Sleep(Math.Max(duration, 1) * 1000);
                }
            }
        }
    }
}