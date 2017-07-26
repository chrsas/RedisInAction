using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using StackExchange.Redis;

namespace RedisInAction.Controllers {
    public class CounterController : ApiController {
        internal IDatabase Redis { get; set; } = RedisConnectionHelp.Connection.GetDatabase();

        public static int[] Precision = { 1, 5, 60, 300, 3600, 18000, 86400 };

        // GET: api/Counter
        public IEnumerable<KeyValuePair<string, string>> Get([FromUri]string name, [FromUri]int precision) {
            var hash = $"{precision}:{name}";
            var data = Redis.HashGetAll($"count:{hash}");
            return data.Select(hashEntry => new KeyValuePair<string, string>(hashEntry.Name, hashEntry.Value));
        }

        // GET: api/Counter/5
        public string Get(int id) {
            return "value";
        }

        // POST: api/Counter
        public void Post([FromUri]string name, [FromUri]int count = 1, long? now = null) {
            now = now ?? DateTimeOffset.Now.ToUnixTimeSeconds();
            var trans = Redis.CreateTransaction();
            foreach(var prec in Precision) {
                var pnow = now / prec * prec;
                var hash = $"{prec}:{name}";
                trans.SortedSetAddAsync("known:", hash, 0);
                trans.HashIncrementAsync($"count:{hash}", pnow, count);
            }
            trans.Execute();
        }

        // PUT: api/Counter/5
        public void Put(int id, [FromBody]string value) {
        }

        // DELETE: api/Counter/5
        public void Delete(int id) {
        }
    }
}
