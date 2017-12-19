using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace azamsfunctions
{
    [DataContract]
    public class UpdateReferenceMessage
    {
        [DataMember]
        public string AssetId { get; set; }

        [DataMember]
        public AssetWorkflowStatus Status { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }
    }

    public enum AssetWorkflowStatus
    {
        Encoding, 
        ContentProtectionAdded, 
        Published, 
        Error
    }
}
