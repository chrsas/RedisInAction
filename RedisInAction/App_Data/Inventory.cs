using System;

namespace RedisInAction.App_Data {
    public class Inventory {
        public string Id { get; set; }
        public string Data { get; set; }
        public long Time { get; set; }

        private Inventory(string id) {
            this.Id = id;
            this.Data = $"data to cache ...{id}";
            this.Time = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public static Inventory Get(string id) {
            return new Inventory(id);
        }
    }
}