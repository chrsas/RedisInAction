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
        public void Post([FromUri]string name, [FromUri]string message, [FromUri]string severity = INFO, IBatch pipeline = null) {
            var destination = $"recent:{name}:{severity}";
            message = $"{DateTime.Now} {message}";
            var batch = pipeline ?? Redis.CreateBatch();
            batch.ListLeftPushAsync(destination, message);
            batch.ListTrimAsync(destination, 0, 99);
            batch.Execute();
        }

        // PUT: api/Log/5
        public void Put(int id, [FromBody]string value) {
        }

        // DELETE: api/Log/5
        public void Delete(int id) {
        }
    }
}
