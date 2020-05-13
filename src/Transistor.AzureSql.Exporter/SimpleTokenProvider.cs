using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Identity.Client;
using Microsoft.SqlServer.Dac;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Transistor.Database.Tool
{
    internal class SimpleTokenProvider : IUniversalAuthProvider
    {
        private readonly string _directoryId;

        private readonly AzureServiceTokenProvider _provider;

        private const string ClientId = "16bd1103-75d6-46e9-8a58-8b29c5251504";

        private const string Authority = "https://login.microsoftonline.com/4df717ef-83fa-49ed-a61d-e08c07d1414f";

        private const string Resource = "https://database.windows.net/";

        public AuthenticationResult CodeFlowResult { get; set; }

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
            string accessToken;

            // I'd rather not have a manual cache of the codeflow, but for some reason the internal azure cache isn't working?
            if (CodeFlowResult != null)
            {
                return CodeFlowResult.AccessToken;
            }

            try
            {
                accessToken = NonInteractiveCodeFlow();
                return accessToken;
            }
            catch (AzureServiceTokenProviderException)
            {
                Console.WriteLine("Could not aquire token from Az cli");
                Console.WriteLine("Login with the az cli so you don't need to execute device code flow");
            }

            accessToken = InteractiveDeviceCodeFlow()
                .GetAwaiter()
                .GetResult();

            return accessToken;
        }

        private string NonInteractiveCodeFlow()
        {
            if (string.IsNullOrEmpty(_directoryId))
            {
                return _provider
                    .GetAccessTokenAsync(Resource)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                return _provider
                    .GetAccessTokenAsync(Resource, _directoryId)
                    .GetAwaiter()
                    .GetResult();
            }
        }

        private async Task<string> InteractiveDeviceCodeFlow()
        {
            IPublicClientApplication pca = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(Authority)
                .WithDefaultRedirectUri()
                .Build();

            var accounts = await pca.GetAccountsAsync();

            CodeFlowResult = await pca.AcquireTokenWithDeviceCode(new List<string> { "https://database.windows.net//.default" },
                deviceCodeResult =>
                {
                    // This will print the message on the console which tells the user where to go sign-in using
                    // a separate browser and the code to enter once they sign in.
                    // The AcquireTokenWithDeviceCode() method will poll the server after firing this
                    // device code callback to look for the successful login of the user via that browser.
                    // This background polling (whose interval and timeout data is also provided as fields in the
                    // deviceCodeCallback class) will occur until:
                    // * The user has successfully logged in via browser and entered the proper code
                    // * The timeout specified by the server for the lifetime of this code (typically ~15 minutes) has been reached
                    // * The developing application calls the Cancel() method on a CancellationToken sent into the method.
                    //   If this occurs, an OperationCanceledException will be thrown (see catch below for more details).
                    Console.WriteLine(deviceCodeResult.Message);
                    return Task.FromResult(0);
                }).ExecuteAsync();

            Console.WriteLine("Token aquired");
            return CodeFlowResult.AccessToken;
        }
    }
}