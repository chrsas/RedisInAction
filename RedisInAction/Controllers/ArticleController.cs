using RedisInAction.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace RedisInAction.Controllers
{
    public class ArticleController : ApiController
    {
        protected IDatabase Redis { get; } = RedisConnectionHelp.Connection.GetDatabase();

        // GET: api/Article
        public IHttpActionResult Get([FromUri]int pageIndex, [FromUri]int pageElements, [FromUri]string order = "score")
        {
            var start = (pageIndex - 1) * pageElements;
            var end = start + pageElements - 1;
            var ids = Redis.SortedSetRangeByRank($"{order}:", start, end, Order.Descending);
            var articles = new List<Article>();
            foreach (var id in ids)
            {
                var article = new Article();
                var articleData = Redis.HashGetAll(id.ToString());
                foreach (var entry in from ad in articleData
                                      join a in article.GetType().GetProperties() on ad.Name.ToString() equals a.Name.ToLower()
                                      select new { a, ad.Value })
                {
                    if (entry.a.PropertyType == typeof(int))
                        entry.a.SetValue(article, int.Parse(entry.Value.ToString()));
                    else
                        entry.a.SetValue(article, entry.Value.ToString());
                }
                articles.Add(article);
            }
            return Ok(articles);
        }

        // GET: api/Article/5
        public string Get(int id)
        {
            return "value2";
        }

        // POST: api/Article
        public IHttpActionResult Post([FromUri]string title, [FromUri]int userId, [FromUri]string link)
        {
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
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Article/5
        public void Delete(int id)
        {
        }

        private const int VOTE_SCORE = 86400 / 200;

        [HttpPost]
        public IHttpActionResult Vote(int id, [FromUri]int userId)
        {
            var articleKey = $"article:{id}";
            if (!Redis.KeyExists(articleKey))
                return BadRequest("article does not exist");
            var cutoff = DateTimeOffset.Now.AddDays(-7).ToUnixTimeSeconds();
            var articleTime = Redis.SortedSetScore("time:", articleKey);
            if (!articleTime.HasValue)
                return BadRequest("article's publish time lost");
            if (cutoff > articleTime.Value)
                return BadRequest("out of time");
            if (!Redis.SetAdd($"voted:{id}", $"user:{userId}"))
                return BadRequest("you have already voted this article");
            Redis.SortedSetIncrement("score:", articleKey, VOTE_SCORE);
            return Ok(Redis.HashIncrement(articleKey, "votes"));
        }

        [HttpPost]
        public IHttpActionResult AddRemoveGroups(int id, [FromUri]string[] toAdd, [FromUri]string[] toRemove)
        {
            var article = $"article:{id}";
            foreach (var item in toAdd)
            {
                Redis.SetAdd($"group:{item}:", article);
            }
            foreach (var item in toRemove)
            {
                Redis.SetRemove($"group:{item}:", article);
            }
            return Ok();
        }

        [HttpGet]
        [ActionName("grouped")]
        public IHttpActionResult GetGroupArticles([FromUri]string group, [FromUri]int pageIndex, [FromUri]int pageElements = 25, [FromUri]string order = "score")
        {
            var key = $"{order}:{group}:";
            if (!Redis.KeyExists(key))
            {
                Redis.SortedSetCombineAndStore(SetOperation.Intersect, key, $"group:{group}:", $"{order}:",
                    Aggregate.Max);
                Redis.KeyExpire(key, new TimeSpan(0, 1, 0));
            }
            return Get(pageIndex, pageElements, key.Substring(0, key.Length - 1));
        }
    }
}
