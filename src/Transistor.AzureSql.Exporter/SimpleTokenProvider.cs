using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.SqlServer.Dac;

namespace Transistor.Database.Tool
{
    internal class SimpleTokenProvider : IUniversalAuthProvider
    {
        private readonly string _directoryId;

        private readonly AzureServiceTokenProvider _provider;

        public SimpleTokenProvider(string directoryId)
        {
            _directoryId = directoryId;
            _provider = new AzureServiceTokenProvider();
        }

        /// <summary>
        /// Might need to plumb this in later
        /// </summary>
        /// <returns></returns>
        public string GetValidAccessToken()
        {
            if (string.IsNullOrEmpty(_directoryId))
            {
                return _provider
                    .GetAccessTokenAsync("https://database.windows.net/")
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                return _provider
                    .GetAccessTokenAsync("https://database.windows.net/", _directoryId)
                    .GetAwaiter()
                    .GetResult();
            }
        }
    }
}