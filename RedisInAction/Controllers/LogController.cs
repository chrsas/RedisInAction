using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using StackExchange.Redis;

namespace RedisInAction.Controllers {

    public class LogController : ApiController {
        internal IDatabase Redis { get; set; } = RedisConnectionHelp.Connection.GetDatabase();
        public const string DEBUG = "debug";
        public const string INFO = "info";
        public const string WARNING = "warning";
        public const string ERROR = "error";
        public const string CRITICAL = "critical";

        // GET: api/Log
        public IEnumerable<string> Get() {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Log/5
        public string Get(int id) {
            return "value";
        }

        // POST: api/Log
        public void Post([FromBody]string name, [FromBody]string message, [FromBody]string severity = INFO, IBatch pipeline = null) {
            var destination = $"recent:{name}:{severity}";
            var batch = pipeline ?? Redis.CreateBatch();
            batch.ListLeftPushAsync(destination, $"{DateTime.Now} {message}");
            batch.ListTrimAsync(destination, 0, 99);
            batch.Execute();
        }

        [HttpPost]
        public void Common([FromUri]string name, [FromBody]string message, [FromUri]string severity = INFO, [FromUri]int timeOut = 5) {
            var commonDest = $"common:{name}:{severity}";
            var startKey = $"{commonDest}:start";
            var end = DateTime.Now.AddMilliseconds(timeOut);
            while(DateTime.Now < end) {
                var hourStart = DateTime.Now.ToString("yyyy-MM-dd HH:00");
                var existing = Redis.StringGet(startKey);
                var trans = Redis.CreateTransaction();
                trans.AddCondition(Condition.StringEqual(startKey, existing));
                if(existing.HasValue && DateTime.Parse(existing) < DateTime.Parse(hourStart)) {
                    trans.KeyRenameAsync(commonDest, $"{commonDest}:last");
                    trans.KeyRenameAsync(startKey, $"{commonDest}:pstart");
                    trans.StringSetAsync(startKey, hourStart);
                } else if(!existing.HasValue)
                    trans.StringSetAsync(startKey, hourStart);
                trans.SortedSetIncrementAsync(commonDest, message, 1);
                var recentDest = $"recent:{name}:{severity}";
                trans.ListLeftPushAsync(recentDest, $"{DateTime.Now} {message}");
                trans.ListTrimAsync(recentDest, 0, 99);
                if(trans.Execute())
                    break;
            }
        }

        [HttpPost]
        public void Test([FromBody]string name, [FromBody]string message) {

        }

        // PUT: api/Log/5
        public void Put(int id, [FromBody]string value) {
        }

        // DELETE: api/Log/5
        public void Delete(int id) {
        }
    }
}
