using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoskiTGBot2024.Services
{
    static class RedisService
    {
        private static ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:90");
        private static IDatabase redisDb = redis.GetDatabase();

        public static async Task<bool> IsStringExist(string key)
        {
            return await redisDb.StringGetAsync(key) != RedisValue.Null;
        }

        public static async Task<bool> SetValue(string key, string value, TimeSpan RateLimitPeriod)
        {
            return await redisDb.StringSetAsync(key, value, RateLimitPeriod);
        }
    }
}
