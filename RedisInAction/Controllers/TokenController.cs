using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using StackExchange.Redis;

namespace RedisInAction.Controllers {
    public class TokenController : ApiController {
        protected IDatabase Redis { get; } = RedisConnectionHelp.Connection.GetDatabase();

        // GET: api/Token
        public IEnumerable<string> Get() {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Token/5
        public string Get(int id) {
            return "value";
        }

        // POST: api/Token
        public void Post([FromUri]string token, [FromUri]int userId, [FromUri]string item) {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            Redis.HashSet("login:", token, userId);
            Redis.SortedSetAdd("recent:", token, timestamp);
            if(item == null)
                return;
            Redis.SortedSetAdd($"viewed:{token}", item, timestamp);
            // 只保留最近浏览的25个
            Redis.SortedSetRemoveRangeByRank($"viewed:{token}", 25, -1);
            Redis.SortedSetIncrement("viewed:", item, 1);
        }

        [HttpPost]
        // POST: api/Token/Batch
        public void Batch([FromUri]string token, [FromUri]int userId, [FromUri]string item) {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var batch = Redis.CreateBatch();
            batch.HashSetAsync("login:", token, userId);
            batch.SortedSetAddAsync("recent:", token, timestamp);
            if(item == null)
                return;
            batch.SortedSetAddAsync($"viewed:{token}", item, timestamp);
            // 只保留最近浏览的25个
            batch.SortedSetRemoveRangeByRankAsync($"viewed:{token}", 25, -1);
            batch.SortedSetIncrementAsync("viewed:", item, 1);
            batch.Execute();
        }

        // PUT: api/Token/5
        public void Put(int id, [FromBody]string value) {
        }

        // DELETE: api/Token/5
        public void Delete(int id) {
        }

        [HttpGet]
        public IHttpActionResult Check([FromUri]string token) {
            return Ok(Redis.HashGet("login:", token));
        }

        [HttpGet]
        public IHttpActionResult BenchmarkUpdate([FromUri]int duration) {
            var methodes = new List<Tuple<Action<string, int, string>, string>>
            {
                new Tuple<Action<string, int, string>, string>(Post, nameof(this.Post)),
                new Tuple<Action<string, int, string>, string>(Batch, nameof(this.Batch))
            };
            var strList = new List<string>();
            foreach(var method in methodes) {
                var count = 0;
                var start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                var end = start + duration * 1000;
                while(DateTimeOffset.Now.ToUnixTimeMilliseconds() < end) {
                    count++;
                    method.Item1.Invoke("token", 111, "item");
                }
                var delta = DateTimeOffset.Now.ToUnixTimeMilliseconds() - start;
                strList.Add($"{method.Item2}, {count}, {delta}, {count / (delta / 1000)}");
            }
            return Ok(strList);
        }
    }
}
