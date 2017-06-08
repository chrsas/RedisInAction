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
        protected IDatabase Cache { get; } = RedisConnectionHelp.Connection.GetDatabase();

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
        public void Post([FromBody]string value)
        {
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
            return Ok(Cache.HashGet("login:", token));
        }
    }
}
