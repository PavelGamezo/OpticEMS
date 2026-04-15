using System.Xml.Serialization;

namespace OpticEMS.License.Common
{
    [XmlInclude(typeof(OpticEMSLicense))]
    public abstract class License : ILicense
    {
        [XmlIgnore]
        public string AppName { get; private set; }

        [XmlElement("Uid")]
        public string Uid { get; set; }

        [XmlElement("CreateDateTime")]
        public DateTime CreateDateTime { get; set; }

        [XmlElement("ExpireDateTime")]
        public DateTime ExpireDateTime { get; set; }

        public abstract LicenseStatus DoExtraValidation(out string validationMsg);
    }
}
