// -----------------------------------------------------------------------
//  <copyright file="Util.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace Akka.Coordination.Azure.Tests
{
    public static class Util
    {
        public static async Task Cleanup(string connectionString)
        {
            var blobClient = new BlobServiceClient(connectionString);
            
            await foreach(var container in blobClient.GetBlobContainersAsync())
            {
                await blobClient.DeleteBlobContainerAsync(container.Name);
            }
        }

    }
}