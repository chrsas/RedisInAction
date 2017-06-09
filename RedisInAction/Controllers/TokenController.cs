using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using StackExchange.Redis;

namespace RedisInAction.Controllers
{
    public class TokenController : ApiController
    {
        protected IDatabase Redis { get; } = RedisConnectionHelp.Connection.GetDatabase();

        // GET: api/Token
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Token/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/Token
        public void Post([FromUri]string token, [FromUri]int userId, [FromUri]string item)
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            Redis.HashSet("login:", token, userId);
            Redis.SortedSetAdd("recent:", token, timestamp);
            if (item == null)
                return;
            Redis.SortedSetAdd($"viewed:{token}", item, timestamp);
            // 只保留最近浏览的25个
            Redis.SortedSetRemoveRangeByRank($"viewed:{token}", 25, -1);
            Redis.SortedSetIncrement("viewed:", item, 1);
        }

        // PUT: api/Token/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Token/5
        public void Delete(int id)
        {
        }

        [HttpGet]
        public IHttpActionResult Check([FromUri]string token)
        {
            return Ok(Redis.HashGet("login:", token));
        }
    }
}
