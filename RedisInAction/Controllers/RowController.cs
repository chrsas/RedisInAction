using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using StackExchange.Redis;

namespace RedisInAction.Controllers {
    public class RowController : ApiController {
        protected IDatabase Redis { get; } = RedisConnectionHelp.Connection.GetDatabase();

        // GET: api/Row
        public IEnumerable<string> Get() {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Row/5
        public string Get(int id) {
            return "value";
        }

        // POST: api/Row
        public void Post([FromBody]string value) {
        }

        // PUT: api/Row/5
        public void Put(int id, [FromUri]int delay) {
            Redis.SortedSetAdd("delay:", id, delay);
            Redis.SortedSetAdd("schedule:", id, DateTimeOffset.Now.ToUnixTimeSeconds());
        }

        // DELETE: api/Row/5
        public void Delete(int id) {
        }
    }
}
