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
        public async Task<IHttpActionResult> Publish([FromUri]string commodityName, [FromUri]int sellerId, decimal price)
        {
            var inventory = $"inventory:{sellerId}";
            var commodity = $"{commodityName}.{sellerId}";
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

        [HttpPost]
        public async Task<IHttpActionResult> Purchase([FromUri]string commodityName, [FromUri]int buyerId, [FromUri]int sellerId, [FromUri]decimal price)
        {
            var buyer = $"user:{buyerId}";
            var seller = $"user:{sellerId}";
            var marketCommodity = $"{commodityName}.{sellerId}";
            var inventory = $"inventory:{buyerId}";
            var end = DateTime.Now.AddSeconds(10);
            var trans = Redis.CreateTransaction();
            Task<double> leftFunds = null;
            while (DateTime.Now < end)
            {
                var marketCommodityCount = trans.SortedSetLengthAsync("market:");
                var currentPrice = trans.SortedSetScoreAsync("market:", marketCommodity);
                var buyerFunds = trans.HashGetAsync(buyer, "funds");
                await trans.ExecuteAsync();
                trans.AddCondition(Condition.SortedSetLengthEqual("market:", marketCommodityCount.Result));
                trans.AddCondition(Condition.HashEqual(buyer, "funds", buyerFunds.Result));
                if (currentPrice.Result == null)
                    return BadRequest("Commodity has sold");
                if (price != (decimal)currentPrice.Result.Value)
                    return BadRequest("Commodity price changed");
                if (currentPrice.Result.Value > (double)buyerFunds.Result)
                    return BadRequest("Funds is not enough");
#pragma warning disable 4014
                trans.HashIncrementAsync(seller, "funds", (double)price);
                leftFunds = trans.HashDecrementAsync(buyer, "funds", (double)price);
                trans.SetAddAsync(inventory, commodityName);
                trans.SortedSetRemoveAsync("market:", marketCommodity);
#pragma warning restore 4014
                if (await trans.ExecuteAsync())
                    break;
            }
            if (leftFunds?.Status == TaskStatus.RanToCompletion)
                return Ok((decimal)leftFunds.Result);
            return BadRequest("Time out");
        }

        // DELETE: api/Commodity/5
        public void Delete(int id)
        {
        }
    }
}
