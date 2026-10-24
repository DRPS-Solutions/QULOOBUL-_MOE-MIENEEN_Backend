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
    /// Reference: Microsoft, "Grant limited access to data with shared access signatures (SAS)." Accessed: Aug. 08, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/storage/common/storage-sas-overview
    /// </summary>
    public class BlobStorageService(BlobServiceClient client)
    {
        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>A single-blob SAS the client uses to PUT the bytes straight to Blob Storage.</summary>
        public async Task<Uri> CreateUploadSasAsync(string container, string blobName, TimeSpan? ttl = null)
        {
            var blob = await GetBlobClientAsync(container, blobName);
            return Sas(blob, BlobSasPermissions.Create | BlobSasPermissions.Write, ttl);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>A single-blob read SAS handed to a client that needs to download the file. With a
        /// <paramref name="downloadName"/>, the link saves the file under that name instead of opening it.</summary>
        public async Task<Uri> CreateReadSasAsync(string container, string blobName, TimeSpan? ttl = null, string? downloadName = null)
        {
            var blob = await GetBlobClientAsync(container, blobName);
            return Sas(blob, BlobSasPermissions.Read, ttl, downloadName);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>Writes a file the API itself produced (a report export), never client bytes.</summary>
        public async Task UploadAsync(string container, string blobName, BinaryData content)
        {
            var blob = await GetBlobClientAsync(container, blobName);
            await blob.UploadAsync(content, overwrite: true);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Deletes a blob if it exists. This is the only way to remove a file from storage; there is no "delete SAS".
        /// </summary>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        public virtual async Task DeleteAsync(string container, string blobName)
        {
            var blob = await GetBlobClientAsync(container, blobName);
            await blob.DeleteIfExistsAsync();
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Names of blobs bigger than <paramref name="maxBytes"/>. A SAS cannot cap the size of a write, so the
        /// limit checked before the SAS is issued (the client's declared size) is enforced here afterwards.
        /// ponytail: lists the whole container each sweep; switch to a Blob-created Event Grid trigger if it grows large.
        /// </summary>
        public virtual async Task<List<string>> BlobsLargerThanAsync(string container, long maxBytes, CancellationToken ct = default)
        {
            var c = client.GetBlobContainerClient(container);
            await c.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
            var oversized = new List<string>();
            await foreach (var b in c.GetBlobsAsync(cancellationToken: ct))
                if (b.Properties.ContentLength > maxBytes) oversized.Add(b.Name);
            return oversized;
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Gets a BlobClient for the given container/blob name, creating the container if it doesn't exist.
        /// </summary>
        /// <param name="container"></param>
        /// <param name="blobName"></param>
        /// <returns></returns>
        private async Task<BlobClient> GetBlobClientAsync(string container, string blobName)
        {
            var c = client.GetBlobContainerClient(container);
            await c.CreateIfNotExistsAsync(PublicAccessType.None);
            return c.GetBlobClient(blobName);
        }
//----------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Generates a SAS for a single blob. Throws if the BlobServiceClient was not created with a shared key.
        /// SAS is the only way to give a client direct access to a blob without passing the bytes through the API.
        /// </summary>
        /// <param name="blob"></param>
        /// <param name="permissions"></param>
        /// <param name="ttl"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static Uri Sas(BlobClient blob, BlobSasPermissions permissions, TimeSpan? ttl, string? downloadName = null)
        {
            if (!blob.CanGenerateSasUri)
                throw new InvalidOperationException(
                    "BlobServiceClient has no shared key. Use a connection string with an account key, " +
                    "or switch to a user-delegation SAS when moving to managed identity.");

            // Reference: Microsoft, "Create a service SAS for a blob with .NET." Accessed: Oct. 04, 2026. [Online]. Available: https://learn.microsoft.com/en-us/azure/storage/blobs/sas-service-create-dotnet
            var builder = new BlobSasBuilder
            {
                BlobContainerName = blob.BlobContainerName,
                BlobName = blob.Name,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(ttl ?? DefaultTtl),
                // Blob Storage sends this back as the response header, so the browser saves rather than previews.
                // Reference: J. Reschke, "Use of the Content-Disposition Header Field in the Hypertext Transfer Protocol (HTTP)," Internet Engineering Task Force (IETF), RFC 6266, Jun. 2011. Accessed: Oct. 04, 2026. [Online]. Available: https://www.rfc-editor.org/rfc/rfc6266
                ContentDisposition = downloadName is null ? null
                    : new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileNameStar = downloadName }.ToString(),
            };
            builder.SetPermissions(permissions);
            return blob.GenerateSasUri(builder);
        }
    }
}
//---------------------------DDDDDDDDDDDoooooooooooooo END OF FILE DDDDDDDDDDDoooooooooooooo---------------------------//