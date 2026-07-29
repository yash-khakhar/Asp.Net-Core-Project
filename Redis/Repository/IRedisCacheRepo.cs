namespace TraineeManagement.api.Redis.Repository
{
    public interface IRedisCacheRepo
    {
        Task SetItem<T>(string cacheKey, T item);

        Task<T?> GetItem<T>(string cacheKey);

        Task RemoveItem(string key);
        Task RemoveByPatternAsync(string pattern);

    }
}
