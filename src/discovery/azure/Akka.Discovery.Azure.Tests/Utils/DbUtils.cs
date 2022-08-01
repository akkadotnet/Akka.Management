using System.Threading.Tasks;
using Azure.Data.Tables;

namespace Akka.Discovery.Azure.Tests.Utils
{
    public static class DbUtils
    {
        public static async Task Cleanup(string connectionString)
        {
            var tableClient = new TableServiceClient(connectionString);
            
            await foreach(var table in tableClient.QueryAsync())
            {
                await tableClient.DeleteTableAsync(table.Name);
            }
        }
    }
}