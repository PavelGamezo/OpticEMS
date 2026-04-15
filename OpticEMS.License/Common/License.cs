using System.Xml.Serialization;

namespace OpticEMS.License.Common
{
    [XmlInclude(typeof(OpticEMSLicense))]
    public abstract class License : ILicense
    {
        [XmlIgnore]
        public abstract string AppName { get; set; }

        [XmlElement("EntrySecret")]
        public abstract string EntrySecret { get; set; }

        [XmlElement("Uid")]
        public string Uid { get; set; }

        [XmlElement("CreateDateTime")]
        public DateTime CreateDateTime { get; set; }

        [XmlElement("ExpireDateTime")]
        public DateTime ExpireDateTime { get; set; }

        [XmlElement("ChannelCount")]
        public int ChannelCount { get; set; }

        public abstract LicenseStatus DoExtraValidation(out string validationMsg);
    }
}
