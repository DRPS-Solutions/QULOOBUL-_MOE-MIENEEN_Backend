using Azure.Storage.Sas;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace QULOOBUL__MOE_MIENEEN_Backend.Services
{
    /// <summary>
    /// Thin wrapper over Azure Blob Storage. Containers are private; every read or write
    /// happens through a short-lived SAS scoped to a single blob, so the file itself never
    /// passes through the API (architecture doc, SD-04 / "Network segregation").
    /// No provider abstraction on purpose (architecture doc rejects "Abstract Factory over storage").
    /// </summary>
    public class BlobStorageService(BlobServiceClient client)
    {
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);

        /// <summary>A single-blob SAS the client uses to PUT the bytes straight to Blob Storage.</summary>
        public async Task<Uri> CreateUploadSasAsync(string container, string blobName, TimeSpan? ttl = null)
        {
            var blob = await GetBlobClientAsync(container, blobName);
            return Sas(blob, BlobSasPermissions.Create | BlobSasPermissions.Write, ttl);
        }

        /// <summary>A single-blob read SAS handed to a client that needs to download the file.</summary>
        public async Task<Uri> CreateReadSasAsync(string container, string blobName, TimeSpan? ttl = null)
        {
            var blob = await GetBlobClientAsync(container, blobName);
            return Sas(blob, BlobSasPermissions.Read, ttl);
        }

        public async Task DeleteAsync(string container, string blobName)
        {
            var blob = await GetBlobClientAsync(container, blobName);
            await blob.DeleteIfExistsAsync();
        }

        private async Task<BlobClient> GetBlobClientAsync(string container, string blobName)
        {
            var c = client.GetBlobContainerClient(container);
            await c.CreateIfNotExistsAsync(PublicAccessType.None);
            return c.GetBlobClient(blobName);
        }

        private static Uri Sas(BlobClient blob, BlobSasPermissions permissions, TimeSpan? ttl)
        {
            if (!blob.CanGenerateSasUri)
                throw new InvalidOperationException(
                    "BlobServiceClient has no shared key. Use a connection string with an account key, " +
                    "or switch to a user-delegation SAS when moving to managed identity.");

            var builder = new BlobSasBuilder
            {
                BlobContainerName = blob.BlobContainerName,
                BlobName = blob.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(ttl ?? DefaultTtl),
            };
            builder.SetPermissions(permissions);
            return blob.GenerateSasUri(builder);
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//