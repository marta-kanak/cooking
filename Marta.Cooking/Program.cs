using System;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Marta.Thermomix;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.ServiceBus;

namespace Marta.Cooking
{
    class Program
    {
        protected internal static DocumentClient DocumentClient;
        protected internal static HttpClient HttpClient;

        protected internal static string DatabaseId;
        protected internal static string CollectionName;
        protected internal static string BearerToken;
        protected internal static string UriString;
        protected internal static string AuthKeyOrResourceToken;
        protected internal static string QueueConnectionString;
        protected internal static string QueueName;
        protected internal static string RecipesUrl;
        protected internal static string RecipeDetailsUrl;

        public static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            BearerToken = configuration.GetSection("authentication:bearer").Value;
            UriString = configuration.GetSection("cosmosdb:endpoint").Value;
            AuthKeyOrResourceToken = configuration.GetSection("cosmosdb:masterkey").Value;
            DatabaseId = configuration.GetSection("cosmosdb:database").Value;
            CollectionName = configuration.GetSection("cosmosdb:collection").Value;
            QueueConnectionString = configuration.GetSection("queue:connectionString").Value;
            QueueName = configuration.GetSection("queue:name").Value;
            RecipesUrl = configuration.GetSection("cookingPlatform:recipesUrl").Value;
            RecipeDetailsUrl = configuration.GetSection("cookingPlatform:recipeDetailsUrl").Value;

            HttpClient = new HttpClient();
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BearerToken);

            Console.WriteLine("Start");
            Console.WriteLine("Fetching all recipe ids to the queue");

            DumpRecipeIdsAsync().Wait();

            Console.WriteLine("Recipe ids dumped successfully");
            Console.WriteLine("Fetching all recipe details to the document db");

            DumpRecipeDetailsAsync().Wait();

            Console.WriteLine("Recipe details dumped successfully");
            Console.WriteLine("End");

            Console.ReadKey();
        }

        static async Task DumpRecipeIdsAsync()
        {
            var queueClient = new QueueClient(QueueConnectionString, QueueName);

            // todo fetch page count
            for (int page = 1; page <= 25; page++)
            {
                var ids = await CookingService.GetRecipesIdsAsync(page, HttpClient, RecipesUrl);
                var msgs = ids.Select(x => new Message(Encoding.UTF8.GetBytes(x))).ToList();

                await queueClient.SendAsync(msgs);
            }

            await queueClient.CloseAsync();
        }

        static async Task DumpRecipeDetailsAsync()
        {
            var queueClient = new QueueClient(QueueConnectionString, QueueName);
            DocumentClient = new DocumentClient(new Uri(UriString), AuthKeyOrResourceToken);

            var databaseDefinition = new Database { Id = DatabaseId };
            await DocumentClient.CreateDatabaseIfNotExistsAsync(databaseDefinition);

            var collectionDefinition = new DocumentCollection { Id = CollectionName };
            await DocumentClient.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(DatabaseId), collectionDefinition);

            queueClient.RegisterMessageHandler(ProcessMsgAsync, new MessageHandlerOptions(ExceptionReceivedHandler) { AutoComplete = true });
        }

        private static async Task ProcessMsgAsync(Message message, CancellationToken cancellationToken)
        {
            var id = Encoding.UTF8.GetString(message.Body);

            var recipe = await CookingService.GetRecipeDetailsAsync(id, HttpClient, RecipeDetailsUrl);
            await DocumentClient.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionName), recipe, cancellationToken: cancellationToken);
        }

        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            Console.WriteLine($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            Console.WriteLine("Exception context for troubleshooting:");
            Console.WriteLine($"- Endpoint: {context.Endpoint}");
            Console.WriteLine($"- Entity Path: {context.EntityPath}");
            Console.WriteLine($"- Executing Action: {context.Action}");
            return Task.CompletedTask;
        }
    }
}
