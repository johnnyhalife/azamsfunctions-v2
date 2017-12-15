using System;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;

namespace azamsfunctions
{
    public class MediaServiceHelper
    {
        public MediaServiceHelper() {  }

        public IMediaProcessor GetLatestMediaProcessorByName(string mediaProcessorName, CloudMediaContext _context)
        {
            var processor = _context.MediaProcessors.Where(p => p.Name == mediaProcessorName).
            ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

            if (processor == null)
                throw new ArgumentException(string.Format("Unknown media processor", mediaProcessorName));

            return processor;
        }

        public Uri GetValidOnDemandURI(IAsset asset, CloudMediaContext _context)
        {
            IEnumerable<Uri> aivalidurls = GetValidURIs(asset, _context);
            if (aivalidurls != null)
            {
                return aivalidurls.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        public IEnumerable<Uri> GetValidURIs(IAsset asset, CloudMediaContext _context)
        {
            IEnumerable<Uri> ValidURIs;
            var ismFile = asset.AssetFiles.AsEnumerable().Where(f => f.Name.EndsWith(".ism")).OrderByDescending(f => f.IsPrimary).FirstOrDefault();

            if (ismFile != null)
            {
                var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

                var se = _context.StreamingEndpoints.AsEnumerable().Where(o => (o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o))).OrderByDescending(o => o.CdnEnabled);

                if (se.Count() == 0) // No running which can do dynpackaging SE. Let's use the default one to get URL
                {
                    se = _context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
                }

                var template = new UriTemplate("{contentAccessComponent}/{ismFileName}/manifest");

                ValidURIs = locators.SelectMany(l =>
                    se.Select(
                            o =>
                                template.BindByPosition(new Uri("http://" + o.HostName), l.ContentAccessComponent,
                                    ismFile.Name)))
                    .ToArray();

                return ValidURIs;
            }
            else
            {
                return null;
            }
        }

        public Uri GetValidOnDemandPath(IAsset asset, CloudMediaContext _context)
        {
            var aivalidurls = GetValidPaths(asset, _context);
            if (aivalidurls != null)
            {
                return aivalidurls.FirstOrDefault();
            }
            else
            {
                return null;
            }
        }

        public IEnumerable<Uri> GetValidPaths(IAsset asset, CloudMediaContext _context)
        {
            IEnumerable<Uri> ValidURIs;

            var locators = asset.Locators.Where(l => l.Type == LocatorType.OnDemandOrigin && l.ExpirationDateTime > DateTime.UtcNow).OrderByDescending(l => l.ExpirationDateTime);

            var se = _context.StreamingEndpoints.AsEnumerable().Where(o => (o.State == StreamingEndpointState.Running) && (CanDoDynPackaging(o))).OrderByDescending(o => o.CdnEnabled);

            if (se.Count() == 0) // No running which can do dynpackaging SE. Let's use the default one to get URL
            {
                se = _context.StreamingEndpoints.AsEnumerable().Where(o => o.Name == "default").OrderByDescending(o => o.CdnEnabled);
            }

            var template = new UriTemplate("{contentAccessComponent}/");
            ValidURIs = locators.SelectMany(l => se.Select(
                        o =>
                            template.BindByPosition(new Uri("http://" + o.HostName), l.ContentAccessComponent)))
                .ToArray();

            return ValidURIs;
        }

        public bool CanDoDynPackaging(IStreamingEndpoint mySE)
        {
            return ReturnTypeSE(mySE) != StreamEndpointType.Classic;
        }

        public StreamEndpointType ReturnTypeSE(IStreamingEndpoint mySE)
        {
            if (mySE.ScaleUnits != null && mySE.ScaleUnits > 0)
            {
                return StreamEndpointType.Premium;
            }
            else
            {
                if (new Version(mySE.StreamingEndpointVersion) == new Version("1.0"))
                {
                    return StreamEndpointType.Classic;
                }
                else
                {
                    return StreamEndpointType.Standard;
                }
            }
        }

        public enum StreamEndpointType
        {
            Classic = 0,
            Standard,
            Premium
        }

    }
}