using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using StackExchange.Redis;

namespace RedisInAction.Controllers
{
    public class CommodityController : ApiController
    {
        protected IDatabase Redis { get; } = RedisConnectionHelp.Connection.GetDatabase();

        // GET: api/Commodity
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/Commodity/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/Commodity
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/Commodity/Publish
        [HttpPost]
        public async Task<IHttpActionResult> Publish([FromUri]string commodityName, [FromUri]int sellerid, decimal price)
        {
            var inventory = $"inventory:{sellerid}";
            var commodity = $"{commodityName},{sellerid}";
            var end = DateTime.Now.AddSeconds(5);
            var trans = Redis.CreateTransaction();
            var added = false;
            while (DateTime.Now < end)
            {
                var commodityCount = trans.SetLengthAsync(inventory);
                var isOwned = trans.SetContainsAsync(inventory, commodityName);
                await trans.ExecuteAsync();
                if (!isOwned.Result)
                    return BadRequest("Commodity is not yours");
                trans.AddCondition(Condition.SetLengthEqual(inventory, commodityCount.Result));
#pragma warning disable 4014
                trans.SortedSetAddAsync("market:", commodity, (double)price);
                trans.SetRemoveAsync(inventory, commodityName);
#pragma warning restore 4014
                if (await trans.ExecuteAsync())
                    added = true;
                break;
            }
            if (added)
                return Ok();
            return BadRequest("out of time");
        }

        // DELETE: api/Commodity/5
        public void Delete(int id)
        {
        }
    }
}
