using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using StackExchange.Redis;

namespace RedisInAction.App_Start
{
    public class CartController : ApiController
    {
        protected IDatabase Cache { get; } = RedisConnectionHelp.Connection.GetDatabase();
        // GET: api/Cart
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Cart/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/Cart
        public void Post([FromUri]string session,[FromUri]string item, [FromUri]int count)
        {
            if (count <= 0)
                Cache.HashDelete($"cart:{session}", item);
            else
                Cache.HashSet($"cart:{session}", item, count);
        }

        // PUT: api/Cart/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Cart/5
        public void Delete(int id)
        {
        }
    }
}
