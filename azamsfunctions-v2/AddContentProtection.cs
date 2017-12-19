using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace azamsfunctions
{
    public static class AddContentProtection
    {
        [FunctionName("AddContentProtection")]
        public static async Task Run(
            [QueueTrigger("%ContentProtectionJobsQueueName%", Connection = "MediaStorageAccount")]
            string assetId,
            [Queue("%PublishJobsQueueName%", Connection = "MediaStorageAccount")]
            ICollector<string> outputPublishQueue,
            TraceWriter log)
        {
            log.Info($"C# Queue trigger function processed: {assetId}");

            var context = MediaContextHelper.CreateContext();

            var asset = context.Assets.Where(a => a.Id == assetId).FirstOrDefault();

            if (asset == null)
                return;

            // PlayReady + Widevine
            var commonEncryptionKey = await CreateCommonEncryptionKey(context, asset);
            var commonEncryptionAuthPolicy = context.ContentKeyAuthorizationPolicies
                .Where(p => p.Name == Environment.GetEnvironmentVariable("CommonEncryptionAuthorizationPolicyName"))
                .FirstOrDefault();

            if (commonEncryptionAuthPolicy == null)
                return;

            commonEncryptionKey.AuthorizationPolicyId = commonEncryptionAuthPolicy.Id;
            commonEncryptionKey = await commonEncryptionKey.UpdateAsync();

            var dynamicCommonEncryptionDeliveryPolicy = context.AssetDeliveryPolicies
                .Where(p => p.Name == Environment.GetEnvironmentVariable("DynamicCommonEncryptionDeliveryPolicyName"))
                .FirstOrDefault();

            if (dynamicCommonEncryptionDeliveryPolicy == null)
                return;

            asset.DeliveryPolicies.Add(dynamicCommonEncryptionDeliveryPolicy);

            // FairPlay
            var commonEncryptionCbcsKey = await CreateCommonEncryptionCbcsKey(context, asset);
            var commonEncryptionCbcsAuthPolicy = context.ContentKeyAuthorizationPolicies
                .Where(p => p.Name == Environment.GetEnvironmentVariable("CommonEncryptionCbcsAuthorizationPolicyName"))
                .FirstOrDefault();

            if (commonEncryptionCbcsAuthPolicy == null)
                return;

            commonEncryptionCbcsKey.AuthorizationPolicyId = commonEncryptionCbcsAuthPolicy.Id;
            commonEncryptionCbcsKey = await commonEncryptionCbcsKey.UpdateAsync();

            var dynamicCommonEncryptionCbcsDeliveryPolicy = context.AssetDeliveryPolicies
                .Where(p => p.Name == Environment.GetEnvironmentVariable("DynamicCommonEncryptionCbcsDeliveryPolicyName"))
                .FirstOrDefault();

            if (dynamicCommonEncryptionCbcsDeliveryPolicy == null)
                return;

            asset.DeliveryPolicies.Add(dynamicCommonEncryptionCbcsDeliveryPolicy);

            outputPublishQueue.Add(asset.Id);
        }

        public static async Task<IContentKey> CreateCommonEncryptionKey(MediaContextBase context, IAsset asset)
        {
            var keyId = Guid.NewGuid();
            var contentKey = GetRandomBuffer(16);

            var key = await context.ContentKeys.CreateAsync(
                        keyId,
                        contentKey,
                        "CommonEncryptionContentKey",
                        ContentKeyType.CommonEncryption);

            // Associate the key with the asset.
            asset.ContentKeys.Add(key);

            return key;
        }

        public static async Task<IContentKey> CreateCommonEncryptionCbcsKey(MediaContextBase context, IAsset asset)
        {
            var keyId = Guid.NewGuid();
            var contentKey = GetRandomBuffer(16);

            var key = await context.ContentKeys.CreateAsync(
                        keyId,
                        contentKey,
                        "CommonEncryptionCbcsContentKey",
                        ContentKeyType.CommonEncryptionCbcs);

            // Associate the key with the asset.
            asset.ContentKeys.Add(key);

            return key;
        }

        private static byte[] GetRandomBuffer(int length)
        {
            var returnValue = new byte[length];

            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(returnValue);
            }

            return returnValue;
        }
    }
}
