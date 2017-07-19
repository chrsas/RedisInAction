using RedisInAction.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace RedisInAction.Controllers {
    public class ArticleController : ApiController {
        protected IDatabase Redis { get; } = RedisConnectionHelp.Connection.GetDatabase();

        // GET: api/Article
        public IHttpActionResult Get([FromUri]int pageIndex, [FromUri]int pageElements, [FromUri]string order = "score") {
            var start = (pageIndex - 1) * pageElements;
            var end = start + pageElements - 1;
            var ids = Redis.SortedSetRangeByRank($"{order}:", start, end, Order.Descending);
            if(!ids.Any())
                return Ok();
            var articles = new List<Article>();
            var transaction = Redis.CreateTransaction();
            var artcilesData = ids.Select(id => transaction.HashGetAllAsync(id.ToString())).ToArray();
            if(!transaction.Execute())
                return BadRequest("数据库发生异常");
            foreach(var artcileData in artcilesData) {
                var article = new Article();
                foreach(var entry in from ad in artcileData.Result
                                     join a in article.GetType().GetProperties() on ad.Name.ToString() equals a.Name.ToLower()
                                     select new { a, ad.Value }) {
                    if(entry.a.PropertyType == typeof(int))
                        entry.a.SetValue(article, int.Parse(entry.Value.ToString()));
                    else
                        entry.a.SetValue(article, entry.Value.ToString());
                }
                articles.Add(article);
            }
            return Ok(articles);
        }

        // GET: api/Article/5
        public string Get(int id) {
            return "value2";
        }

        // POST: api/Article
        public IHttpActionResult Post([FromUri]string title, [FromUri]int userId, [FromUri]string link) {
            var articleId = Redis.StringIncrement("article:");
            var voted = $"voted:{articleId}";
            Redis.SetAdd(voted, $"user:{userId}");
            Redis.KeyExpire(voted, new TimeSpan(7, 0, 0, 0));
            var publishTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            var article = $"article:{articleId}";
            Redis.HashSet(article, new[]
            {
                new HashEntry("id", articleId),
                new HashEntry("title", title),
                new HashEntry("link", link),
                new HashEntry("poster", $"user:{userId}"),
                new HashEntry("time", publishTime),
                new HashEntry("votes", 1),
            });
            Redis.SortedSetAdd("score:", article, publishTime + VOTE_SCORE);
            Redis.SortedSetAdd("time:", article, publishTime);
            return Ok(articleId);
        }

        // PUT: api/Article/5
        public void Put(int id, [FromBody]string value) {
        }

        // DELETE: api/Article/5
        public void Delete(int id) {
        }

        private const int VOTE_SCORE = 86400 / 200;

        [HttpPost]
        public async Task<IHttpActionResult> Vote(int id, [FromUri]int userId) {
            var articleKey = $"article:{id}";
            if(!await Redis.KeyExistsAsync(articleKey))
                return BadRequest("article does not exist");
            var cutoff = DateTimeOffset.Now.AddDays(-7).ToUnixTimeSeconds();
            var articleTime = await Redis.SortedSetScoreAsync("time:", articleKey);
            if(!articleTime.HasValue)
                return BadRequest("article's publish time lost");
            if(cutoff > articleTime.Value)
                return BadRequest("out of time");
            var voted = $"voted:{id}";
            var trans = Redis.CreateTransaction();
            var user = $"user:{userId}";
            Task<long> votedResult = null;
            while(cutoff < articleTime.Value) {
                var currentVotedLength = trans.SetLengthAsync(voted);
                var isMember = trans.SetContainsAsync(voted, user);
                await trans.ExecuteAsync();
                if(isMember.Result)
                    return BadRequest("you have already voted this article");
                trans.AddCondition(Condition.SetLengthEqual(voted, currentVotedLength.Result));
#pragma warning disable CS4014 
                trans.SetAddAsync(voted, user);
                // 为文章的投票设置过期时间，防止过多的投票结果用完内存
                trans.KeyExpireAsync($"voted:{id}", new TimeSpan(0, 0, (int)(articleTime.Value - cutoff)));
                trans.SortedSetIncrementAsync("score:", articleKey, VOTE_SCORE);
#pragma warning restore CS4014
                votedResult = trans.HashIncrementAsync(articleKey, "votes");
                if(await trans.ExecuteAsync())
                    break;
            }
            if(votedResult?.Status == TaskStatus.RanToCompletion)
                return Ok((decimal)votedResult.Result);
            return BadRequest("Time out");
        }

        [HttpPost]
        public IHttpActionResult AddRemoveGroups(int id, [FromUri]string[] toAdd, [FromUri]string[] toRemove) {
            var article = $"article:{id}";
            foreach(var item in toAdd) {
                Redis.SetAdd($"group:{item}:", article);
            }
            foreach(var item in toRemove) {
                Redis.SetRemove($"group:{item}:", article);
            }
            return Ok();
        }

        [HttpGet]
        [ActionName("grouped")]
        public IHttpActionResult GetGroupArticles([FromUri]string group, [FromUri]int pageIndex, [FromUri]int pageElements = 25, [FromUri]string order = "score") {
            var key = $"{order}:{group}:";
            if(!Redis.KeyExists(key)) {
                Redis.SortedSetCombineAndStore(SetOperation.Intersect, key, $"group:{group}:", $"{order}:",
                    Aggregate.Max);
                Redis.KeyExpire(key, new TimeSpan(0, 1, 0));
            }
            return Get(pageIndex, pageElements, key.Substring(0, key.Length - 1));
        }
    }
}
