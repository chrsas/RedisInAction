using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Mono.Csv;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace RedisInAction.Controllers
{
    public class LocationController : ApiController
    {
        protected IDatabase Redis { get; } = RedisConnectionHelp.Connection.GetDatabase();

        private long IpToScore(string ipAddress) => ipAddress.Split('.')
            .Aggregate(0L, (workingSentence, next) => workingSentence * 256 + int.Parse(next));

        private string ScoreToIp(long ipScore)
        {
            var ip = string.Empty;
            var i = 4;
            while (i > 0)
            {
                i--;
                ip = $"{ipScore % 256}.{ip}";
                ipScore = ipScore / 256;
            }
            return ip.Substring(0, ip.Length - 1);
        }

        private static string GetLocalFile(string fileName) => $"{System.Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\downloads\\{fileName}";

        [HttpPost]
        public void ImportIps([FromUri] string fileName = "GeoLiteCity-Blocks.csv")
        {
            var row = new List<string>();
            using (var reader = new CsvFileReader(GetLocalFile(fileName)))
            {
                var count = 0;
                var batch = Redis.CreateBatch();
                while (reader.ReadRow(row))
                {
                    var startIp = row.Any() ? row[0] : string.Empty;
                    if (startIp.ToLower().Contains('i'))
                        continue;
                    long score;
                    if (startIp.Contains('.'))
                        score = IpToScore(startIp);
                    else if (!long.TryParse(startIp, out score))
                        continue;
                    var cityId = $"{row[2]}_{count}";
                    batch.SortedSetAddAsync("ip2cityid:", cityId, score);
                    count++;
                    if (count % 1000 == 0)
                        batch.Execute();
                }
                batch.Execute();
            }
        }

        [HttpPost]
        public void ImportCities([FromUri] string fileName = "GeoLiteCity-Location.csv")
        {
            var row = new List<string>();
            var batch = Redis.CreateBatch();
            using (var reader = new CsvFileReader(GetLocalFile(fileName)))
            {
                var count = 0;
                while (reader.ReadRow(row))
                {
                    if (row.Count < 4 || !char.IsDigit(row[0][0]))
                        continue;
                    var cityId = row[0];
                    var country = row[1];
                    var region = row[2];
                    var city = row[3];
                    var json = JsonConvert.SerializeObject(new { city, region, country });
                    batch.HashSetAsync("cityid2city:", cityId, json);
                    count++;
                    if (count % 1000 == 0)
                        batch.Execute();
                }
                batch.Execute();
            }
        }

        [HttpGet]
        [ActionName("City")]
        public IHttpActionResult FindCityByIp([FromUri]string ipAddress)
        {
            var score = IpToScore(ipAddress);
            var result = Redis.SortedSetRangeByScore("ip2cityid:", 0, score, order: Order.Descending, take: 1).FirstOrDefault();
            if (result.IsNullOrEmpty)
                return Ok();
            var cityId = result.ToString().Substring(0, result.ToString().IndexOf('_'));
            return Ok(Redis.HashGet("cityid2city:", cityId));
        }
    }
}
