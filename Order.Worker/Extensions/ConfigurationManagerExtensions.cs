namespace Order.Worker.Extensions;

public static class ConfigurationManagerExtensions
{
    extension(ConfigurationManager configuration)
    {
        public T GetValueByKey<T>(string key)
        {
            return configuration.GetValue<T>(key) 
                   ?? throw new InvalidOperationException($"{key} not set");
        }
    }
}